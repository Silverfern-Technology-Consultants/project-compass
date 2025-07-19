using Compass.Data;
using Compass.Data.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Compass.Data.Interfaces;

namespace Compass.Core.Services;

public interface IUsageTrackingService
{
    // ✅ NEW: Organization-scoped methods
    Task TrackAssessmentRun(Guid organizationId);
    Task TrackAPICall(Guid organizationId, string endpoint);
    Task TrackFeatureUsage(Guid organizationId, string featureName, int count = 1);
    Task<bool> CheckUsageLimits(Guid organizationId, string metricType);
    Task NotifyUsageThreshold(Guid organizationId, string metricType, double thresholdPercentage);

    // ✅ BACKWARD COMPATIBILITY: Customer-scoped methods (will convert to organization internally)
    Task TrackAssessmentRunForCustomer(Guid customerId);
    Task TrackAPICallForCustomer(Guid customerId, string endpoint);
}

public class UsageTrackingService : IUsageTrackingService
{
    private readonly IUsageMetricRepository _usageRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILicenseValidationService _licenseService;
    private readonly CompassDbContext _context;
    private readonly ILogger<UsageTrackingService> _logger;

    public UsageTrackingService(
        IUsageMetricRepository usageRepository,
        ISubscriptionRepository subscriptionRepository,
        ILicenseValidationService licenseService,
        CompassDbContext context,
        ILogger<UsageTrackingService> logger)
    {
        _usageRepository = usageRepository;
        _subscriptionRepository = subscriptionRepository;
        _licenseService = licenseService;
        _context = context;
        _logger = logger;
    }

    // ✅ ORGANIZATION-SCOPED METHODS

    public async Task TrackAssessmentRun(Guid organizationId)
    {
        await RecordUsageForOrganization(organizationId, "AssessmentRun", 1);

        // Check if usage limits exceeded
        var subscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId);
        if (subscription?.MaxAssessmentsPerMonth.HasValue == true)
        {
            var currentUsage = await GetCurrentMonthUsageForOrganization(organizationId, "AssessmentRun");
            var usagePercentage = (double)currentUsage / subscription.MaxAssessmentsPerMonth.Value;

            // Notify at 80% and 100% thresholds
            if (usagePercentage >= 0.8)
            {
                await NotifyUsageThreshold(organizationId, "AssessmentRun", usagePercentage);
            }
        }

