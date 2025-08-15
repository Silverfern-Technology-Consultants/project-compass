using Compass.Data.Entities;
using Compass.Data.Interfaces;
using Compass.Core.Models.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using DataEntities = Compass.Data.Entities;
using CoreModels = Compass.Core.Models;

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
            var responses = preferences.Select(p => MapToResponse(p)).ToList();

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
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCustomerIdFromContext();

            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            if (customerId == null)
            {
                return BadRequest("Customer context not found");
            }

            // Verify client exists and belongs to organization
            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound(new { error = "Client not found", clientId });
            }

            // Check if preferences already exist
            var existingPreferences = await _preferencesRepository.GetByClientIdAsync(clientId, organizationId.Value);

            DataEntities.ClientPreferences preferences;
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

    /// <summary>
    /// Preview naming scheme examples
    /// </summary>
    [HttpPost("client/{clientId}/naming-scheme/preview")]
    public async Task<ActionResult<List<NamingSchemeExample>>> PreviewNamingScheme(
        Guid clientId,
        [FromBody] NamingSchemeConfiguration namingScheme)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Create temporary config for preview
            var tempConfig = new ClientAssessmentConfiguration
            {
                ClientId = clientId,
                ClientName = "Preview",
                NamingScheme = namingScheme
            };

            // Generate examples
            var examples = tempConfig.GenerateNamingExamples();

            _logger.LogInformation("Generated {ExampleCount} naming scheme examples for preview", examples.Count);
            return Ok(examples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview naming scheme for client {ClientId}", clientId);
            return StatusCode(500, new { error = "Failed to generate naming scheme preview" });
        }
    }

    /// <summary>
    /// Get default component definitions for the scheme constructor
    /// </summary>
    [HttpGet("component-definitions")]
    public ActionResult<List<ComponentDefinition>> GetComponentDefinitions()
    {
        var definitions = new List<ComponentDefinition>
        {
            new ComponentDefinition
            {
                ComponentType = "company",
                DisplayName = "Company",
                Description = "Organization identifier for resource ownership",
                DefaultFormat = "3-letter abbreviation",
                CommonValues = new List<string> {"abc", "xyz" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "environment",
                DisplayName = "Environment",
                Description = "Deployment environment classification",
                DefaultFormat = "Lowercase, standardized values",
                CommonValues = new List<string> { "prod", "dev", "test", "shared", "staging" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "service",
                DisplayName = "Service/Application",
                Description = "Business function or application identifier",
                DefaultFormat = "Lowercase, descriptive",
                CommonValues = new List<string> { "veeam", "boomi", "crm", "api", "web" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "application",
                DisplayName = "Application",
                Description = "Application or workload identifier",
                DefaultFormat = "Lowercase, descriptive",
                CommonValues = new List<string> { "webapp", "api", "database", "cache" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "service/application",
                DisplayName = "Service/Application",
                Description = "Combined service and application identifier",
                DefaultFormat = "Lowercase, descriptive",
                CommonValues = new List<string> { "veeam", "boomi", "webapp", "api" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "resource-type",
                DisplayName = "Resource Type",
                Description = "Azure resource type abbreviation",
                DefaultFormat = "Lowercase abbreviation",
                CommonValues = new List<string> { "vm", "rg", "kv", "app", "st", "sql" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "instance",
                DisplayName = "Instance",
                Description = "Sequential number for multiple instances",
                DefaultFormat = "Zero-padded numbers",
                CommonValues = new List<string> { "01", "02", "03", "1", "2", "3" },
                IsSystemDefined = true
            },
            new ComponentDefinition
            {
                ComponentType = "location",
                DisplayName = "Location",
                Description = "Azure region or location identifier",
                DefaultFormat = "Short region code",
                CommonValues = new List<string> { "eus", "wus", "neu", "sea" },
                IsSystemDefined = true
            }
        };

        return Ok(definitions);
    }

    /// <summary>
    /// Validate naming scheme configuration
    /// </summary>
    [HttpPost("naming-scheme/validate")]
    public ActionResult<NamingSchemeValidationResponse> ValidateNamingScheme(
        [FromBody] NamingSchemeConfiguration namingScheme)
    {
        try
        {
            var response = new NamingSchemeValidationResponse
            {
                IsValid = true,
                Issues = new List<string>(),
                Warnings = new List<string>()
            };

            // Validate component requirements
            if (!namingScheme.Components.Any())
            {
                response.IsValid = false;
                response.Issues.Add("At least one component is required");
            }

            // Check for duplicate positions
            var positions = namingScheme.Components.Select(c => c.Position).ToList();
            var duplicatePositions = positions.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key);
            if (duplicatePositions.Any())
            {
                response.IsValid = false;
                response.Issues.Add($"Duplicate positions found: {string.Join(", ", duplicatePositions)}");
            }

            // Validate separator
            if (string.IsNullOrEmpty(namingScheme.Separator))
            {
                response.Warnings.Add("No separator specified - components will be concatenated");
            }
            else if (namingScheme.Separator.Length > 1)
            {
                response.Warnings.Add("Multi-character separators may cause parsing issues");
            }

            // Check for required components
            var hasResourceType = namingScheme.Components.Any(c => c.ComponentType == "resource-type");
            if (!hasResourceType)
            {
                response.Warnings.Add("No resource-type component found - resource identification may be difficult");
            }

            // Validate component formats
            foreach (var component in namingScheme.Components)
            {
                if (component.IsRequired && component.AllowedValues.Any() && string.IsNullOrEmpty(component.DefaultValue))
                {
                    response.Warnings.Add($"Required component '{component.ComponentType}' has no default value");
                }
            }

            _logger.LogInformation("Naming scheme validation completed. Valid: {IsValid}, Issues: {IssueCount}, Warnings: {WarningCount}",
                response.IsValid, response.Issues.Count, response.Warnings.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate naming scheme");
            return StatusCode(500, new { error = "Failed to validate naming scheme" });
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
        var customerClaim = User.FindFirst("customer_id")?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

        return Guid.TryParse(customerClaim, out var customerId) ? customerId : null;
    }

    private DataEntities.ClientPreferences CreatePreferencesFromRequest(
    Guid clientId,
    Guid organizationId,
    CreateClientPreferencesRequest request,
    Guid customerId)
    {
        return new DataEntities.ClientPreferences
        {
            ClientId = clientId,
            OrganizationId = organizationId,

            // Legacy fields (for backward compatibility)
            AllowedNamingPatterns = request.AllowedNamingPatterns?.Any() == true
                ? JsonSerializer.Serialize(request.AllowedNamingPatterns)
                : null,
            RequiredNamingElements = request.RequiredNamingElements?.Any() == true
                ? JsonSerializer.Serialize(request.RequiredNamingElements)
                : null,
            EnvironmentIndicators = request.EnvironmentIndicators,

            // NEW: Naming scheme configuration
            NamingSchemeConfiguration = request.NamingScheme != null
                ? JsonSerializer.Serialize(request.NamingScheme)
                : null,
            ComponentDefinitions = request.ComponentDefinitions?.Any() == true
                ? JsonSerializer.Serialize(request.ComponentDefinitions)
                : null,

            // NEW: Accepted company names
            AcceptedCompanyNames = request.AcceptedCompanyNames?.Any() == true
                ? JsonSerializer.Serialize(request.AcceptedCompanyNames)
                : null,

            // NEW: Service Abbreviations
            ServiceAbbreviations = request.ServiceAbbreviations?.Any() == true
                ? JsonSerializer.Serialize(request.ServiceAbbreviations)
                : null,

            // Enhanced fields
            NamingStyle = request.NamingStyle,
            EnvironmentSize = request.EnvironmentSize,
            OrganizationMethod = request.OrganizationMethod,
            EnvironmentIndicatorLevel = request.EnvironmentIndicatorLevel,

            // Tagging fields
            RequiredTags = request.RequiredTags?.Any() == true
                ? JsonSerializer.Serialize(request.RequiredTags)
                : null,
            EnforceTagCompliance = request.EnforceTagCompliance,
            TaggingApproach = request.TaggingApproach,
            SelectedTags = request.SelectedTags?.Any() == true
                ? JsonSerializer.Serialize(request.SelectedTags)
                : null,
            CustomTags = request.CustomTags?.Any() == true
                ? JsonSerializer.Serialize(request.CustomTags)
                : null,

            // Compliance fields
            ComplianceFrameworks = request.ComplianceFrameworks?.Any() == true
                ? JsonSerializer.Serialize(request.ComplianceFrameworks)
                : null,
            SelectedCompliances = request.SelectedCompliances?.Any() == true
                ? JsonSerializer.Serialize(request.SelectedCompliances)
                : null,
            NoSpecificRequirements = request.NoSpecificRequirements,

            CreatedByCustomerId = customerId
        };
    }

    private DataEntities.ClientPreferences UpdatePreferencesFromRequest(
        DataEntities.ClientPreferences existing,
        CreateClientPreferencesRequest request,
        Guid customerId)
    {
        // Legacy fields (for backward compatibility)
        existing.AllowedNamingPatterns = request.AllowedNamingPatterns?.Any() == true
            ? JsonSerializer.Serialize(request.AllowedNamingPatterns)
            : null;
        existing.RequiredNamingElements = request.RequiredNamingElements?.Any() == true
            ? JsonSerializer.Serialize(request.RequiredNamingElements)
            : null;
        existing.EnvironmentIndicators = request.EnvironmentIndicators;

        // NEW: Naming scheme configuration
        existing.NamingSchemeConfiguration = request.NamingScheme != null
            ? JsonSerializer.Serialize(request.NamingScheme)
            : null;
        existing.ComponentDefinitions = request.ComponentDefinitions?.Any() == true
            ? JsonSerializer.Serialize(request.ComponentDefinitions)
            : null;

        // NEW: Accepted company names
        existing.AcceptedCompanyNames = request.AcceptedCompanyNames?.Any() == true
            ? JsonSerializer.Serialize(request.AcceptedCompanyNames)
            : null;

        // NEW: Service Abbreviations
        existing.ServiceAbbreviations = request.ServiceAbbreviations?.Any() == true
            ? JsonSerializer.Serialize(request.ServiceAbbreviations)
            : null;

        // Enhanced fields
        existing.NamingStyle = request.NamingStyle;
        existing.EnvironmentSize = request.EnvironmentSize;
        existing.OrganizationMethod = request.OrganizationMethod;
        existing.EnvironmentIndicatorLevel = request.EnvironmentIndicatorLevel;

        // Tagging fields
        existing.RequiredTags = request.RequiredTags?.Any() == true
            ? JsonSerializer.Serialize(request.RequiredTags)
            : null;
        existing.EnforceTagCompliance = request.EnforceTagCompliance;
        existing.TaggingApproach = request.TaggingApproach;
        existing.SelectedTags = request.SelectedTags?.Any() == true
            ? JsonSerializer.Serialize(request.SelectedTags)
            : null;
        existing.CustomTags = request.CustomTags?.Any() == true
            ? JsonSerializer.Serialize(request.CustomTags)
            : null;

        // Compliance fields
        existing.ComplianceFrameworks = request.ComplianceFrameworks?.Any() == true
            ? JsonSerializer.Serialize(request.ComplianceFrameworks)
            : null;
        existing.SelectedCompliances = request.SelectedCompliances?.Any() == true
            ? JsonSerializer.Serialize(request.SelectedCompliances)
            : null;
        existing.NoSpecificRequirements = request.NoSpecificRequirements;

        existing.LastModifiedByCustomerId = customerId;

        return existing;
    }

    private ClientPreferencesResponse MapToResponse(DataEntities.ClientPreferences preferences)
    {
        return new ClientPreferencesResponse
        {
            ClientPreferencesId = preferences.ClientPreferencesId,
            ClientId = preferences.ClientId,
            ClientName = preferences.Client?.Name ?? "Unknown Client",

            // Legacy fields
            AllowedNamingPatterns = !string.IsNullOrEmpty(preferences.AllowedNamingPatterns)
                ? JsonSerializer.Deserialize<List<string>>(preferences.AllowedNamingPatterns) ?? new List<string>()
                : new List<string>(),
            RequiredNamingElements = !string.IsNullOrEmpty(preferences.RequiredNamingElements)
                ? JsonSerializer.Deserialize<List<string>>(preferences.RequiredNamingElements) ?? new List<string>()
                : new List<string>(),
            EnvironmentIndicators = preferences.EnvironmentIndicators,

            // NEW: Naming scheme configuration
            NamingScheme = !string.IsNullOrEmpty(preferences.NamingSchemeConfiguration)
                ? JsonSerializer.Deserialize<NamingSchemeConfiguration>(preferences.NamingSchemeConfiguration)
                : null,
            ComponentDefinitions = !string.IsNullOrEmpty(preferences.ComponentDefinitions)
                ? JsonSerializer.Deserialize<List<ComponentDefinition>>(preferences.ComponentDefinitions) ?? new List<ComponentDefinition>()
                : new List<ComponentDefinition>(),

            // NEW: Accepted company names
            AcceptedCompanyNames = !string.IsNullOrEmpty(preferences.AcceptedCompanyNames)
                ? JsonSerializer.Deserialize<List<string>>(preferences.AcceptedCompanyNames) ?? new List<string>()
                : new List<string>(),

            // NEW: Service Abbreviations
            ServiceAbbreviations = !string.IsNullOrEmpty(preferences.ServiceAbbreviations)
                ? JsonSerializer.Deserialize<List<CoreModels.ServiceAbbreviationDto>>(preferences.ServiceAbbreviations) ?? new List<CoreModels.ServiceAbbreviationDto>()
                : new List<CoreModels.ServiceAbbreviationDto>(),

            // Enhanced fields
            NamingStyle = preferences.NamingStyle,
            EnvironmentSize = preferences.EnvironmentSize,
            OrganizationMethod = preferences.OrganizationMethod,
            EnvironmentIndicatorLevel = preferences.EnvironmentIndicatorLevel,

            // Tagging fields
            RequiredTags = !string.IsNullOrEmpty(preferences.RequiredTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.RequiredTags) ?? new List<string>()
                : new List<string>(),
            EnforceTagCompliance = preferences.EnforceTagCompliance,
            TaggingApproach = preferences.TaggingApproach,
            SelectedTags = !string.IsNullOrEmpty(preferences.SelectedTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.SelectedTags) ?? new List<string>()
                : new List<string>(),
            CustomTags = !string.IsNullOrEmpty(preferences.CustomTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.CustomTags) ?? new List<string>()
                : new List<string>(),

            // Compliance fields
            ComplianceFrameworks = !string.IsNullOrEmpty(preferences.ComplianceFrameworks)
                ? JsonSerializer.Deserialize<List<string>>(preferences.ComplianceFrameworks) ?? new List<string>()
                : new List<string>(),
            SelectedCompliances = !string.IsNullOrEmpty(preferences.SelectedCompliances)
                ? JsonSerializer.Deserialize<List<string>>(preferences.SelectedCompliances) ?? new List<string>()
                : new List<string>(),
            NoSpecificRequirements = preferences.NoSpecificRequirements,

            CreatedDate = preferences.CreatedDate,
            LastModifiedDate = preferences.LastModifiedDate,
            IsActive = preferences.IsActive
        };
    }
}

// DTOs for the API
public class CreateClientPreferencesRequest
{
    // Legacy fields (for backward compatibility)
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; } = false;
    public List<string> RequiredTags { get; set; } = new();
    public bool EnforceTagCompliance { get; set; } = true;
    public List<string> ComplianceFrameworks { get; set; } = new();

    // NEW: Accepted company names for validation
    public List<string> AcceptedCompanyNames { get; set; } = new();

    // NEW: Service Abbreviations (Phase 1 - Service Abbreviations Feature)  
    public List<CoreModels.ServiceAbbreviationDto> ServiceAbbreviations { get; set; } = new();

    // Naming scheme configuration
    public NamingSchemeConfiguration? NamingScheme { get; set; }
    public List<ComponentDefinition> ComponentDefinitions { get; set; } = new();

    // Enhanced fields
    public string? NamingStyle { get; set; }
    public string? TaggingApproach { get; set; }
    public string? EnvironmentSize { get; set; }
    public string? OrganizationMethod { get; set; }
    public string? EnvironmentIndicatorLevel { get; set; }

    public List<string> SelectedTags { get; set; } = new();
    public List<string> CustomTags { get; set; } = new();
    public List<string> SelectedCompliances { get; set; } = new();
    public bool NoSpecificRequirements { get; set; } = false;
}

public class ClientPreferencesResponse
{
    public Guid ClientPreferencesId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    // Legacy fields (for backward compatibility)
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; }
    public List<string> RequiredTags { get; set; } = new();
    public bool EnforceTagCompliance { get; set; }
    public List<string> ComplianceFrameworks { get; set; } = new();

    // Naming scheme configuration
    public NamingSchemeConfiguration? NamingScheme { get; set; }
    public List<ComponentDefinition> ComponentDefinitions { get; set; } = new();

    // NEW: Accepted company names for validation
    public List<string> AcceptedCompanyNames { get; set; } = new();

    // NEW: Service Abbreviations (Phase 1 - Service Abbreviations Feature)
    public List<CoreModels.ServiceAbbreviationDto> ServiceAbbreviations { get; set; } = new();

    // Enhanced fields
    public string? NamingStyle { get; set; }
    public string? TaggingApproach { get; set; }
    public string? EnvironmentSize { get; set; }
    public string? OrganizationMethod { get; set; }
    public string? EnvironmentIndicatorLevel { get; set; }

    public List<string> SelectedTags { get; set; } = new();
    public List<string> CustomTags { get; set; } = new();
    public List<string> SelectedCompliances { get; set; } = new();
    public bool NoSpecificRequirements { get; set; }
    public bool HasPreferences { get; set; }

    // Audit fields
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public bool IsActive { get; set; }
}

// Additional DTOs for naming scheme functionality
public class NamingSchemeValidationResponse
{
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}