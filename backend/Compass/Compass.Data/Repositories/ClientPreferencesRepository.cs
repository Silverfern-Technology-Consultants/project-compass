using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Compass.Data.Repositories;

public class ClientPreferencesRepository : IClientPreferencesRepository
{
    private readonly CompassDbContext _context;
    private readonly ILogger<ClientPreferencesRepository> _logger;

    public ClientPreferencesRepository(CompassDbContext context, ILogger<ClientPreferencesRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ClientPreferences?> GetByIdAsync(Guid clientPreferencesId)
    {
        try
        {
            _logger.LogInformation("Retrieving client preferences by ID: {ClientPreferencesId}", clientPreferencesId);

            return await _context.ClientPreferences
                .Include(cp => cp.Client)
                .Include(cp => cp.Organization)
                .Include(cp => cp.CreatedBy)
                .Include(cp => cp.LastModifiedBy)
                .FirstOrDefaultAsync(cp => cp.ClientPreferencesId == clientPreferencesId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client preferences by ID: {ClientPreferencesId}", clientPreferencesId);
            throw;
        }
    }

    public async Task<ClientPreferences?> GetByClientIdAsync(Guid clientId, Guid organizationId)
    {
        try
        {
            _logger.LogInformation("Retrieving client preferences for client {ClientId} in organization {OrganizationId}",
                clientId, organizationId);

            return await _context.ClientPreferences
                .Include(cp => cp.Client)
                .Include(cp => cp.Organization)
                .Include(cp => cp.CreatedBy)
                .Include(cp => cp.LastModifiedBy)
                .FirstOrDefaultAsync(cp => cp.ClientId == clientId && cp.OrganizationId == organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client preferences for client {ClientId} in organization {OrganizationId}",
                clientId, organizationId);
            throw;
        }
    }

    public async Task<ClientPreferences> CreateAsync(ClientPreferences clientPreferences)
    {
        try
        {
            _logger.LogInformation("Creating client preferences for client {ClientId} in organization {OrganizationId}",
                clientPreferences.ClientId, clientPreferences.OrganizationId);

            clientPreferences.CreatedDate = DateTime.UtcNow;
            clientPreferences.IsActive = true;

            _context.ClientPreferences.Add(clientPreferences);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created client preferences with ID: {ClientPreferencesId}",
                clientPreferences.ClientPreferencesId);

            return clientPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create client preferences for client {ClientId}", clientPreferences.ClientId);
            throw;
        }
    }

    public async Task<ClientPreferences> UpdateAsync(ClientPreferences clientPreferences)
    {
        try
        {
            _logger.LogInformation("Updating client preferences with ID: {ClientPreferencesId}",
                clientPreferences.ClientPreferencesId);

            var existingPreferences = await _context.ClientPreferences
                .FirstOrDefaultAsync(cp => cp.ClientPreferencesId == clientPreferences.ClientPreferencesId);

            if (existingPreferences == null)
            {
                throw new InvalidOperationException($"Client preferences with ID {clientPreferences.ClientPreferencesId} not found");
            }

            // Update properties
            existingPreferences.AllowedNamingPatterns = clientPreferences.AllowedNamingPatterns;
            existingPreferences.RequiredNamingElements = clientPreferences.RequiredNamingElements;
            existingPreferences.EnvironmentIndicators = clientPreferences.EnvironmentIndicators;
            existingPreferences.RequiredTags = clientPreferences.RequiredTags;
            existingPreferences.EnforceTagCompliance = clientPreferences.EnforceTagCompliance;
            existingPreferences.ComplianceFrameworks = clientPreferences.ComplianceFrameworks;
            existingPreferences.LastModifiedDate = DateTime.UtcNow;
            existingPreferences.LastModifiedByCustomerId = clientPreferences.LastModifiedByCustomerId;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully updated client preferences with ID: {ClientPreferencesId}",
                clientPreferences.ClientPreferencesId);

            return existingPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update client preferences with ID: {ClientPreferencesId}",
                clientPreferences.ClientPreferencesId);
            throw;
        }
    }

    public async Task DeleteAsync(Guid clientPreferencesId)
    {
        try
        {
            _logger.LogInformation("Deleting client preferences with ID: {ClientPreferencesId}", clientPreferencesId);

            var clientPreferences = await _context.ClientPreferences
                .FirstOrDefaultAsync(cp => cp.ClientPreferencesId == clientPreferencesId);

            if (clientPreferences == null)
            {
                _logger.LogWarning("Client preferences with ID {ClientPreferencesId} not found for deletion", clientPreferencesId);
                return;
            }

            _context.ClientPreferences.Remove(clientPreferences);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted client preferences with ID: {ClientPreferencesId}", clientPreferencesId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete client preferences with ID: {ClientPreferencesId}", clientPreferencesId);
            throw;
        }
    }

    public async Task<IEnumerable<ClientPreferences>> GetByOrganizationIdAsync(Guid organizationId)
    {
        try
        {
            _logger.LogInformation("Retrieving all client preferences for organization {OrganizationId}", organizationId);

            return await _context.ClientPreferences
                .Include(cp => cp.Client)
                .Include(cp => cp.Organization)
                .Where(cp => cp.OrganizationId == organizationId)
                .OrderBy(cp => cp.Client.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client preferences for organization {OrganizationId}", organizationId);
            throw;
        }
    }

    public async Task<IEnumerable<ClientPreferences>> GetActiveByOrganizationIdAsync(Guid organizationId)
    {
        try
        {
            _logger.LogInformation("Retrieving active client preferences for organization {OrganizationId}", organizationId);

            return await _context.ClientPreferences
                .Include(cp => cp.Client)
                .Include(cp => cp.Organization)
                .Where(cp => cp.OrganizationId == organizationId && cp.IsActive)
                .OrderBy(cp => cp.Client.Name)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve active client preferences for organization {OrganizationId}", organizationId);
            throw;
        }
    }

    public async Task<bool> ExistsForClientAsync(Guid clientId, Guid organizationId)
    {
        try
        {
            return await _context.ClientPreferences
                .AnyAsync(cp => cp.ClientId == clientId && cp.OrganizationId == organizationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if client preferences exist for client {ClientId} in organization {OrganizationId}",
                clientId, organizationId);
            throw;
        }
    }

    public async Task<bool> HasActivePreferencesAsync(Guid clientId, Guid organizationId)
    {
        try
        {
            return await _context.ClientPreferences
                .AnyAsync(cp => cp.ClientId == clientId && cp.OrganizationId == organizationId && cp.IsActive);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if active client preferences exist for client {ClientId} in organization {OrganizationId}",
                clientId, organizationId);
            throw;
        }
    }
}