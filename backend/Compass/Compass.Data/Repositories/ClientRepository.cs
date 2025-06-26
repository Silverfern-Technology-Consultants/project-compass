using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly CompassDbContext _context;

    public ClientRepository(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<Client?> GetByIdAsync(Guid clientId)
    {
        return await _context.Clients
            .Include(c => c.Organization)
            .Include(c => c.CreatedBy)
            .Include(c => c.LastModifiedBy)
            .Include(c => c.ClientAccess)
                .ThenInclude(ca => ca.Customer)
            .FirstOrDefaultAsync(c => c.ClientId == clientId);
    }

    public async Task<Client?> GetByIdAndOrganizationAsync(Guid clientId, Guid organizationId)
    {
        return await _context.Clients
            .Include(c => c.Organization)
            .Include(c => c.CreatedBy)
            .Include(c => c.LastModifiedBy)
            .Include(c => c.ClientAccess)
                .ThenInclude(ca => ca.Customer)
            .Where(c => c.ClientId == clientId && c.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
    }

    public async Task<Client> CreateAsync(Client client)
    {
        client.CreatedDate = DateTime.UtcNow;
        client.LastModifiedDate = DateTime.UtcNow;

        _context.Clients.Add(client);
        await _context.SaveChangesAsync();
        return client;
    }

    public async Task<Client> UpdateAsync(Client client)
    {
        client.LastModifiedDate = DateTime.UtcNow;

        _context.Clients.Update(client);
        await _context.SaveChangesAsync();
        return client;
    }

    public async Task DeleteAsync(Guid clientId)
    {
        var client = await GetByIdAsync(clientId);
        if (client != null)
        {
            // Soft delete by marking as inactive
            client.IsActive = false;
            client.Status = "Deleted";
            client.LastModifiedDate = DateTime.UtcNow;

            await UpdateAsync(client);
        }
    }

    public async Task<IEnumerable<Client>> GetByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Clients
            .Include(c => c.Organization)
            .Include(c => c.CreatedBy)
            .Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Client>> GetActiveByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Clients
            .Include(c => c.Organization)
            .Include(c => c.CreatedBy)
            .Where(c => c.OrganizationId == organizationId && c.IsActive && c.Status == "Active")
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Client>> GetClientsByUserAccessAsync(Guid customerId)
    {
        return await _context.ClientAccess
            .Include(ca => ca.Client)
                .ThenInclude(c => c.Organization)
            .Where(ca => ca.CustomerId == customerId && ca.Client.IsActive)
            .Select(ca => ca.Client)
            .Distinct()
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ClientAccess?> GetUserClientAccessAsync(Guid customerId, Guid clientId)
    {
        return await _context.ClientAccess
            .Include(ca => ca.Client)
            .Include(ca => ca.Customer)
            .FirstOrDefaultAsync(ca => ca.CustomerId == customerId && ca.ClientId == clientId);
    }

    public async Task<IEnumerable<ClientAccess>> GetClientAccessUsersAsync(Guid clientId)
    {
        return await _context.ClientAccess
            .Include(ca => ca.Customer)
            .Include(ca => ca.GrantedBy)
            .Where(ca => ca.ClientId == clientId)
            .OrderBy(ca => ca.Customer.FirstName)
            .ThenBy(ca => ca.Customer.LastName)
            .ToListAsync();
    }

    public async Task<ClientAccess> GrantClientAccessAsync(ClientAccess clientAccess)
    {
        clientAccess.CreatedDate = DateTime.UtcNow;
        clientAccess.LastModifiedDate = DateTime.UtcNow;

        _context.ClientAccess.Add(clientAccess);
        await _context.SaveChangesAsync();
        return clientAccess;
    }

    public async Task<ClientAccess> UpdateClientAccessAsync(ClientAccess clientAccess)
    {
        clientAccess.LastModifiedDate = DateTime.UtcNow;

        _context.ClientAccess.Update(clientAccess);
        await _context.SaveChangesAsync();
        return clientAccess;
    }

    public async Task RevokeClientAccessAsync(Guid customerId, Guid clientId)
    {
        var access = await GetUserClientAccessAsync(customerId, clientId);
        if (access != null)
        {
            _context.ClientAccess.Remove(access);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Client>> SearchClientsAsync(Guid organizationId, string searchTerm)
    {
        var query = _context.Clients
            .Include(c => c.Organization)
            .Where(c => c.OrganizationId == organizationId && c.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(searchLower) ||
                (c.ContactName != null && c.ContactName.ToLower().Contains(searchLower)) ||
                (c.ContactEmail != null && c.ContactEmail.ToLower().Contains(searchLower)) ||
                (c.Industry != null && c.Industry.ToLower().Contains(searchLower))
            );
        }

        return await query
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(Guid clientId, Guid organizationId)
    {
        return await _context.Clients
            .AnyAsync(c => c.ClientId == clientId && c.OrganizationId == organizationId && c.IsActive);
    }

    public async Task<bool> IsClientNameUniqueAsync(string name, Guid organizationId, Guid? excludeClientId = null)
    {
        var query = _context.Clients
            .Where(c => c.OrganizationId == organizationId &&
                       c.Name.ToLower() == name.ToLower() &&
                       c.IsActive);

        if (excludeClientId.HasValue)
        {
            query = query.Where(c => c.ClientId != excludeClientId.Value);
        }

        return !await query.AnyAsync();
    }
}