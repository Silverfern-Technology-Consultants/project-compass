// Compass.Data/Repositories/UsageMetricRepository.cs
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class UsageMetricRepository : IUsageMetricRepository
{
    private readonly CompassDbContext _context;

    public UsageMetricRepository(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<UsageMetric> GetByIdAsync(Guid usageId)
    {
        return await _context.UsageMetrics
            .Include(u => u.Customer)
            .Include(u => u.Subscription)
            .FirstOrDefaultAsync(u => u.UsageId == usageId);
    }

    public async Task<IEnumerable<UsageMetric>> GetByCustomerIdAsync(Guid customerId)
    {
        return await _context.UsageMetrics
            .Include(u => u.Subscription)
            .Where(u => u.CustomerId == customerId)
            .OrderByDescending(u => u.RecordedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<UsageMetric>> GetByCustomerAndPeriodAsync(Guid customerId, string billingPeriod)
    {
        return await _context.UsageMetrics
            .Include(u => u.Subscription)
            .Where(u => u.CustomerId == customerId && u.BillingPeriod == billingPeriod)
            .OrderByDescending(u => u.RecordedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<UsageMetric>> GetBySubscriptionIdAsync(Guid subscriptionId)
    {
        return await _context.UsageMetrics
            .Include(u => u.Customer)
            .Where(u => u.SubscriptionId == subscriptionId)
            .OrderByDescending(u => u.RecordedDate)
            .ToListAsync();
    }

    public async Task<UsageMetric> CreateAsync(UsageMetric usageMetric)
    {
        usageMetric.RecordedDate = DateTime.UtcNow;

        _context.UsageMetrics.Add(usageMetric);
        await _context.SaveChangesAsync();
        return usageMetric;
    }

    public async Task<UsageMetric> UpdateAsync(UsageMetric usageMetric)
    {
        _context.UsageMetrics.Update(usageMetric);
        await _context.SaveChangesAsync();
        return usageMetric;
    }

    public async Task DeleteAsync(Guid usageId)
    {
        var usageMetric = await GetByIdAsync(usageId);
        if (usageMetric != null)
        {
            _context.UsageMetrics.Remove(usageMetric);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<string, int>> GetUsageSummaryAsync(Guid customerId, string billingPeriod)
    {
        return await _context.UsageMetrics
            .Where(u => u.CustomerId == customerId && u.BillingPeriod == billingPeriod)
            .GroupBy(u => u.MetricType)
            .Select(g => new { MetricType = g.Key, Total = g.Sum(u => u.MetricValue) })
            .ToDictionaryAsync(x => x.MetricType, x => x.Total);
    }

    public async Task<IEnumerable<UsageMetric>> GetByMetricTypeAsync(string metricType, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.UsageMetrics
            .Include(u => u.Customer)
            .Include(u => u.Subscription)
            .Where(u => u.MetricType == metricType);

        if (startDate.HasValue)
            query = query.Where(u => u.RecordedDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(u => u.RecordedDate <= endDate.Value);

        return await query
            .OrderByDescending(u => u.RecordedDate)
            .ToListAsync();
    }
}