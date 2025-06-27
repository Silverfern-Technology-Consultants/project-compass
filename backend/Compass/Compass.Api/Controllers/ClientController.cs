using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientController : ControllerBase
{
    private readonly CompassDbContext _context;
    private readonly IClientRepository _clientRepository;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ILogger<ClientController> _logger;

    public ClientController(
        CompassDbContext context,
        IClientRepository clientRepository,
        IAssessmentRepository assessmentRepository,
        ILogger<ClientController> logger)
    {
        _context = context;
        _clientRepository = clientRepository;
        _assessmentRepository = assessmentRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ClientSummaryDto>>> GetClients([FromQuery] string? search = null)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var clients = string.IsNullOrEmpty(search)
                ? await _clientRepository.GetActiveByOrganizationIdAsync(organizationId.Value)
                : await _clientRepository.SearchClientsAsync(organizationId.Value, search);

            var clientSummaries = new List<ClientSummaryDto>();

            foreach (var client in clients)
            {
                // Get client statistics
                var assessmentCount = await _context.Assessments
                .CountAsync(a => a.ClientId == client.ClientId);

                var environmentCount = await _context.AzureEnvironments
                    .CountAsync(e => e.ClientId == client.ClientId);

                // Count Azure subscription IDs from environments (stored as JSON string)
                var subscriptionCount = 0;
                var clientEnvironments = await _context.AzureEnvironments
                    .Where(e => e.ClientId == client.ClientId)
                    .ToListAsync();

                foreach (var environment in clientEnvironments)
                {
                    subscriptionCount += environment.SubscriptionIds?.Count ?? 0;
                }

                clientSummaries.Add(new ClientSummaryDto
                {
                    ClientId = client.ClientId,
                    Name = client.Name,
                    Industry = client.Industry,
                    ContactName = client.ContactName,
                    ContactEmail = client.ContactEmail,
                    Status = client.Status,
                    ContractStartDate = client.ContractStartDate,
                    ContractEndDate = client.ContractEndDate,
                    AssessmentCount = assessmentCount,
                    EnvironmentCount = environmentCount,
                    SubscriptionCount = subscriptionCount,
                    CreatedDate = client.CreatedDate,
                    HasActiveContract = client.HasActiveContract
                });
            }

            _logger.LogInformation("Retrieved {ClientCount} clients for organization {OrganizationId}",
                clientSummaries.Count, organizationId);

            return Ok(clientSummaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving clients for organization {OrganizationId}", GetOrganizationIdFromContext());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{clientId}")]
    public async Task<ActionResult<ClientDetailDto>> GetClient(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            // Check user access to this client
            var currentCustomerId = GetCurrentCustomerId();
            if (currentCustomerId == null)
            {
                return Unauthorized();
            }

            var hasAccess = await HasClientAccess(currentCustomerId.Value, clientId);
            if (!hasAccess)
            {
                return Forbid("You don't have access to this client");
            }

            // Get detailed statistics
            var assessments = await _context.Assessments
            .Where(a => a.ClientId == clientId)
            .Include(a => a.Customer)
            .ToListAsync();

            var environments = await _context.AzureEnvironments
                .Where(e => e.ClientId == clientId)
                .ToListAsync();

            // Count Azure subscription IDs from environments
            var subscriptionCount = 0;
            foreach (var environment in environments)
            {
                subscriptionCount += environment.SubscriptionIds?.Count ?? 0;
            }

            var clientAccess = await _clientRepository.GetClientAccessUsersAsync(clientId);

            var clientDetail = new ClientDetailDto
            {
                ClientId = client.ClientId,
                Name = client.Name,
                Description = client.Description,
                Industry = client.Industry,
                ContactName = client.ContactName,
                ContactEmail = client.ContactEmail,
                ContactPhone = client.ContactPhone,
                Address = client.Address,
                City = client.City,
                State = client.State,
                Country = client.Country,
                PostalCode = client.PostalCode,
                Status = client.Status,
                TimeZone = client.TimeZone,
                ContractStartDate = client.ContractStartDate,
                ContractEndDate = client.ContractEndDate,
                CreatedDate = client.CreatedDate,
                CreatedBy = client.CreatedBy?.FullName,

                // Statistics
                TotalAssessments = assessments.Count,
                CompletedAssessments = assessments.Count(a => a.Status == "Completed"),
                TotalEnvironments = environments.Count,
                TotalSubscriptions = subscriptionCount,

                // Recent activity
                RecentAssessments = assessments
                    .OrderByDescending(a => a.StartedDate)
                    .Take(5)
                    .Select(a => new AssessmentSummaryDto
                    {
                        AssessmentId = a.Id,
                        Name = a.Name,
                        Status = a.Status,
                        StartedDate = a.StartedDate,
                        CompletedDate = a.CompletedDate,
                        OverallScore = a.OverallScore
                    }).ToList(),

                // Team access
                TeamAccess = clientAccess.Select(ca => new ClientAccessDto
                {
                    CustomerId = ca.CustomerId,
                    CustomerName = ca.Customer.FullName,
                    CustomerEmail = ca.Customer.Email,
                    AccessLevel = ca.AccessLevel,
                    CanViewAssessments = ca.CanViewAssessments,
                    CanCreateAssessments = ca.CanCreateAssessments,
                    CanDeleteAssessments = ca.CanDeleteAssessments,
                    CanManageEnvironments = ca.CanManageEnvironments,
                    GrantedDate = ca.CreatedDate,
                    GrantedBy = ca.GrantedBy?.FullName
                }).ToList()
            };

            return Ok(clientDetail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<ClientSummaryDto>> CreateClient([FromBody] CreateClientRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var currentCustomerId = GetCurrentCustomerId();

            if (organizationId == null || currentCustomerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Check permissions - only Owner/Admin can create clients
            if (!CanManageClients())
            {
                return Forbid("You don't have permission to create clients");
            }

            // Validate unique client name within organization
            var isNameUnique = await _clientRepository.IsClientNameUniqueAsync(request.Name, organizationId.Value);
            if (!isNameUnique)
            {
                return BadRequest("A client with this name already exists in your organization");
            }

            var client = new Client
            {
                OrganizationId = organizationId.Value,
                Name = request.Name,
                Description = request.Description,
                Industry = request.Industry,
                ContactName = request.ContactName,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                Address = request.Address,
                City = request.City,
                State = request.State,
                Country = request.Country,
                PostalCode = request.PostalCode,
                TimeZone = request.TimeZone,
                ContractStartDate = request.ContractStartDate,
                ContractEndDate = request.ContractEndDate,
                CreatedByCustomerId = currentCustomerId.Value,
                Settings = request.Settings
            };

            var createdClient = await _clientRepository.CreateAsync(client);

            // Grant full access to the creator
            var creatorAccess = new ClientAccess
            {
                CustomerId = currentCustomerId.Value,
                ClientId = createdClient.ClientId,
                AccessLevel = "Admin",
                CanViewAssessments = true,
                CanCreateAssessments = true,
                CanDeleteAssessments = true,
                CanManageEnvironments = true,
                CanViewReports = true,
                CanExportData = true,
                GrantedByCustomerId = currentCustomerId.Value
            };

            await _clientRepository.GrantClientAccessAsync(creatorAccess);

            _logger.LogInformation("Client created: {ClientId} ({Name}) by {CustomerId} in organization {OrganizationId}",
                createdClient.ClientId, createdClient.Name, currentCustomerId, organizationId);

            var summary = new ClientSummaryDto
            {
                ClientId = createdClient.ClientId,
                Name = createdClient.Name,
                Industry = createdClient.Industry,
                ContactName = createdClient.ContactName,
                ContactEmail = createdClient.ContactEmail,
                Status = createdClient.Status,
                ContractStartDate = createdClient.ContractStartDate,
                ContractEndDate = createdClient.ContractEndDate,
                AssessmentCount = 0,
                EnvironmentCount = 0,
                SubscriptionCount = 0,
                CreatedDate = createdClient.CreatedDate,
                HasActiveContract = createdClient.HasActiveContract
            };

            return CreatedAtAction(nameof(GetClient), new { clientId = createdClient.ClientId }, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating client {ClientName}", request.Name);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{clientId}")]
    public async Task<ActionResult<ClientSummaryDto>> UpdateClient(Guid clientId, [FromBody] UpdateClientRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var currentCustomerId = GetCurrentCustomerId();

            if (organizationId == null || currentCustomerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            // Check permissions
            var hasAccess = await HasClientManagementAccess(currentCustomerId.Value, clientId);
            if (!hasAccess)
            {
                return Forbid("You don't have permission to update this client");
            }

            // Validate unique name if changed
            if (request.Name != client.Name)
            {
                var isNameUnique = await _clientRepository.IsClientNameUniqueAsync(request.Name, organizationId.Value, clientId);
                if (!isNameUnique)
                {
                    return BadRequest("A client with this name already exists in your organization");
                }
            }

            // Update client properties
            client.Name = request.Name;
            client.Description = request.Description;
            client.Industry = request.Industry;
            client.ContactName = request.ContactName;
            client.ContactEmail = request.ContactEmail;
            client.ContactPhone = request.ContactPhone;
            client.Address = request.Address;
            client.City = request.City;
            client.State = request.State;
            client.Country = request.Country;
            client.PostalCode = request.PostalCode;
            client.TimeZone = request.TimeZone;
            client.Status = request.Status;
            client.ContractStartDate = request.ContractStartDate;
            client.ContractEndDate = request.ContractEndDate;
            client.LastModifiedByCustomerId = currentCustomerId.Value;
            client.Settings = request.Settings;

            var updatedClient = await _clientRepository.UpdateAsync(client);

            _logger.LogInformation("Client updated: {ClientId} ({Name}) by {CustomerId}",
                clientId, updatedClient.Name, currentCustomerId);

            var summary = new ClientSummaryDto
            {
                ClientId = updatedClient.ClientId,
                Name = updatedClient.Name,
                Industry = updatedClient.Industry,
                ContactName = updatedClient.ContactName,
                ContactEmail = updatedClient.ContactEmail,
                Status = updatedClient.Status,
                ContractStartDate = updatedClient.ContractStartDate,
                ContractEndDate = updatedClient.ContractEndDate,
                CreatedDate = updatedClient.CreatedDate,
                HasActiveContract = updatedClient.HasActiveContract
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{clientId}")]
    public async Task<IActionResult> DeleteClient(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var currentCustomerId = GetCurrentCustomerId();

            if (organizationId == null || currentCustomerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            // Check permissions - only Owner/Admin can delete clients
            if (!CanManageClients())
            {
                return Forbid("You don't have permission to delete clients");
            }

            // Check if client has any associated data
            var hasAssessments = await _context.Assessments.AnyAsync(a => a.ClientId == clientId);
            var hasEnvironments = await _context.AzureEnvironments.AnyAsync(e => e.ClientId == clientId);
            var hasSubscriptions = await _context.Subscriptions.AnyAsync(s => s.ClientId == clientId);

            if (hasAssessments || hasEnvironments || hasSubscriptions)
            {
                return BadRequest(new
                {
                    error = "Cannot delete client with associated data",
                    details = new
                    {
                        hasAssessments,
                        hasEnvironments,
                        hasSubscriptions
                    },
                    suggestion = "Consider deactivating the client instead of deleting"
                });
            }

            // Soft delete - mark as inactive
            await _clientRepository.DeleteAsync(clientId);

            _logger.LogInformation("Client deleted: {ClientId} ({Name}) by {CustomerId}",
                clientId, client.Name, currentCustomerId);

            return Ok(new { message = "Client deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("{clientId}/access")]
    public async Task<ActionResult<ClientAccessDto>> GrantClientAccess(Guid clientId, [FromBody] GrantClientAccessRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var currentCustomerId = GetCurrentCustomerId();

            if (organizationId == null || currentCustomerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Check if client exists and user has access
            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            if (!CanManageClients())
            {
                return Forbid("You don't have permission to manage client access");
            }

            // Check if target user exists in organization
            var targetUser = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == request.CustomerId &&
                                         c.OrganizationId == organizationId.Value &&
                                         c.IsActive);

            if (targetUser == null)
            {
                return BadRequest("Target user not found in organization");
            }

            // Check if access already exists
            var existingAccess = await _clientRepository.GetUserClientAccessAsync(request.CustomerId, clientId);
            if (existingAccess != null)
            {
                return BadRequest("User already has access to this client");
            }

            var clientAccess = new ClientAccess
            {
                CustomerId = request.CustomerId,
                ClientId = clientId,
                AccessLevel = request.AccessLevel,
                CanViewAssessments = request.CanViewAssessments,
                CanCreateAssessments = request.CanCreateAssessments,
                CanDeleteAssessments = request.CanDeleteAssessments,
                CanManageEnvironments = request.CanManageEnvironments,
                CanViewReports = request.CanViewReports,
                CanExportData = request.CanExportData,
                GrantedByCustomerId = currentCustomerId.Value
            };

            var grantedAccess = await _clientRepository.GrantClientAccessAsync(clientAccess);

            _logger.LogInformation("Client access granted: User {UserId} to Client {ClientId} with level {AccessLevel} by {GranterId}",
                request.CustomerId, clientId, request.AccessLevel, currentCustomerId);

            var accessDto = new ClientAccessDto
            {
                CustomerId = grantedAccess.CustomerId,
                CustomerName = targetUser.FullName,
                CustomerEmail = targetUser.Email,
                AccessLevel = grantedAccess.AccessLevel,
                CanViewAssessments = grantedAccess.CanViewAssessments,
                CanCreateAssessments = grantedAccess.CanCreateAssessments,
                CanDeleteAssessments = grantedAccess.CanDeleteAssessments,
                CanManageEnvironments = grantedAccess.CanManageEnvironments,
                GrantedDate = grantedAccess.CreatedDate,
                GrantedBy = (await _context.Customers.FindAsync(currentCustomerId.Value))?.FullName
            };

            return Ok(accessDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error granting client access for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{clientId}/access/{customerId}")]
    public async Task<IActionResult> RevokeClientAccess(Guid clientId, Guid customerId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var currentCustomerId = GetCurrentCustomerId();

            if (organizationId == null || currentCustomerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Check permissions
            if (!CanManageClients())
            {
                return Forbid("You don't have permission to manage client access");
            }

            // Verify client exists
            var clientExists = await _clientRepository.ExistsAsync(clientId, organizationId.Value);
            if (!clientExists)
            {
                return NotFound("Client not found");
            }

            // Don't allow revoking access from self
            if (customerId == currentCustomerId)
            {
                return BadRequest("Cannot revoke your own access");
            }

            await _clientRepository.RevokeClientAccessAsync(customerId, clientId);

            _logger.LogInformation("Client access revoked: User {UserId} from Client {ClientId} by {RevokerId}",
                customerId, clientId, currentCustomerId);

            return Ok(new { message = "Client access revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking client access for client {ClientId}, user {CustomerId}", clientId, customerId);
            return StatusCode(500, "Internal server error");
        }
    }

    // Helper methods following your existing patterns
    private Guid? GetOrganizationIdFromContext()
    {
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        if (Guid.TryParse(orgIdClaim, out var organizationId))
        {
            return organizationId;
        }
        return null;
    }

    private Guid? GetCurrentCustomerId()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(customerIdClaim, out var customerId))
        {
            return customerId;
        }
        return null;
    }

    private string? GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    private bool CanManageClients()
    {
        var role = GetCurrentUserRole();
        return role == "Owner" || role == "Admin";
    }

    private async Task<bool> HasClientAccess(Guid customerId, Guid clientId)
    {
        // Organization owners and admins have access to all clients
        var role = GetCurrentUserRole();
        if (role == "Owner" || role == "Admin")
        {
            return true;
        }

        // Check specific client access
        var access = await _clientRepository.GetUserClientAccessAsync(customerId, clientId);
        return access != null && access.HasReadAccess;
    }

    private async Task<bool> HasClientManagementAccess(Guid customerId, Guid clientId)
    {
        // Organization owners and admins can manage all clients
        var role = GetCurrentUserRole();
        if (role == "Owner" || role == "Admin")
        {
            return true;
        }

        // Check specific client access
        var access = await _clientRepository.GetUserClientAccessAsync(customerId, clientId);
        return access != null && access.HasAdminAccess;
    }
}

// DTOs
public class ClientSummaryDto
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public int AssessmentCount { get; set; }
    public int EnvironmentCount { get; set; }
    public int SubscriptionCount { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool HasActiveContract { get; set; }
}

public class ClientDetailDto : ClientSummaryDto
{
    public string? Description { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? TimeZone { get; set; }
    public string? CreatedBy { get; set; }
    public int TotalAssessments { get; set; }
    public int CompletedAssessments { get; set; }
    public int TotalEnvironments { get; set; }
    public int TotalSubscriptions { get; set; }
    public List<AssessmentSummaryDto> RecentAssessments { get; set; } = new();
    public List<ClientAccessDto> TeamAccess { get; set; } = new();
}

public class AssessmentSummaryDto
{
    public Guid AssessmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? OverallScore { get; set; }
}

public class ClientAccessDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string AccessLevel { get; set; } = string.Empty;
    public bool CanViewAssessments { get; set; }
    public bool CanCreateAssessments { get; set; }
    public bool CanDeleteAssessments { get; set; }
    public bool CanManageEnvironments { get; set; }
    public bool CanViewReports { get; set; }
    public bool CanExportData { get; set; }
    public DateTime GrantedDate { get; set; }
    public string? GrantedBy { get; set; }
}

public class CreateClientRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(100)]
    public string? ContactName { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? ContactEmail { get; set; }

    [StringLength(20)]
    public string? ContactPhone { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(50)]
    public string? Country { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string? TimeZone { get; set; }

    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public string? Settings { get; set; }
}

public class UpdateClientRequest : CreateClientRequest
{
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Active";
}

public class GrantClientAccessRequest
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [StringLength(50)]
    public string AccessLevel { get; set; } = "Read";

    public bool CanViewAssessments { get; set; } = true;
    public bool CanCreateAssessments { get; set; } = false;
    public bool CanDeleteAssessments { get; set; } = false;
    public bool CanManageEnvironments { get; set; } = false;
    public bool CanViewReports { get; set; } = true;
    public bool CanExportData { get; set; } = false;
}