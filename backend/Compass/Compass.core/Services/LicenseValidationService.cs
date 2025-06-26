using Microsoft.EntityFrameworkCore;
using Compass.Core.Models;
using Compass.Data;

namespace Compass.Core.Services;

public interface ILicenseValidationService
{
    // ✅ UPDATED: Organization-scoped methods
    Task<bool> HasActiveSubscription(Guid organizationId);
    Task<AccessLevel> GetFeatureAccess(Guid organizationId, string featureName);
    Task<LicenseLimits> GetCurrentLimits(Guid organizationId);
    Task<bool> CanCreateAssessment(Guid organizationId);
    Task<bool> CanAddAzureSubscription(Guid organizationId);
    Task<UsageReport> GetUsageReport(Guid organizationId, string? billingPeriod = null);
    Task<ValidationResult> CanCreateAssessmentAsync(Guid organizationId);
    Task<ValidationResult> CanCreateEnvironmentAsync(Guid organizationId);
    Task<ValidationResult> CanAddUserAsync(Guid organizationId, Guid environmentId);
    Task<ValidationResult> ValidateFeatureAccessAsync(Guid organizationId, string featureName);
    Task<SubscriptionInfo> GetSubscriptionInfoAsync(Guid organizationId);
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

    // ORGANIZATION-SCOPED METHODS

    public async Task<bool> HasActiveSubscription(Guid organizationId)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
        return subscription != null;
    }

    public async Task<AccessLevel> GetFeatureAccess(Guid organizationId, string featureName)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
        if (subscription == null)
        {
            return new AccessLevel { HasAccess = false, LimitValue = "0", UsageCount = 0 };
        }

        var hasAccess = featureName switch
        {
            "unlimited-assessments" => subscription.MaxAssessmentsPerMonth == null,
            "api-access" => subscription.IncludesAPI,
            "white-label" => subscription.IncludesWhiteLabel,
            "custom-branding" => subscription.IncludesCustomBranding,
            "priority-support" => subscription.PrioritySupport,
            _ => true
        };

        return new AccessLevel
        {
            HasAccess = hasAccess,
            LimitValue = subscription.MaxAssessmentsPerMonth?.ToString() ?? "unlimited",
            UsageCount = 0 // TODO: Calculate actual usage
        };
    }

    public async Task<LicenseLimits> GetCurrentLimits(Guid organizationId)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
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

    public async Task<bool> CanCreateAssessment(Guid organizationId)
    {
        var result = await CanCreateAssessmentAsync(organizationId);
        return result.HasAccess;
    }

    public async Task<bool> CanAddAzureSubscription(Guid organizationId)
    {
        var result = await CanCreateEnvironmentAsync(organizationId);
        return result.HasAccess;
    }

    public async Task<UsageReport> GetUsageReport(Guid organizationId, string? billingPeriod = null)
    {
        var currentPeriod = billingPeriod ?? $"{DateTime.UtcNow:yyyy-MM}";

        // FIXED: Organization-scoped usage query
        var usage = await _context.UsageMetrics
            .Where(u => u.Customer.OrganizationId == organizationId && u.BillingPeriod == currentPeriod)
            .GroupBy(u => u.MetricType)
            .Select(g => new { MetricType = g.Key, Total = g.Sum(u => u.MetricValue) })
            .ToDictionaryAsync(x => x.MetricType, x => x.Total);

        var limits = await GetCurrentLimits(organizationId);

        return new UsageReport
        {
            CustomerId = organizationId, // Using organization ID here
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

    public async Task<ValidationResult> CanCreateAssessmentAsync(Guid organizationId)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
        if (subscription == null)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "No active subscription found",
                ReasonCode = "NO_SUBSCRIPTION"
            };
        }

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

        if (!subscription.MaxAssessmentsPerMonth.HasValue)
        {
            return new ValidationResult { HasAccess = true, Message = "Unlimited assessments" };
        }

        // FIXED: Organization-scoped usage count
        var currentMonth = DateTime.UtcNow.Month;
        var currentYear = DateTime.UtcNow.Year;

        var monthlyUsage = await _context.UsageMetrics
            .Where(u => u.Customer.OrganizationId == organizationId &&
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

    public async Task<ValidationResult> CanCreateEnvironmentAsync(Guid organizationId)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
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

        // FIXED: Organization-scoped environment count
        var currentEnvironments = await _context.Assessments
            .Where(a => a.OrganizationId == organizationId)
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

    public async Task<ValidationResult> CanAddUserAsync(Guid organizationId, Guid environmentId)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
        if (subscription == null)
        {
            return new ValidationResult
            {
                HasAccess = false,
                Message = "No active subscription found",
                ReasonCode = "NO_SUBSCRIPTION"
            };
        }

        return new ValidationResult { HasAccess = true, Message = "User addition allowed" };
    }

    public async Task<ValidationResult> ValidateFeatureAccessAsync(Guid organizationId, string featureName)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);
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
            _ => true
        };

        return new ValidationResult
        {
            HasAccess = hasAccess,
            Message = hasAccess ? "Feature access granted" : "Feature not available in current plan"
        };
    }

    public async Task<SubscriptionInfo> GetSubscriptionInfoAsync(Guid organizationId)
    {
        var subscription = await GetActiveSubscriptionByOrganizationAsync(organizationId);

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

    // NEW: Organization-scoped subscription query
    private async Task<Data.Entities.Subscription?> GetActiveSubscriptionByOrganizationAsync(Guid organizationId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
            .Where(s => s.Customer.OrganizationId == organizationId &&
                       (s.Status == "Active" || s.Status == "Trial"))
            .OrderByDescending(s => s.CreatedDate)
            .FirstOrDefaultAsync();
    }
}