        _logger.LogInformation("Assessment run tracked for organization {OrganizationId}", organizationId);
    }

    public async Task TrackAPICall(Guid organizationId, string endpoint)
    {
        await RecordUsageForOrganization(organizationId, "APICall", 1);

        // Track specific endpoint usage for analytics
        await RecordUsageForOrganization(organizationId, $"APICall_{endpoint}", 1);

        // Rate limiting could be implemented here
        var hasAPIAccess = await _licenseService.ValidateFeatureAccessAsync(organizationId, "api-access");
        if (!hasAPIAccess.HasAccess)
        {
            _logger.LogWarning("API call attempted by organization {OrganizationId} without API access", organizationId);
            throw new UnauthorizedAccessException("API access not included in current plan");
        }

        _logger.LogDebug("API call tracked for organization {OrganizationId} to endpoint {Endpoint}", organizationId, endpoint);
    }

    public async Task TrackFeatureUsage(Guid organizationId, string featureName, int count = 1)
    {
        await RecordUsageForOrganization(organizationId, $"Feature_{featureName}", count);

        _logger.LogDebug("Feature usage tracked: {FeatureName} for organization {OrganizationId}", featureName, organizationId);
    }

    public async Task<bool> CheckUsageLimits(Guid organizationId, string metricType)
    {
        var subscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId);
        if (subscription == null) return false;

        var currentUsage = await GetCurrentMonthUsageForOrganization(organizationId, metricType);

        return metricType switch
        {
            "AssessmentRun" => !subscription.MaxAssessmentsPerMonth.HasValue || currentUsage < subscription.MaxAssessmentsPerMonth.Value,
            "APICall" => subscription.IncludesAPI, // API calls are unlimited if access is granted
            _ => true // Default to allow unless specifically limited
        };
    }

    public async Task NotifyUsageThreshold(Guid organizationId, string metricType, double thresholdPercentage)
    {
        var subscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId);
        if (subscription?.Customer == null) return;

        var notificationLevel = thresholdPercentage switch
        {
            >= 1.0 => "exceeded",
            >= 0.9 => "critical",
            >= 0.8 => "warning",
            _ => "info"
        };

        _logger.LogInformation(
            "Usage threshold {Level} for organization {OrganizationId}: {MetricType} at {Percentage:P0}",
            notificationLevel, organizationId, metricType, thresholdPercentage);

        // Here you would integrate with email service, webhooks, etc.
        await SendUsageNotification(subscription.Customer, metricType, thresholdPercentage, notificationLevel);
    }

    // ✅ BACKWARD COMPATIBILITY: Customer-scoped methods
    public async Task TrackAssessmentRunForCustomer(Guid customerId)
    {
        // Convert customer ID to organization ID and use organization method
        var organizationId = await GetOrganizationIdFromCustomer(customerId);
        if (organizationId.HasValue)
        {
            await TrackAssessmentRun(organizationId.Value);
        }
        else
        {
            _logger.LogWarning("Could not find organization for customer {CustomerId}", customerId);
        }
    }

    public async Task TrackAPICallForCustomer(Guid customerId, string endpoint)
    {
        // Convert customer ID to organization ID and use organization method
        var organizationId = await GetOrganizationIdFromCustomer(customerId);
        if (organizationId.HasValue)
        {
            await TrackAPICall(organizationId.Value, endpoint);
        }
        else
        {
            _logger.LogWarning("Could not find organization for customer {CustomerId}", customerId);
        }
    }

    // ✅ PRIVATE HELPER METHODS

    private async Task RecordUsageForOrganization(Guid organizationId, string metricType, int count)
    {
        var subscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId);
        if (subscription == null)
        {
            _logger.LogWarning("No active subscription found for organization {OrganizationId}", organizationId);
            return;
        }

        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;
        var billingPeriod = $"{currentYear}-{currentMonth:D2}";

        // ✅ IMPORTANT: Still record with the subscription's customer ID for data model compatibility
        var usageMetric = new UsageMetric
        {
            CustomerId = subscription.CustomerId, // The subscription owner's customer ID
            SubscriptionId = subscription.SubscriptionId,
            MetricType = metricType,
            MetricValue = count,
            BillingPeriod = billingPeriod,
            RecordedDate = DateTime.UtcNow
        };

        await _usageRepository.CreateAsync(usageMetric);
    }

    private async Task<int> GetCurrentMonthUsageForOrganization(Guid organizationId, string metricType)
    {
        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;
        var currentPeriod = $"{currentYear}-{currentMonth:D2}";

        // ✅ FIXED: Query by organization instead of individual customer
        var usage = await _context.UsageMetrics
            .Include(u => u.Customer)
            .Where(u => u.Customer.OrganizationId == organizationId &&
                       u.BillingPeriod == currentPeriod &&
                       u.MetricType == metricType)
            .SumAsync(u => u.MetricValue);

        return usage;
    }

    private async Task<Guid?> GetOrganizationIdFromCustomer(Guid customerId)
    {
        var customer = await _context.Customers
            .Where(c => c.CustomerId == customerId)
            .Select(c => c.OrganizationId)
            .FirstOrDefaultAsync();

        return customer;
    }

    private async Task SendUsageNotification(Customer customer, string metricType, double percentage, string level)
    {
        // Placeholder for email/notification service integration
        var message = level switch
        {
            "exceeded" => $"Your organization's {metricType} usage has exceeded your plan limits.",
            "critical" => $"Your organization is approaching your {metricType} limit ({percentage:P0} used).",
            "warning" => $"Your organization has used {percentage:P0} of your {metricType} allocation.",
            _ => $"Usage update: {metricType} at {percentage:P0}"
        };

        _logger.LogInformation(
            "Usage notification sent to {CustomerEmail}: {Message}",
            customer.Email, message);

        // TODO: Integrate with email service (SendGrid, etc.)
        // await _emailService.SendUsageNotificationAsync(customer.Email, message);
        await Task.CompletedTask;
    }

    // ✅ LEGACY METHODS (for backward compatibility)
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
}