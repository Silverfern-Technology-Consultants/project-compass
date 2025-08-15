using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Compass.Core.Interfaces;
using Compass.Core.Models;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionsService _permissionsService;
    private readonly ILogger<PermissionsController> _logger;

    public PermissionsController(
        IPermissionsService permissionsService,
        ILogger<PermissionsController> logger)
    {
        _permissionsService = permissionsService;
        _logger = logger;
    }

    /// <summary>
    /// Check permissions for a specific Azure environment and update database
    /// </summary>
    [HttpPost("environments/{azureEnvironmentId}/check-cost-permissions")]
    public async Task<ActionResult<CostManagementPermissionStatus>> CheckCostPermissions(
        Guid azureEnvironmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var status = await _permissionsService.CheckAndUpdateCostPermissionsAsync(
                azureEnvironmentId, organizationId.Value);

            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check cost permissions for environment {EnvironmentId}", azureEnvironmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get current permission status for an Azure environment (from database)
    /// </summary>
    [HttpGet("environments/{azureEnvironmentId}/status")]
    public async Task<ActionResult<EnvironmentPermissionStatus>> GetEnvironmentStatus(
        Guid azureEnvironmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var status = await _permissionsService.GetEnvironmentPermissionStatusAsync(
                azureEnvironmentId, organizationId.Value);

            return Ok(status);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get environment status for {EnvironmentId}", azureEnvironmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Mark cost management as ready after setup completion
    /// </summary>
    [HttpPost("environments/{azureEnvironmentId}/mark-ready")]
    public async Task<ActionResult> MarkCostManagementReady(
        Guid azureEnvironmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            await _permissionsService.MarkCostManagementReadyAsync(
                azureEnvironmentId, organizationId.Value);

            return Ok(new { message = "Cost management status updated" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark cost management ready for environment {EnvironmentId}", azureEnvironmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get setup instructions for cost management
    /// </summary>
    [HttpGet("environments/{azureEnvironmentId}/setup-instructions")]
    public async Task<ActionResult<CostManagementSetupInstructions>> GetSetupInstructions(
        Guid azureEnvironmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var instructions = await _permissionsService.GetCostSetupInstructionsAsync(
                azureEnvironmentId, organizationId.Value);

            return Ok(instructions);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get setup instructions for environment {EnvironmentId}", azureEnvironmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Check permissions across all environments for a client
    /// </summary>
    [HttpPost("clients/{clientId}/check-all-permissions")]
    public async Task<ActionResult<ClientPermissionMatrix>> CheckClientPermissions(
        Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var matrix = await _permissionsService.CheckClientPermissionsAsync(
                clientId, organizationId.Value);

            return Ok(matrix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check client permissions for {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Comprehensive permissions tool - checks all clients and environments
    /// </summary>
    [HttpPost("check-all")]
    public async Task<ActionResult<OrganizationPermissionReport>> CheckAllPermissions()
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // This would be implemented to check all clients in the organization
            // For now, return a placeholder
            var report = new OrganizationPermissionReport
            {
                OrganizationId = organizationId.Value,
                CheckedAt = DateTime.UtcNow,
                Status = "Checking permissions across all clients and environments..."
            };

            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check all permissions for organization");
            return StatusCode(500, "Internal server error");
        }
    }

    private Guid? GetOrganizationIdFromContext()
    {
        var orgClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgClaim, out var orgId) ? orgId : null;
    }
}

// New DTOs for permissions endpoints
public class OrganizationPermissionReport
{
    public Guid OrganizationId { get; set; }
    public DateTime CheckedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ClientPermissionMatrix> ClientMatrices { get; set; } = new();
}
