using Compass.Data.Entities;

namespace Compass.Data.Repositories;

public interface IClientPreferencesRepository
{
    // Basic CRUD operations
    Task<ClientPreferences?> GetByIdAsync(Guid clientPreferencesId);
    Task<ClientPreferences?> GetByClientIdAsync(Guid clientId, Guid organizationId);
    Task<ClientPreferences> CreateAsync(ClientPreferences clientPreferences);
    Task<ClientPreferences> UpdateAsync(ClientPreferences clientPreferences);
    Task DeleteAsync(Guid clientPreferencesId);

    // Organization-scoped operations
    Task<IEnumerable<ClientPreferences>> GetByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<ClientPreferences>> GetActiveByOrganizationIdAsync(Guid organizationId);

    // Existence checks
    Task<bool> ExistsForClientAsync(Guid clientId, Guid organizationId);
    Task<bool> HasActivePreferencesAsync(Guid clientId, Guid organizationId);
}