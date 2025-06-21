using Microsoft.EntityFrameworkCore;
using Compass.Core.Models;
using Compass.Data;

namespace Compass.Core.Services;

public interface ILicenseValidationService
{
    Task<bool> HasActiveSubscription(Guid customerId);
    Task<AccessLevel> GetFeatureAccess(Guid customerId, string featureName);
    Task<LicenseLimits> GetCurrentLimits(Guid customerId);
    Task<bool> CanCreateAssessment(Guid customerId);
    Task<bool> CanAddAzureSubscription(Guid customerId);
    Task<UsageReport> GetUsageReport(Guid customerId, string? billingPeriod = null);
    Task<ValidationResult> CanCreateAssessmentAsync(Guid customerId);
    Task<ValidationResult> CanCreateEnvironmentAsync(Guid customerId);
    Task<ValidationResult> CanAddUserAsync(Guid customerId, Guid environmentId);
    Task<ValidationResult> ValidateFeatureAccessAsync(Guid customerId, string featureName);
    Task<SubscriptionInfo> GetSubscriptionInfoAsync(Guid customerId);
}

public class ValidationResult
{
    public bool HasAccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public int? CurrentUsage { get; set; }
    public int? MaxAllowed { get; set; }
}

public class SubscriptionInfo
{
    public string PlanType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? MaxAssessmentsPerMonth { get; set; }
    public int? MaxEnvironments { get; set; }
    public bool IsActive { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
}

public class LicenseValidationService : ILicenseValidationService
{
    private readonly CompassDbContext _context;

    public LicenseValidationService(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasActiveSubscription(Guid customerId)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        return subscription != null;
    }

    public async Task<AccessLevel> GetFeatureAccess(Guid customerId, string featureName)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        if (subscription == null)
        {
            return new AccessLevel { HasAccess = false, LimitValue = "0", UsageCount = 0 };
        }

        // Check feature access based on subscription plan
        var hasAccess = featureName switch
        {
            "unlimited-assessments" => subscription.MaxAssessmentsPerMonth == null,
            "api-access" => subscription.IncludesAPI,
            "white-label" => subscription.IncludesWhiteLabel,
            "custom-branding" => subscription.IncludesCustomBranding,
            "priority-support" => subscription.PrioritySupport,
            _ => true // Default features
        };

        return new AccessLevel
        {
            HasAccess = hasAccess,
            LimitValue = subscription.MaxAssessmentsPerMonth?.ToString() ?? "unlimited",
            UsageCount = 0 // TODO: Calculate actual usage
        };
    }

    public async Task<LicenseLimits> GetCurrentLimits(Guid customerId)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        if (subscription == null)
        {
            return new LicenseLimits
            {
                MaxSubscriptions = 0,
                MaxAssessmentsPerMonth = 0,
                HasAPIAccess = false,
                HasWhiteLabel = false,
                HasCustomBranding = false,
                SupportLevel = "None"
            };
        }

