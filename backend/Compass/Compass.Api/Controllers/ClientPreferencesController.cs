using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Compass.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ClientPreferencesController : ControllerBase
{
    private readonly IClientPreferencesRepository _preferencesRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ILogger<ClientPreferencesController> _logger;

    public ClientPreferencesController(
        IClientPreferencesRepository preferencesRepository,
        IClientRepository clientRepository,
        ILogger<ClientPreferencesController> logger)
    {
        _preferencesRepository = preferencesRepository;
        _clientRepository = clientRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get client preferences by client ID
    /// </summary>
    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<ClientPreferencesResponse>> GetClientPreferences(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var preferences = await _preferencesRepository.GetByClientIdAsync(clientId, organizationId.Value);
            if (preferences == null)
            {
                return NotFound(new { error = "Client preferences not found", clientId });
            }

            var response = MapToResponse(preferences);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client preferences for client {ClientId}", clientId);
            return StatusCode(500, new { error = "Failed to retrieve client preferences" });
        }
    }

    /// <summary>
    /// Get all client preferences for the organization
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClientPreferencesResponse>>> GetAllClientPreferences()
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var preferences = await _preferencesRepository.GetActiveByOrganizationIdAsync(organizationId.Value);
            var responses = preferences.Select(MapToResponse).ToList();

            return Ok(responses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client preferences for organization");
            return StatusCode(500, new { error = "Failed to retrieve client preferences" });
        }
    }

    /// <summary>
    /// Create or update client preferences
    /// </summary>
    [HttpPost("client/{clientId}")]
    public async Task<ActionResult<ClientPreferencesResponse>> CreateOrUpdateClientPreferences(
        Guid clientId,
        [FromBody] CreateClientPreferencesRequest request)
    {
        try
        {
            // Debug: Log all claims
            _logger.LogInformation("JWT Claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));

            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCustomerIdFromContext();

            _logger.LogInformation("Extracted - OrganizationId: {OrgId}, CustomerId: {CustId}",
                organizationId, customerId);

            if (organizationId == null)
            {
                _logger.LogWarning("Organization ID is null");
                return BadRequest("Organization context not found");
            }

            if (customerId == null)
            {
                _logger.LogWarning("Customer ID is null");
                return BadRequest("Customer context not found");
            }

            // Verify client exists and belongs to organization
            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                _logger.LogWarning("Client {ClientId} not found in organization {OrgId}", clientId, organizationId);
                return NotFound(new { error = "Client not found", clientId });
            }

            // Check if preferences already exist
            var existingPreferences = await _preferencesRepository.GetByClientIdAsync(clientId, organizationId.Value);

            ClientPreferences preferences;
            if (existingPreferences != null)
            {
                // Update existing preferences
                preferences = UpdatePreferencesFromRequest(existingPreferences, request, customerId.Value);
                preferences = await _preferencesRepository.UpdateAsync(preferences);
                _logger.LogInformation("Updated client preferences for client {ClientId}", clientId);
            }
            else
            {
                // Create new preferences
                preferences = CreatePreferencesFromRequest(clientId, organizationId.Value, request, customerId.Value);
                preferences = await _preferencesRepository.CreateAsync(preferences);
                _logger.LogInformation("Created client preferences for client {ClientId}", clientId);
            }

            var response = MapToResponse(preferences);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/update client preferences for client {ClientId}", clientId);
            return StatusCode(500, new { error = "Failed to save client preferences" });
        }
    }

    /// <summary>
    /// Delete client preferences
    /// </summary>
    [HttpDelete("client/{clientId}")]
    public async Task<ActionResult> DeleteClientPreferences(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var preferences = await _preferencesRepository.GetByClientIdAsync(clientId, organizationId.Value);
            if (preferences == null)
            {
                return NotFound(new { error = "Client preferences not found", clientId });
            }

            await _preferencesRepository.DeleteAsync(preferences.ClientPreferencesId);

            _logger.LogInformation("Successfully deleted preferences for client {ClientId}", clientId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete client preferences for client {ClientId}", clientId);
            return StatusCode(500, new { error = "Failed to delete client preferences" });
        }
    }

    /// <summary>
    /// Check if client has preferences configured
    /// </summary>
    [HttpGet("client/{clientId}/exists")]
    public async Task<ActionResult<bool>> CheckClientPreferencesExist(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var exists = await _preferencesRepository.HasActivePreferencesAsync(clientId, organizationId.Value);
            return Ok(exists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if client preferences exist for client {ClientId}", clientId);
            return StatusCode(500, new { error = "Failed to check client preferences" });
        }
    }

    // Helper methods
    private Guid? GetOrganizationIdFromContext()
    {
        var orgClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgClaim, out var orgId) ? orgId : null;
    }

    private Guid? GetCustomerIdFromContext()
    {
        // Try multiple claim types for customer ID
        var customerClaim = User.FindFirst("customer_id")?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        return Guid.TryParse(customerClaim, out var customerId) ? customerId : null;
    }

    private ClientPreferences CreatePreferencesFromRequest(
        Guid clientId,
        Guid organizationId,
        CreateClientPreferencesRequest request,
        Guid customerId)
    {
        return new ClientPreferences
        {
            ClientId = clientId,
            OrganizationId = organizationId,
            AllowedNamingPatterns = request.AllowedNamingPatterns?.Any() == true
                ? JsonSerializer.Serialize(request.AllowedNamingPatterns)
                : null,
            RequiredNamingElements = request.RequiredNamingElements?.Any() == true
                ? JsonSerializer.Serialize(request.RequiredNamingElements)
                : null,
            EnvironmentIndicators = request.EnvironmentIndicators,
            RequiredTags = request.RequiredTags?.Any() == true
                ? JsonSerializer.Serialize(request.RequiredTags)
                : null,
            EnforceTagCompliance = request.EnforceTagCompliance,
            ComplianceFrameworks = request.ComplianceFrameworks?.Any() == true
                ? JsonSerializer.Serialize(request.ComplianceFrameworks)
                : null,
            CreatedByCustomerId = customerId
        };
    }

    private ClientPreferences UpdatePreferencesFromRequest(
        ClientPreferences existing,
        CreateClientPreferencesRequest request,
        Guid customerId)
    {
        existing.AllowedNamingPatterns = request.AllowedNamingPatterns?.Any() == true
            ? JsonSerializer.Serialize(request.AllowedNamingPatterns)
            : null;
        existing.RequiredNamingElements = request.RequiredNamingElements?.Any() == true
            ? JsonSerializer.Serialize(request.RequiredNamingElements)
            : null;
        existing.EnvironmentIndicators = request.EnvironmentIndicators;
        existing.RequiredTags = request.RequiredTags?.Any() == true
            ? JsonSerializer.Serialize(request.RequiredTags)
            : null;
        existing.EnforceTagCompliance = request.EnforceTagCompliance;
        existing.ComplianceFrameworks = request.ComplianceFrameworks?.Any() == true
            ? JsonSerializer.Serialize(request.ComplianceFrameworks)
            : null;
        existing.LastModifiedByCustomerId = customerId;

        return existing;
    }

    private ClientPreferencesResponse MapToResponse(ClientPreferences preferences)
    {
        return new ClientPreferencesResponse
        {
            ClientPreferencesId = preferences.ClientPreferencesId,
            ClientId = preferences.ClientId,
            ClientName = preferences.Client?.Name ?? "Unknown Client",
            AllowedNamingPatterns = !string.IsNullOrEmpty(preferences.AllowedNamingPatterns)
                ? JsonSerializer.Deserialize<List<string>>(preferences.AllowedNamingPatterns) ?? new List<string>()
                : new List<string>(),
            RequiredNamingElements = !string.IsNullOrEmpty(preferences.RequiredNamingElements)
                ? JsonSerializer.Deserialize<List<string>>(preferences.RequiredNamingElements) ?? new List<string>()
                : new List<string>(),
            EnvironmentIndicators = preferences.EnvironmentIndicators,
            RequiredTags = !string.IsNullOrEmpty(preferences.RequiredTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.RequiredTags) ?? new List<string>()
                : new List<string>(),
            EnforceTagCompliance = preferences.EnforceTagCompliance,
            ComplianceFrameworks = !string.IsNullOrEmpty(preferences.ComplianceFrameworks)
                ? JsonSerializer.Deserialize<List<string>>(preferences.ComplianceFrameworks) ?? new List<string>()
                : new List<string>(),
            CreatedDate = preferences.CreatedDate,
            LastModifiedDate = preferences.LastModifiedDate,
            IsActive = preferences.IsActive
        };
    }
}

// DTOs for the API
public class CreateClientPreferencesRequest
{
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; } = false;
    public List<string> RequiredTags { get; set; } = new();
    public bool EnforceTagCompliance { get; set; } = true;
    public List<string> ComplianceFrameworks { get; set; } = new();
}

public class ClientPreferencesResponse
{
    public Guid ClientPreferencesId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; }
    public List<string> RequiredTags { get; set; } = new();
    public bool EnforceTagCompliance { get; set; }
    public List<string> ComplianceFrameworks { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public bool IsActive { get; set; }
}