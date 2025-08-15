using Compass.Data.Entities;

namespace Compass.Data.Interfaces;

public interface IClientRepository
{
    // Basic CRUD operations
    Task<Client?> GetByIdAsync(Guid clientId);
    Task<Client?> GetByIdAndOrganizationAsync(Guid clientId, Guid organizationId);
    Task<Client> CreateAsync(Client client);
    Task<Client> UpdateAsync(Client client);
    Task DeleteAsync(Guid clientId);

    // Organization-scoped operations
    Task<IEnumerable<Client>> GetByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<Client>> GetActiveByOrganizationIdAsync(Guid organizationId);

    // User access operations
    Task<IEnumerable<Client>> GetClientsByUserAccessAsync(Guid customerId);
    Task<ClientAccess?> GetUserClientAccessAsync(Guid customerId, Guid clientId);
    Task<IEnumerable<ClientAccess>> GetClientAccessUsersAsync(Guid clientId);

    // Client access management
    Task<ClientAccess> GrantClientAccessAsync(ClientAccess clientAccess);
    Task<ClientAccess> UpdateClientAccessAsync(ClientAccess clientAccess);
    Task RevokeClientAccessAsync(Guid customerId, Guid clientId);

    // Search and filtering
    Task<IEnumerable<Client>> SearchClientsAsync(Guid organizationId, string searchTerm);
    Task<bool> ExistsAsync(Guid clientId, Guid organizationId);
    Task<bool> IsClientNameUniqueAsync(string name, Guid organizationId, Guid? excludeClientId = null);

    // Azure Environment operations
    Task<IEnumerable<AzureEnvironment>> GetClientAzureEnvironmentsAsync(Guid clientId, Guid organizationId);
    Task<Client?> GetClientByIdAsync(Guid clientId, Guid organizationId);
    Task<AzureEnvironment?> GetAzureEnvironmentByIdAsync(Guid azureEnvironmentId, Guid organizationId);
    Task<AzureEnvironment> UpdateAzureEnvironmentAsync(AzureEnvironment azureEnvironment);
}