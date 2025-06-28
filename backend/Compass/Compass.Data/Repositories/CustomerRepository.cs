// Compass.Data/Repositories/CustomerRepository.cs
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly CompassDbContext _context;

    public CustomerRepository(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(Guid customerId)
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .Include(c => c.Assessments)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant());
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        customer.CreatedDate = DateTime.UtcNow;
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task<Customer> UpdateAsync(Customer customer)
    {
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task DeleteAsync(Guid customerId)
    {
        var customer = await GetByIdAsync(customerId);
        if (customer != null)
        {
            customer.IsActive = false; // Soft delete
            await UpdateAsync(customer);
        }
    }

    public async Task<bool> ExistsAsync(Guid customerId)
    {
        return await _context.Customers
            .AnyAsync(c => c.CustomerId == customerId && c.IsActive);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Customers
            .AnyAsync(c => c.Email == email.ToLowerInvariant() && c.IsActive);
    }

    // NEW: Enhanced account management methods

    public async Task<bool> DeactivateAccountAsync(Guid customerId, string reason = "")
    {
        var customer = await GetByIdAsync(customerId);
        if (customer == null) return false;

        customer.IsActive = false;
        customer.OrganizationId = null; // Remove from organization
        customer.Role = "Owner"; // Reset role

        // Add audit fields if they exist
        if (customer.GetType().GetProperty("DeactivatedDate") != null)
        {
            customer.GetType().GetProperty("DeactivatedDate")?.SetValue(customer, DateTime.UtcNow);
        }
        if (customer.GetType().GetProperty("DeactivationReason") != null)
        {
            customer.GetType().GetProperty("DeactivationReason")?.SetValue(customer, reason);
        }

        await UpdateAsync(customer);
        return true;
    }

    public async Task<bool> ReactivateAccountAsync(Guid customerId)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null) return false;

        customer.IsActive = true;

        // Clear deactivation fields if they exist
        if (customer.GetType().GetProperty("DeactivatedDate") != null)
        {
            customer.GetType().GetProperty("DeactivatedDate")?.SetValue(customer, null);
        }
        if (customer.GetType().GetProperty("DeactivationReason") != null)
        {
            customer.GetType().GetProperty("DeactivationReason")?.SetValue(customer, null);
        }

        await UpdateAsync(customer);
        return true;
    }

    public async Task<bool> RemoveFromOrganizationAsync(Guid customerId)
    {
        var customer = await GetByIdAsync(customerId);
        if (customer == null) return false;

        customer.OrganizationId = null;
        customer.Role = "Owner"; // Reset to default
        await UpdateAsync(customer);
        return true;
    }

    public async Task<bool> HasOtherOrganizationTiesAsync(string email, Guid? excludeOrganizationId = null)
    {
        // Check if user has other active accounts in different organizations
        var hasOtherAccounts = await _context.Customers
            .AnyAsync(c => c.Email == email.ToLowerInvariant() &&
                          c.IsActive &&
                          c.OrganizationId != null &&
                          (excludeOrganizationId == null || c.OrganizationId != excludeOrganizationId));

        // Check if user has pending invitations to other organizations
        var hasPendingInvitations = await _context.TeamInvitations
            .AnyAsync(ti => ti.InvitedEmail == email.ToLowerInvariant() &&
                           ti.Status == "Pending" &&
                           (excludeOrganizationId == null || ti.OrganizationId != excludeOrganizationId));

        return hasOtherAccounts || hasPendingInvitations;
    }

    public async Task<bool> HasCreatedContentAsync(Guid customerId)
    {
        // Check if user has created assessments
        var hasAssessments = await _context.Assessments
            .AnyAsync(a => a.CustomerId == customerId);

        // Check if user has created Azure environments
        var hasAzureEnvironments = await _context.AzureEnvironments
            .AnyAsync(ae => ae.CustomerId == customerId);

        // Check if user has active subscriptions
        var hasSubscriptions = await _context.Subscriptions
            .AnyAsync(s => s.CustomerId == customerId && s.Status == "Active");

        return hasAssessments || hasAzureEnvironments || hasSubscriptions;
    }

    public async Task<bool> TransferOwnershipAsync(Guid fromCustomerId, Guid toCustomerId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Transfer assessments
            var assessments = await _context.Assessments
                .Where(a => a.CustomerId == fromCustomerId)
                .ToListAsync();

            foreach (var assessment in assessments)
            {
                assessment.CustomerId = toCustomerId;
            }

            if (assessments.Any())
            {
                _context.Assessments.UpdateRange(assessments);
            }

            // Transfer Azure environments
            var azureEnvironments = await _context.AzureEnvironments
                .Where(ae => ae.CustomerId == fromCustomerId)
                .ToListAsync();

            foreach (var env in azureEnvironments)
            {
                env.CustomerId = toCustomerId;
            }

            if (azureEnvironments.Any())
            {
                _context.AzureEnvironments.UpdateRange(azureEnvironments);
            }

            // Note: Subscriptions might need special handling based on business rules
            // For now, we'll leave them with the original owner but mark them for review

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<CustomerAccountInfo?> GetAccountInfoAsync(Guid customerId)
    {
        var customer = await _context.Customers
            .Include(c => c.Organization)
            .Include(c => c.Assessments)
            .Include(c => c.AzureEnvironments)
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null) return null; // This fixes the CS8603 warning

        return new CustomerAccountInfo
        {
            // ... existing mapping
        };
    }

    public async Task<List<Customer>> GetOrganizationMembersAsync(Guid organizationId)
    {
        return await _context.Customers
            .Where(c => c.OrganizationId == organizationId && c.IsActive)
            .Include(c => c.Assessments)
            .Include(c => c.AzureEnvironments)
            .OrderBy(c => c.Role == "Owner" ? 0 : c.Role == "Admin" ? 1 : 2)
            .ThenBy(c => c.FirstName)
            .ToListAsync();
    }

    public async Task<bool> CanUserBeRemovedAsync(Guid customerId, Guid organizationId)
    {
        var customer = await GetByIdAsync(customerId);
        if (customer == null) return false;

        // Can't remove organization owner
        if (customer.Role == "Owner") return false;

        // Can't remove if they're the only admin and there are other members
        if (customer.Role == "Admin")
        {
            var memberCount = await _context.Customers
                .CountAsync(c => c.OrganizationId == organizationId && c.IsActive);

            var adminCount = await _context.Customers
                .CountAsync(c => c.OrganizationId == organizationId &&
                               c.IsActive &&
                               (c.Role == "Admin" || c.Role == "Owner"));

            // If there are multiple members but only one admin (and this is it), don't allow removal
            if (memberCount > 1 && adminCount == 1)
            {
                return false;
            }
        }

        return true;
    }
}

// NEW: Account info DTO for detailed account status
public class CustomerAccountInfo
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastLoginDate { get; set; }
    public int AssessmentCount { get; set; }
    public int AzureEnvironmentCount { get; set; }
    public int ActiveSubscriptionCount { get; set; }
    public bool HasCreatedContent { get; set; }
}