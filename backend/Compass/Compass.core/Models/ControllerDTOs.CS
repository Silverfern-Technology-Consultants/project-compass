// Add to Compass.Core/Models/ folder
namespace Compass.Core.Models;

// LicensingController DTOs
public class FeatureAccess
{
    public required string FeatureName { get; set; }
    public bool HasAccess { get; set; }
    public required string LimitValue { get; set; }
    public int UsageCount { get; set; }
    public int? LimitRemaining { get; set; }
}

public class AccessResult
{
    public bool HasAccess { get; set; }
    public required string Message { get; set; }
    public required LimitInfo LimitInfo { get; set; }
}

public class LimitInfo
{
    public required string LimitValue { get; set; }
    public int UsageCount { get; set; }
    public int? LimitRemaining { get; set; }
}

public class CheckAccessRequest
{
    public required string FeatureName { get; set; }
}

public class TrackUsageRequest
{
    public required string MetricType { get; set; }
    public int Count { get; set; } = 1;
    public string? Details { get; set; }
}

// AccountController DTOs
public class UpdateProfileRequest
{
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? CompanySize { get; set; }
    public string? Industry { get; set; }
    public string? TimeZone { get; set; }
}

public class StartTrialRequest
{
    public string PlanType { get; set; } = "Professional";
    public int TrialDays { get; set; } = 14;
}

// SubscriptionController DTOs
public class CancelRequest
{
    public bool ImmediateCancel { get; set; } = false;
    public string? Reason { get; set; }
}