// Compass.Core/Models/LicensingModels.cs
namespace Compass.Core.Models;

public class SubscriptionPlan
{
    public required string Name { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int? MaxSubscriptions { get; set; }
    public int? MaxAssessmentsPerMonth { get; set; }
    public List<string> Features { get; set; } = new();
    public required string SupportLevel { get; set; }
}

public class AccessLevel
{
    public bool HasAccess { get; set; }
    public required string LimitValue { get; set; }
    public int UsageCount { get; set; }
    public int? LimitRemaining => int.TryParse(LimitValue, out var limit) ? Math.Max(0, limit - UsageCount) : null;
}

public class UsageReport
{
    public Guid CustomerId { get; set; }
    public required string BillingPeriod { get; set; }
    public Dictionary<string, int> MetricCounts { get; set; } = new();
    public Dictionary<string, int> Limits { get; set; } = new();
    public Dictionary<string, bool> LimitExceeded { get; set; } = new();
}

public class LicenseLimits
{
    public int? MaxSubscriptions { get; set; }
    public int? MaxAssessmentsPerMonth { get; set; }
    public bool HasAPIAccess { get; set; }
    public bool HasWhiteLabel { get; set; }
    public bool HasCustomBranding { get; set; }
    public required string SupportLevel { get; set; }
}

public class RegisterAccountRequest
{
    public required string CompanyName { get; set; }
    public required string ContactName { get; set; }
    public required string ContactEmail { get; set; }
    public required string ContactPhone { get; set; }
    public required string CompanySize { get; set; }
    public required string Industry { get; set; }
    public required string Country { get; set; }
    public string PlanType { get; set; } = "Trial";
}

public class UpgradeRequest
{
    public required string NewPlanType { get; set; }
    public string BillingCycle { get; set; } = "Monthly";
    public required string PaymentMethodId { get; set; }
}

public class CustomerProfile
{
    public Guid CustomerId { get; set; }
    public required string CompanyName { get; set; }
    public required string ContactName { get; set; }
    public required string ContactEmail { get; set; }
    public required string ContactPhone { get; set; }
    public required string CompanySize { get; set; }
    public required string Industry { get; set; }
    public required string Country { get; set; }
    public bool IsTrialAccount { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public required SubscriptionDetails CurrentSubscription { get; set; }
}

public class SubscriptionDetails
{
    public Guid SubscriptionId { get; set; }
    public required string PlanType { get; set; }
    public required string Status { get; set; }
    public decimal MonthlyPrice { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? NextBillingDate { get; set; }
    public required LicenseLimits Limits { get; set; }
    public required UsageReport CurrentUsage { get; set; }
}

public class PaymentDetails
{
    public required string PaymentMethodId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal Amount { get; set; }
    public required string Description { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public Dictionary<string, object> Details { get; set; } = new();
}

// Removed duplicate Invoice class - use Compass.Data.Entities.Invoice instead