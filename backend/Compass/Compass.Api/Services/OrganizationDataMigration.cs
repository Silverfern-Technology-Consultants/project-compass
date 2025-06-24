using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Compass.Data;
using Compass.Data.Entities;

namespace Compass.Api.Services;

public class OrganizationDataMigrationService
{
    private readonly CompassDbContext _context;
    private readonly ILogger<OrganizationDataMigrationService> _logger;

    public OrganizationDataMigrationService(
        CompassDbContext context,
        ILogger<OrganizationDataMigrationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task MigrateExistingCustomersToOrganizations()
    {
        _logger.LogInformation("Starting organization data migration...");

        try
        {
            // Get all customers without organizations
            var customersWithoutOrgs = await _context.Customers
                .Where(c => c.OrganizationId == null)
                .ToListAsync();

            _logger.LogInformation($"Found {customersWithoutOrgs.Count} customers without organizations");

            foreach (var customer in customersWithoutOrgs)
            {
                // Create organization for each customer
                var organization = new Organization
                {
                    Name = customer.CompanyName,
                    Description = $"Organization for {customer.CompanyName}",
                    OwnerId = customer.CustomerId,
                    OrganizationType = "MSP",
                    IsTrialOrganization = customer.IsTrialAccount,
                    TrialStartDate = customer.TrialStartDate,
                    TrialEndDate = customer.TrialEndDate,
                    CreatedDate = customer.CreatedDate,
                    TimeZone = customer.TimeZone,
                    Country = customer.Country
                };

                _context.Organizations.Add(organization);
                await _context.SaveChangesAsync(); // Save to get OrganizationId

                // Update customer with organization and set as owner
                customer.OrganizationId = organization.OrganizationId;
                customer.Role = "Owner";

                _logger.LogInformation($"Created organization {organization.Name} for customer {customer.Email}");
            }

            await _context.SaveChangesAsync();

            // Update existing team invitations to use proper organization IDs
            await UpdateTeamInvitations();

            _logger.LogInformation("Organization data migration completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during organization data migration");
            throw;
        }
    }

    private async Task UpdateTeamInvitations()
    {
        var invitations = await _context.TeamInvitations
            .Include(ti => ti.InvitedBy)
            .ToListAsync();

        foreach (var invitation in invitations)
        {
            if (invitation.InvitedBy?.OrganizationId != null)
            {
                // Update OrganizationId to match the inviter's organization
                invitation.OrganizationId = invitation.InvitedBy.OrganizationId.Value;
                _logger.LogInformation($"Updated invitation {invitation.InvitationId} to organization {invitation.OrganizationId}");
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsDataMigrationNeeded()
    {
        var customersWithoutOrgs = await _context.Customers
            .CountAsync(c => c.OrganizationId == null);

        return customersWithoutOrgs > 0;
    }

    public async Task<OrganizationMigrationStatus> GetMigrationStatus()
    {
        var totalCustomers = await _context.Customers.CountAsync();
        var customersWithOrgs = await _context.Customers.CountAsync(c => c.OrganizationId != null);
        var totalOrganizations = await _context.Organizations.CountAsync();

        return new OrganizationMigrationStatus
        {
            TotalCustomers = totalCustomers,
            CustomersWithOrganizations = customersWithOrgs,
            CustomersWithoutOrganizations = totalCustomers - customersWithOrgs,
            TotalOrganizations = totalOrganizations,
            MigrationNeeded = customersWithOrgs < totalCustomers
        };
    }
}

public class OrganizationMigrationStatus
{
    public int TotalCustomers { get; set; }
    public int CustomersWithOrganizations { get; set; }
    public int CustomersWithoutOrganizations { get; set; }
    public int TotalOrganizations { get; set; }
    public bool MigrationNeeded { get; set; }
}