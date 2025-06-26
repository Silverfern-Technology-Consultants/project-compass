using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AzureEnvironmentController : ControllerBase
{
    private readonly CompassDbContext _context;
    private readonly IClientRepository _clientRepository;
    private readonly IClientService _clientService;
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly ILogger<AzureEnvironmentController> _logger;

    public AzureEnvironmentController(
        CompassDbContext context,
        IClientRepository clientRepository,
        IClientService clientService,
        IAzureResourceGraphService resourceGraphService,
        ILogger<AzureEnvironmentController> logger)
    {
        _context = context;
        _clientRepository = clientRepository;
        _clientService = clientService;
        _resourceGraphService = resourceGraphService;
        _logger = logger;
    }

    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<List<AzureEnvironmentDto>>> GetClientEnvironments(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Validate client access
            var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, clientId, "ViewEnvironments");
            if (!hasClientAccess)
            {
                return Forbid("You don't have permission to view environments for this client");
            }

            var environments = await _context.AzureEnvironments
                .Where(e => e.ClientId == clientId)
                .OrderBy(e => e.Name)
                .ToListAsync();

            var environmentDtos = environments.Select(e => new AzureEnvironmentDto
            {
                AzureEnvironmentId = e.AzureEnvironmentId,
                ClientId = e.ClientId,
                Name = e.Name,
                Description = e.Description,
                TenantId = e.TenantId,
                SubscriptionIds = e.SubscriptionIds,
                ServicePrincipalId = e.ServicePrincipalId,
                ServicePrincipalName = e.ServicePrincipalName,
                IsActive = e.IsActive,
                CreatedDate = e.CreatedDate,
                LastAccessDate = e.LastAccessDate,
                LastConnectionTest = e.LastConnectionTest,
                LastConnectionTestDate = e.LastConnectionTestDate,
                LastConnectionError = e.LastConnectionError
            }).ToList();

            return Ok(environmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environments for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{environmentId}")]
    public async Task<ActionResult<AzureEnvironmentDto>> GetEnvironment(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access through the environment's client
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ViewEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view this environment");
                }
            }

            var environmentDto = new AzureEnvironmentDto
            {
                AzureEnvironmentId = environment.AzureEnvironmentId,
                ClientId = environment.ClientId,
                Name = environment.Name,
                Description = environment.Description,
                TenantId = environment.TenantId,
                SubscriptionIds = environment.SubscriptionIds,
                ServicePrincipalId = environment.ServicePrincipalId,
                ServicePrincipalName = environment.ServicePrincipalName,
                IsActive = environment.IsActive,
                CreatedDate = environment.CreatedDate,
                LastAccessDate = environment.LastAccessDate,
                LastConnectionTest = environment.LastConnectionTest,
                LastConnectionTestDate = environment.LastConnectionTestDate,
                LastConnectionError = environment.LastConnectionError
            };

            return Ok(environmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<AzureEnvironmentDto>> CreateEnvironment([FromBody] CreateAzureEnvironmentRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Validate client access
            var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, request.ClientId, "ManageEnvironments");
            if (!hasClientAccess)
            {
                return Forbid("You don't have permission to create environments for this client");
            }

            // Verify client exists and belongs to organization
            var client = await _clientRepository.GetByIdAndOrganizationAsync(request.ClientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            // Check for duplicate environment name within client
            var existingEnvironment = await _context.AzureEnvironments
                .FirstOrDefaultAsync(e => e.ClientId == request.ClientId &&
                                         e.Name.ToLower() == request.Name.ToLower() &&
                                         e.IsActive);

            if (existingEnvironment != null)
            {
                return BadRequest("An environment with this name already exists for this client");
            }

            var environment = new AzureEnvironment
            {
                ClientId = request.ClientId,
                CustomerId = customerId.Value, // Still track who created it
                Name = request.Name,
                Description = request.Description,
                TenantId = request.TenantId,
                SubscriptionIds = request.SubscriptionIds,
                ServicePrincipalId = request.ServicePrincipalId,
                ServicePrincipalName = request.ServicePrincipalName,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.AzureEnvironments.Add(environment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Azure environment created: {EnvironmentId} ({Name}) for client {ClientId} by {CustomerId}",
                environment.AzureEnvironmentId, environment.Name, request.ClientId, customerId);

            var environmentDto = new AzureEnvironmentDto
            {
                AzureEnvironmentId = environment.AzureEnvironmentId,
                ClientId = environment.ClientId,
                Name = environment.Name,
                Description = environment.Description,
                TenantId = environment.TenantId,
                SubscriptionIds = environment.SubscriptionIds,
                ServicePrincipalId = environment.ServicePrincipalId,
                ServicePrincipalName = environment.ServicePrincipalName,
                IsActive = environment.IsActive,
                CreatedDate = environment.CreatedDate
            };

            return CreatedAtAction(nameof(GetEnvironment), new { environmentId = environment.AzureEnvironmentId }, environmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating environment for client {ClientId}", request.ClientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{environmentId}")]
    public async Task<ActionResult<AzureEnvironmentDto>> UpdateEnvironment(Guid environmentId, [FromBody] UpdateAzureEnvironmentRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ManageEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to update this environment");
                }
            }

            // Check for duplicate name if name is being changed
            if (request.Name != environment.Name)
            {
                var existingEnvironment = await _context.AzureEnvironments
                    .FirstOrDefaultAsync(e => e.ClientId == environment.ClientId &&
                                             e.Name.ToLower() == request.Name.ToLower() &&
                                             e.AzureEnvironmentId != environmentId &&
                                             e.IsActive);

                if (existingEnvironment != null)
                {
                    return BadRequest("An environment with this name already exists for this client");
                }
            }

            // Update environment properties
            environment.Name = request.Name;
            environment.Description = request.Description;
            environment.TenantId = request.TenantId;
            environment.SubscriptionIds = request.SubscriptionIds;
            environment.ServicePrincipalId = request.ServicePrincipalId;
            environment.ServicePrincipalName = request.ServicePrincipalName;
            environment.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Azure environment updated: {EnvironmentId} ({Name}) by {CustomerId}",
                environmentId, environment.Name, customerId);

            var environmentDto = new AzureEnvironmentDto
            {
                AzureEnvironmentId = environment.AzureEnvironmentId,
                ClientId = environment.ClientId,
                Name = environment.Name,
                Description = environment.Description,
                TenantId = environment.TenantId,
                SubscriptionIds = environment.SubscriptionIds,
                ServicePrincipalId = environment.ServicePrincipalId,
                ServicePrincipalName = environment.ServicePrincipalName,
                IsActive = environment.IsActive,
                CreatedDate = environment.CreatedDate,
                LastAccessDate = environment.LastAccessDate,
                LastConnectionTest = environment.LastConnectionTest,
                LastConnectionTestDate = environment.LastConnectionTestDate,
                LastConnectionError = environment.LastConnectionError
            };

            return Ok(environmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{environmentId}")]
    public async Task<IActionResult> DeleteEnvironment(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ManageEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to delete this environment");
                }
            }

            // Check if environment has any assessments
            var hasAssessments = await _context.Assessments
                .AnyAsync(a => a.EnvironmentId == environmentId);

            if (hasAssessments)
            {
                // Soft delete - mark as inactive
                environment.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Azure environment soft deleted: {EnvironmentId} ({Name}) by {CustomerId}",
                    environmentId, environment.Name, customerId);

                return Ok(new { message = "Environment deactivated successfully (assessments exist)" });
            }
            else
            {
                // Hard delete - no assessments depend on it
                _context.AzureEnvironments.Remove(environment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Azure environment deleted: {EnvironmentId} ({Name}) by {CustomerId}",
                    environmentId, environment.Name, customerId);

                return Ok(new { message = "Environment deleted successfully" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{environmentId}/test-connection")]
    public async Task<ActionResult<ConnectionTestResult>> TestEnvironmentConnection(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ViewEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to test this environment");
                }
            }

            // DEBUG: Log the subscription IDs being retrieved from the database
            _logger.LogInformation("Retrieved environment {EnvironmentId} with {Count} subscription IDs: {SubscriptionIds}",
                environmentId,
                environment.SubscriptionIds?.Count ?? 0,
                environment.SubscriptionIds != null ? string.Join(", ", environment.SubscriptionIds) : "null");

            // Validate that we have subscription IDs
            if (environment.SubscriptionIds == null || !environment.SubscriptionIds.Any())
            {
                var errorMessage = "No subscription IDs found for this environment";
                _logger.LogWarning("Connection test failed for environment {EnvironmentId}: {Error}", environmentId, errorMessage);

                environment.LastConnectionTest = false;
                environment.LastConnectionTestDate = DateTime.UtcNow;
                environment.LastConnectionError = errorMessage;
                environment.LastAccessDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new ConnectionTestResult
                {
                    Success = false,
                    Message = errorMessage,
                    ErrorCode = "NO_SUBSCRIPTIONS",
                    Details = new Dictionary<string, object>
                    {
                        ["EnvironmentId"] = environmentId,
                        ["SubscriptionIds"] = environment.SubscriptionIds ?? new List<string>(),
                        ["TestedAt"] = DateTime.UtcNow,
                        ["SubscriptionCount"] = 0
                    }
                });
            }

            // Convert to array and test connection
            var subscriptionArray = environment.SubscriptionIds.ToArray();
            _logger.LogInformation("Testing connection with subscription array: {SubscriptionArray}",
                string.Join(", ", subscriptionArray));

            var canConnect = await _resourceGraphService.TestConnectionAsync(subscriptionArray);

            // Update environment with test results
            environment.LastConnectionTest = canConnect;
            environment.LastConnectionTestDate = DateTime.UtcNow;
            environment.LastConnectionError = canConnect ? null : "Connection test failed";
            environment.LastAccessDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Connection test for environment {EnvironmentId}: {Result}",
                environmentId, canConnect ? "Success" : "Failed");

            return Ok(new ConnectionTestResult
            {
                Success = canConnect,
                Message = canConnect
                    ? "Successfully connected to all subscriptions"
                    : "Failed to connect to one or more subscriptions",
                Details = new Dictionary<string, object>
                {
                    ["EnvironmentId"] = environmentId,
                    ["SubscriptionIds"] = environment.SubscriptionIds,
                    ["TestedAt"] = DateTime.UtcNow,
                    ["SubscriptionCount"] = environment.SubscriptionIds.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for environment {EnvironmentId}", environmentId);

            // Update environment with error
            var environment = await _context.AzureEnvironments.FindAsync(environmentId);
            if (environment != null)
            {
                environment.LastConnectionTest = false;
                environment.LastConnectionTestDate = DateTime.UtcNow;
                environment.LastConnectionError = ex.Message;
                await _context.SaveChangesAsync();
            }

            return Ok(new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}",
                ErrorCode = "CONNECTION_ERROR",
                Details = new Dictionary<string, object>
                {
                    ["EnvironmentId"] = environmentId,
                    ["TestedAt"] = DateTime.UtcNow,
                    ["Error"] = ex.Message
                }
            });
        }
    }

    // Helper methods
    private Guid? GetOrganizationIdFromContext()
    {
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgIdClaim, out var organizationId) ? organizationId : null;
    }

    private Guid? GetCurrentCustomerId()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(customerIdClaim, out var customerId) ? customerId : null;
    }
}

// DTOs
public class AzureEnvironmentDto
{
    public Guid AzureEnvironmentId { get; set; }
    public Guid? ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public List<string> SubscriptionIds { get; set; } = new();
    public string? ServicePrincipalId { get; set; }
    public string? ServicePrincipalName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastAccessDate { get; set; }
    public bool? LastConnectionTest { get; set; }
    public DateTime? LastConnectionTestDate { get; set; }
    public string? LastConnectionError { get; set; }
}

public class CreateAzureEnvironmentRequest
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public List<string> SubscriptionIds { get; set; } = new();

    [StringLength(36)]
    public string? ServicePrincipalId { get; set; }

    [StringLength(100)]
    public string? ServicePrincipalName { get; set; }
}

public class UpdateAzureEnvironmentRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public List<string> SubscriptionIds { get; set; } = new();

    [StringLength(36)]
    public string? ServicePrincipalId { get; set; }

    [StringLength(100)]
    public string? ServicePrincipalName { get; set; }

    public bool IsActive { get; set; } = true;
}