using Compass.Core.Models;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface IClientService
{
    Task<List<ClientSelectionDto>> GetAccessibleClientsAsync(Guid customerId);
    Task<ClientContext> GetClientContextAsync(Guid? clientId, Guid organizationId);
    Task<bool> ValidateClientAccessAsync(Guid customerId, Guid clientId);
    Task<ClientStatsDto> GetClientStatsAsync(Guid organizationId);
    Task<List<ClientActivityDto>> GetClientActivityAsync(Guid organizationId, int page = 1, int pageSize = 50);
    Task<bool> CanUserAccessClient(Guid customerId, Guid clientId, string requiredPermission = "Read");
    Task<Client?> GetClientByIdAsync(Guid clientId, Guid organizationId);
}
