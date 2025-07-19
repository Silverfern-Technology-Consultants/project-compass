// Compass.Data/Repositories/IUsageMetricRepository.cs

// Compass.Data/Repositories/IUsageMetricRepository.cs
using Compass.Data.Entities;

namespace Compass.Data.Interfaces;

public interface IUsageMetricRepository
{
    Task<UsageMetric?> GetByIdAsync(Guid usageId);
    Task<IEnumerable<UsageMetric>> GetByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<UsageMetric>> GetByCustomerAndPeriodAsync(Guid customerId, string billingPeriod);
    Task<IEnumerable<UsageMetric>> GetBySubscriptionIdAsync(Guid subscriptionId);
    Task<UsageMetric> CreateAsync(UsageMetric usageMetric);
    Task<UsageMetric> UpdateAsync(UsageMetric usageMetric);
    Task DeleteAsync(Guid usageId);
    Task<Dictionary<string, int>> GetUsageSummaryAsync(Guid customerId, string billingPeriod);
    Task<IEnumerable<UsageMetric>> GetByMetricTypeAsync(string metricType, DateTime? startDate = null, DateTime? endDate = null);
}