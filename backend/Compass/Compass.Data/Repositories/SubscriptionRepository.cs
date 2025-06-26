// Compass.Data/Repositories/SubscriptionRepository.cs
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly CompassDbContext _context;

    public SubscriptionRepository(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<Subscription> GetByIdAsync(Guid subscriptionId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Include(s => s.UsageMetrics)
            .Include(s => s.Invoices)
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
    }

    public async Task<Subscription> GetActiveByCustomerIdAsync(Guid customerId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Where(s => s.CustomerId == customerId)
            .Where(s => s.Status == "Active" || s.Status == "Trial")
            .Where(s => s.EndDate == null || s.EndDate > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task<Subscription?> GetActiveByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Where(s => s.Customer.OrganizationId == organizationId)
            .Where(s => s.Status == "Active" || s.Status == "Trial")
            .Where(s => s.EndDate == null || s.EndDate > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Subscription>> GetByCustomerIdAsync(Guid customerId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Subscription>> GetByOrganizationIdAsync(Guid organizationId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Where(s => s.Customer.OrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
    }

    public async Task<Subscription> CreateAsync(Subscription subscription)
    {
        subscription.CreatedDate = DateTime.UtcNow;
        subscription.ModifiedDate = DateTime.UtcNow;

        _context.Subscriptions.Add(subscription);
        await _context.SaveChangesAsync();
        return subscription;
    }

    public async Task<Subscription> UpdateAsync(Subscription subscription)
    {
        subscription.ModifiedDate = DateTime.UtcNow;

        _context.Subscriptions.Update(subscription);
        await _context.SaveChangesAsync();
        return subscription;
    }

    public async Task DeleteAsync(Guid subscriptionId)
    {
        var subscription = await GetByIdAsync(subscriptionId);
        if (subscription != null)
        {
            _context.Subscriptions.Remove(subscription);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Subscription>> GetExpiringSubscriptionsAsync(DateTime date)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Where(s => s.Status == "Active")
            .Where(s => s.NextBillingDate.HasValue && s.NextBillingDate.Value.Date == date.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Subscription>> GetByStatusAsync(string status)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
                .ThenInclude(c => c.Organization)
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync();
    }
}