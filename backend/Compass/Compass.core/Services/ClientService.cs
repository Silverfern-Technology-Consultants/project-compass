using Compass.core.Interfaces;
using Compass.Core.Models;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public class ClientService : IClientService
{
    private readonly CompassDbContext _context;
    private readonly IClientRepository _clientRepository;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        CompassDbContext context,
        IClientRepository clientRepository,
        ILogger<ClientService> logger)
    {
        _context = context;
        _clientRepository = clientRepository;
        _logger = logger;
    }

    public async Task<List<ClientSelectionDto>> GetAccessibleClientsAsync(Guid customerId)
    {
        try
        {
            // Get user's role and organization
            var customer = await _context.Customers
                .Include(c => c.Organization)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer?.Organization == null)
            {
                _logger.LogWarning("Customer {CustomerId} not found or has no organization", customerId);
                return new List<ClientSelectionDto>();
            }

            List<Client> accessibleClients;

            // Organization owners and admins have access to all clients
            if (customer.Role == "Owner" || customer.Role == "Admin")
            {
                accessibleClients = (await _clientRepository.GetActiveByOrganizationIdAsync(customer.OrganizationId!.Value)).ToList();
            }
            else
            {
                // Get clients user has explicit access to
                accessibleClients = (await _clientRepository.GetClientsByUserAccessAsync(customerId)).ToList();
            }

            var clientSelections = new List<ClientSelectionDto>();

            foreach (var client in accessibleClients)
            {
                var assessmentCount = await _context.Assessments
                    .CountAsync(a => a.ClientId == client.ClientId);

                clientSelections.Add(new ClientSelectionDto
                {
                    ClientId = client.ClientId,
                    Name = client.Name,
                    Industry = client.Industry,
                    IsActive = client.IsActive && client.Status == "Active",
                    HasActiveContract = client.HasActiveContract,
                    AssessmentCount = assessmentCount
                });
            }

            _logger.LogInformation("Retrieved {ClientCount} accessible clients for customer {CustomerId}",
                clientSelections.Count, customerId);

            return clientSelections.OrderBy(c => c.Name).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving accessible clients for customer {CustomerId}", customerId);
            return new List<ClientSelectionDto>();
        }
    }

    public async Task<ClientContext> GetClientContextAsync(Guid? clientId, Guid organizationId)
    {
        try
        {
            if (!clientId.HasValue)
            {
                return new ClientContext
                {
                    ClientId = null,
                    ClientName = null,
                    IsMultiClient = false
                };
            }

            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId.Value, organizationId);
            if (client == null)
            {
                _logger.LogWarning("Client {ClientId} not found in organization {OrganizationId}", clientId, organizationId);
                return new ClientContext
                {
                    ClientId = null,
                    ClientName = null,
                    IsMultiClient = false
                };
            }

            return new ClientContext
            {
                ClientId = client.ClientId,
                ClientName = client.Name,
                IsMultiClient = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client context for client {ClientId}", clientId);
            return new ClientContext
            {
                ClientId = null,
                ClientName = null,
                IsMultiClient = false
            };
        }
    }

    public async Task<bool> ValidateClientAccessAsync(Guid customerId, Guid clientId)
    {
        try
        {
            // Get user's role
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null)
            {
                return false;
            }

            // Organization owners and admins have access to all clients
            if (customer.Role == "Owner" || customer.Role == "Admin")
            {
                // Verify client exists in same organization
                return await _clientRepository.ExistsAsync(clientId, customer.OrganizationId!.Value);
            }

            // Check specific client access
            var access = await _clientRepository.GetUserClientAccessAsync(customerId, clientId);
            return access != null && access.HasReadAccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating client access for customer {CustomerId}, client {ClientId}", customerId, clientId);
            return false;
        }
    }

    public async Task<ClientStatsDto> GetClientStatsAsync(Guid organizationId)
    {
        try
        {
            var clients = await _clientRepository.GetByOrganizationIdAsync(organizationId);
            var clientList = clients.ToList();

            var totalAssessments = await _context.Assessments
                .Where(a => a.OrganizationId == organizationId && a.ClientId != null)
                .CountAsync();

            var totalEnvironments = await _context.AzureEnvironments
                .Where(e => e.ClientId != null)
                .Join(_context.Clients.Where(c => c.OrganizationId == organizationId),
                      e => e.ClientId,
                      c => c.ClientId,
                      (e, c) => e)
                .CountAsync();

            var clientsByIndustry = clientList
                .Where(c => !string.IsNullOrEmpty(c.Industry))
                .GroupBy(c => c.Industry!)
                .ToDictionary(g => g.Key, g => g.Count());

            var clientsByStatus = clientList
                .GroupBy(c => c.Status)
                .ToDictionary(g => g.Key, g => g.Count());

            return new ClientStatsDto
            {
                TotalClients = clientList.Count,
                ActiveClients = clientList.Count(c => c.IsActive && c.Status == "Active"),
                InactiveClients = clientList.Count(c => !c.IsActive || c.Status != "Active"),
                TotalAssessments = totalAssessments,
                TotalEnvironments = totalEnvironments,
                ClientsByIndustry = clientsByIndustry,
                ClientsByStatus = clientsByStatus
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client stats for organization {OrganizationId}", organizationId);
            return new ClientStatsDto();
        }
    }

    public async Task<List<ClientActivityDto>> GetClientActivityAsync(Guid organizationId, int page = 1, int pageSize = 50)
    {
        try
        {
            var skip = (page - 1) * pageSize;
            var activities = new List<ClientActivityDto>();

            // Get recent assessments for all clients in organization
            var recentAssessments = await _context.Assessments
                .Where(a => a.OrganizationId == organizationId && a.ClientId != null)
                .Include(a => a.Customer)
                .Include(a => a.Client)
                .OrderByDescending(a => a.StartedDate)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            foreach (var assessment in recentAssessments)
            {
                activities.Add(new ClientActivityDto
                {
                    ActivityId = assessment.Id,
                    ClientId = assessment.ClientId!.Value,
                    ClientName = assessment.Client?.Name ?? "Unknown Client",
                    ActivityType = "assessment_created",
                    Description = $"Assessment '{assessment.Name}' created",
                    PerformedBy = assessment.Customer?.FullName ?? "Unknown User",
                    ActivityDate = assessment.StartedDate,
                    Metadata = new
                    {
                        assessmentName = assessment.Name,
                        assessmentType = assessment.AssessmentType,
                        status = assessment.Status
                    }
                });
            }

            return activities.OrderByDescending(a => a.ActivityDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client activity for organization {OrganizationId}", organizationId);
            return new List<ClientActivityDto>();
        }
    }

    public async Task<bool> CanUserAccessClient(Guid customerId, Guid clientId, string requiredPermission = "Read")
    {
        try
        {
            // Get user's role
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null)
            {
                return false;
            }

            // Organization owners and admins have full access
            if (customer.Role == "Owner" || customer.Role == "Admin")
            {
                return true;
            }

            // Check specific client access
            var access = await _clientRepository.GetUserClientAccessAsync(customerId, clientId);
            if (access == null)
            {
                return false;
            }

            return requiredPermission.ToLower() switch
            {
                "read" => access.HasReadAccess,
                "write" => access.HasWriteAccess,
                "admin" => access.HasAdminAccess,
                "viewassessments" => access.CanViewAssessments,
                "createassessments" => access.CanCreateAssessments,
                "deleteassessments" => access.CanDeleteAssessments,
                "manageenvironments" => access.CanManageEnvironments,
                "viewreports" => access.CanViewReports,
                "exportdata" => access.CanExportData,
                _ => access.HasReadAccess
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking user {CustomerId} access to client {ClientId} for permission {Permission}",
                customerId, clientId, requiredPermission);
            return false;
        }
    }

    public async Task<Client?> GetClientByIdAsync(Guid clientId, Guid organizationId)
    {
        try
        {
            return await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting client {ClientId} for organization {OrganizationId}", clientId, organizationId);
            return null;
        }
    }
}