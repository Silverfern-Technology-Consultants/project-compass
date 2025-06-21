using Microsoft.Extensions.Logging;
using Compass.Data.Entities;
using Compass.Data.Repositories;

namespace Compass.Core.Services;

public interface IUsageTrackingService
{
    Task TrackAssessmentRun(Guid customerId);
    Task TrackAPICall(Guid customerId, string endpoint);
    Task TrackFeatureUsage(Guid customerId, string featureName, int count = 1);
    Task<bool> CheckUsageLimits(Guid customerId, string metricType);
    Task NotifyUsageThreshold(Guid customerId, string metricType, double thresholdPercentage);
}

public class UsageTrackingService : IUsageTrackingService
{
    private readonly IUsageMetricRepository _usageRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILicenseValidationService _licenseService;
    private readonly ILogger<UsageTrackingService> _logger;

    public UsageTrackingService(
        IUsageMetricRepository usageRepository,
        ISubscriptionRepository subscriptionRepository,
        ILicenseValidationService licenseService,
        ILogger<UsageTrackingService> logger)
    {
        _usageRepository = usageRepository;
        _subscriptionRepository = subscriptionRepository;
        _licenseService = licenseService;
        _logger = logger;
    }

    public async Task TrackAssessmentRun(Guid customerId)
    {
        await RecordUsage(customerId, "AssessmentRun", 1);

        // Check if usage limits exceeded
        var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
        if (subscription?.MaxAssessmentsPerMonth.HasValue == true)
        {
            var currentUsage = await GetCurrentMonthUsage(customerId, "AssessmentRun");
            var usagePercentage = (double)currentUsage / subscription.MaxAssessmentsPerMonth.Value;

            // Notify at 80% and 100% thresholds
            if (usagePercentage >= 0.8)
            {
                await NotifyUsageThreshold(customerId, "AssessmentRun", usagePercentage);
            }
        }

        _logger.LogInformation("Assessment run tracked for customer {CustomerId}", customerId);
    }

    public async Task TrackAPICall(Guid customerId, string endpoint)
    {
        await RecordUsage(customerId, "APICall", 1);

        // Track specific endpoint usage for analytics
        await RecordUsage(customerId, $"APICall_{endpoint}", 1);

        // Rate limiting could be implemented here
        var hasAPIAccess = await _licenseService.ValidateFeatureAccessAsync(customerId, "api-access");
        if (!hasAPIAccess.HasAccess)
        {
            _logger.LogWarning("API call attempted by customer {CustomerId} without API access", customerId);
            throw new UnauthorizedAccessException("API access not included in current plan");
        }

        _logger.LogDebug("API call tracked for customer {CustomerId} to endpoint {Endpoint}", customerId, endpoint);
    }

    public async Task TrackFeatureUsage(Guid customerId, string featureName, int count = 1)
    {
        await RecordUsage(customerId, $"Feature_{featureName}", count);

        _logger.LogDebug("Feature usage tracked: {FeatureName} for customer {CustomerId}", featureName, customerId);
    }

    public async Task<bool> CheckUsageLimits(Guid customerId, string metricType)
    {
        var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
        if (subscription == null) return false;

        var currentUsage = await GetCurrentMonthUsage(customerId, metricType);

        return metricType switch
        {
            "AssessmentRun" => !subscription.MaxAssessmentsPerMonth.HasValue || currentUsage < subscription.MaxAssessmentsPerMonth.Value,
            "APICall" => subscription.IncludesAPI, // API calls are unlimited if access is granted
            _ => true // Default to allow unless specifically limited
        };
    }

    public async Task NotifyUsageThreshold(Guid customerId, string metricType, double thresholdPercentage)
    {
        var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
        if (subscription?.Customer == null) return;

        var notificationLevel = thresholdPercentage switch
        {
            >= 1.0 => "exceeded",
            >= 0.9 => "critical",
            >= 0.8 => "warning",
            _ => "info"
        };

        _logger.LogInformation(
            "Usage threshold {Level} for customer {CustomerId}: {MetricType} at {Percentage:P0}",
            notificationLevel, customerId, metricType, thresholdPercentage);

        // Here you would integrate with email service, webhooks, etc.
        await SendUsageNotification(subscription.Customer, metricType, thresholdPercentage, notificationLevel);
    }

    private async Task RecordUsage(Guid customerId, string metricType, int count)
    {
        var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
        if (subscription == null)
        {
            _logger.LogWarning("No active subscription found for customer {CustomerId}", customerId);
            return;
        }

        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;
        var billingPeriod = $"{currentYear}-{currentMonth:D2}";

        var usageMetric = new UsageMetric
        {
            CustomerId = customerId,
            SubscriptionId = subscription.SubscriptionId,
            MetricType = metricType,
            MetricValue = count,
            BillingPeriod = billingPeriod,
            RecordedDate = DateTime.UtcNow
        };

        await _usageRepository.CreateAsync(usageMetric);
    }

    private async Task<int> GetCurrentMonthUsage(Guid customerId, string metricType)
    {
        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;
        var currentPeriod = $"{currentYear}-{currentMonth:D2}";

        var usage = await _usageRepository.GetByCustomerAndPeriodAsync(customerId, currentPeriod);
        return usage.Where(u => u.MetricType == metricType).Sum(u => u.MetricValue);
    }

    private async Task SendUsageNotification(Customer customer, string metricType, double percentage, string level)
    {
        // Placeholder for email/notification service integration
        var message = level switch
        {
            "exceeded" => $"Your {metricType} usage has exceeded your plan limits.",
            "critical" => $"You're approaching your {metricType} limit ({percentage:P0} used).",
            "warning" => $"You've used {percentage:P0} of your {metricType} allocation.",
            _ => $"Usage update: {metricType} at {percentage:P0}"
        };

        _logger.LogInformation(
            "Usage notification sent to {CustomerEmail}: {Message}",
            customer.Email, message);

        // TODO: Integrate with email service (SendGrid, etc.)
        // await _emailService.SendUsageNotificationAsync(customer.Email, message);
        await Task.CompletedTask;
    }
}