        return new LicenseLimits
        {
            MaxSubscriptions = subscription.MaxSubscriptions,
            MaxAssessmentsPerMonth = subscription.MaxAssessmentsPerMonth,
            HasAPIAccess = subscription.IncludesAPI,
            HasWhiteLabel = subscription.IncludesWhiteLabel,
            HasCustomBranding = subscription.IncludesCustomBranding,
            SupportLevel = subscription.SupportLevel
        };
    }

    public async Task<bool> CanCreateAssessment(Guid customerId)
    {
        var result = await CanCreateAssessmentAsync(customerId);
        return result.HasAccess;
    }

    public async Task<bool> CanAddAzureSubscription(Guid customerId)
    {
        var result = await CanCreateEnvironmentAsync(customerId);
        return result.HasAccess;
    }

    public async Task<UsageReport> GetUsageReport(Guid customerId, string? billingPeriod = null)
    {
        var currentPeriod = billingPeriod ?? $"{DateTime.UtcNow:yyyy-MM}";

        var usage = await _context.UsageMetrics
            .Where(u => u.CustomerId == customerId && u.BillingPeriod == currentPeriod)
            .GroupBy(u => u.MetricType)
            .Select(g => new { MetricType = g.Key, Total = g.Sum(u => u.MetricValue) })
            .ToDictionaryAsync(x => x.MetricType, x => x.Total);

        var limits = await GetCurrentLimits(customerId);

        return new UsageReport
        {
            CustomerId = customerId,
            BillingPeriod = currentPeriod,
            MetricCounts = usage,
            Limits = new Dictionary<string, int>
            {
                ["AssessmentRun"] = limits.MaxAssessmentsPerMonth ?? int.MaxValue,
                ["SubscriptionCount"] = limits.MaxSubscriptions ?? int.MaxValue
            },
            LimitExceeded = new Dictionary<string, bool>
            {
                ["AssessmentRun"] = limits.MaxAssessmentsPerMonth.HasValue &&
                                   usage.GetValueOrDefault("AssessmentRun", 0) >= limits.MaxAssessmentsPerMonth.Value
            }
        };
    }

    public async Task<ValidationResult> CanCreateAssessmentAsync(Guid customerId)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        if (subscription == null)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "No active subscription found",
                ReasonCode = "NO_SUBSCRIPTION"
            };
        }

        // Check if subscription is expired
        if (subscription.Status == "Expired" ||
            (subscription.TrialEndDate.HasValue && subscription.TrialEndDate < DateTime.UtcNow))
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "Subscription has expired",
                ReasonCode = "SUBSCRIPTION_EXPIRED"
            };
        }

        // If unlimited assessments (null), allow
        if (!subscription.MaxAssessmentsPerMonth.HasValue)
        {
            return new ValidationResult { HasAccess = true, Message = "Unlimited assessments" };
        }

        // Count current month's assessments
        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;

        var monthlyUsage = await _context.UsageMetrics
            .Where(u => u.CustomerId == customerId &&
                       u.MetricType == "AssessmentRun" &&
                       u.BillingPeriod == $"{currentYear}-{currentMonth:D2}")
            .SumAsync(u => u.MetricValue);

        if (monthlyUsage >= subscription.MaxAssessmentsPerMonth.Value)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "Assessment limit reached for current billing period",
                ReasonCode = "LIMIT_REACHED",
                CurrentUsage = monthlyUsage,
                MaxAllowed = subscription.MaxAssessmentsPerMonth.Value
            };
        }

        return new ValidationResult
        {
            HasAccess = true,
            Message = "Assessment allowed",
            CurrentUsage = monthlyUsage,
            MaxAllowed = subscription.MaxAssessmentsPerMonth.Value
        };
    }

    public async Task<ValidationResult> CanCreateEnvironmentAsync(Guid customerId)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        if (subscription == null)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "No active subscription found",
                ReasonCode = "NO_SUBSCRIPTION"
            };
        }

        if (!subscription.MaxEnvironments.HasValue)
        {
            return new ValidationResult { HasAccess = true, Message = "Unlimited environments" };
        }

        // For now, just count assessments as environments aren't implemented yet
        var currentEnvironments = await _context.Assessments
            .Where(a => a.CustomerId == customerId)
            .Select(a => a.EnvironmentId)
            .Distinct()
            .CountAsync();

        if (currentEnvironments >= subscription.MaxEnvironments.Value)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "Environment limit reached",
                ReasonCode = "LIMIT_REACHED",
                CurrentUsage = currentEnvironments,
                MaxAllowed = subscription.MaxEnvironments.Value
            };
        }

        return new ValidationResult
        {
            HasAccess = true,
            Message = "Environment creation allowed",
            CurrentUsage = currentEnvironments,
            MaxAllowed = subscription.MaxEnvironments.Value
        };
    }

    public async Task<ValidationResult> CanAddUserAsync(Guid customerId, Guid environmentId)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        if (subscription == null)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "No active subscription found",
                ReasonCode = "NO_SUBSCRIPTION"
            };
        }

        // Implementation depends on your user management system
        return new ValidationResult { HasAccess = true, Message = "User addition allowed" };
    }

    public async Task<ValidationResult> ValidateFeatureAccessAsync(Guid customerId, string featureName)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);
        if (subscription == null)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "No active subscription found",
                ReasonCode = "NO_SUBSCRIPTION"
            };
        }

        var hasAccess = featureName switch
        {
            "CustomReporting" => subscription.CustomReporting,
            "ApiAccess" => subscription.IncludesAPI,
            "PrioritySupport" => subscription.PrioritySupport,
            "WhiteLabel" => subscription.IncludesWhiteLabel,
            "CustomBranding" => subscription.IncludesCustomBranding,
            _ => true // Default features available
        };

        return new ValidationResult
        {
            HasAccess = hasAccess,
            Message = hasAccess ? "Feature access granted" : "Feature not available in current plan"
        };
    }

    public async Task<SubscriptionInfo> GetSubscriptionInfoAsync(Guid customerId)
    {
        var subscription = await GetActiveSubscriptionAsync(customerId);

        return new SubscriptionInfo
        {
            PlanType = subscription?.PlanType ?? "None",
            Status = subscription?.Status ?? "Inactive",
            MaxAssessmentsPerMonth = subscription?.MaxAssessmentsPerMonth,
            MaxEnvironments = subscription?.MaxEnvironments,
            IsActive = subscription?.Status == "Active" || subscription?.Status == "Trial",
            TrialEndDate = subscription?.TrialEndDate,
            NextBillingDate = subscription?.NextBillingDate
        };
    }

    private async Task<Data.Entities.Subscription?> GetActiveSubscriptionAsync(Guid customerId)
    {
        return await _context.Subscriptions
            .FirstOrDefaultAsync(s => s.CustomerId == customerId &&
                                    (s.Status == "Active" || s.Status == "Trial"));
    }
}