// Compass.Api/Controllers/LicensingController.cs
using Microsoft.AspNetCore.Mvc;
using Compass.Core.Services;
using Compass.Core.Models;
namespace Compass.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class LicensingController : ControllerBase
{
    private readonly ILicenseValidationService _licenseService;
    private readonly IUsageTrackingService _usageService;
    private readonly ILogger<LicensingController> _logger;
    public LicensingController(
        ILicenseValidationService licenseService,
        IUsageTrackingService usageService,
        ILogger<LicensingController> logger)
    {
        _licenseService = licenseService;
        _usageService = usageService;
        _logger = logger;
    }
    [HttpGet("features")]
    public async Task<ActionResult<List<FeatureAccess>>> GetAvailableFeatures()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var features = new List<string>
                {
                    "unlimited-assessments",
                    "api-access",
                    "white-label",
                    "custom-branding",
                    "advanced-analytics",
                    "priority-support",
                    "multi-tenant"
                };
            var featureAccessList = new List<FeatureAccess>();
            foreach (var feature in features)
            {
                var access = await _licenseService.GetFeatureAccess(customerId, feature);
                featureAccessList.Add(new FeatureAccess
                {
                    FeatureName = feature,
                    HasAccess = access.HasAccess,
                    LimitValue = access.LimitValue,
                    UsageCount = access.UsageCount,
                    LimitRemaining = access.LimitRemaining
                });
            }
            return Ok(featureAccessList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving feature access information");
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpGet("limits")]
    public async Task<ActionResult<LicenseLimits>> GetCurrentLimits()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var limits = await _licenseService.GetCurrentLimits(customerId);
            return Ok(limits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving license limits");
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpPost("check-access")]
    public async Task<ActionResult<AccessResult>> CheckFeatureAccess(CheckAccessRequest request)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var access = await _licenseService.GetFeatureAccess(customerId, request.FeatureName);
            var result = new AccessResult
            {
                HasAccess = access.HasAccess,
                Message = access.HasAccess ? "Access granted" : "Feature not available in current plan",
                LimitInfo = new LimitInfo
                {
                    LimitValue = access.LimitValue,
                    UsageCount = access.UsageCount,
                    LimitRemaining = access.LimitRemaining
                }
            };
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking feature access for {FeatureName}", request.FeatureName);
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpPost("validate-assessment")]
    public async Task<ActionResult<AccessResult>> ValidateAssessmentAccess()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var canCreate = await _licenseService.CanCreateAssessment(customerId);
            var result = new AccessResult
            {
                HasAccess = canCreate,
                Message = canCreate ? "Assessment creation allowed" : "Assessment limit reached for current billing period"
            };
            if (!canCreate)
            {
                var limits = await _licenseService.GetCurrentLimits(customerId);
                var usage = await _licenseService.GetUsageReport(customerId);
                result.LimitInfo = new LimitInfo
                {
                    LimitValue = limits.MaxAssessmentsPerMonth?.ToString() ?? "unlimited",
                    UsageCount = usage.MetricCounts.GetValueOrDefault("AssessmentRun", 0),
                    LimitRemaining = limits.MaxAssessmentsPerMonth.HasValue
                        ? Math.Max(0, limits.MaxAssessmentsPerMonth.Value - usage.MetricCounts.GetValueOrDefault("AssessmentRun", 0))
                        : null
                };
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating assessment access");
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpPost("validate-subscription")]
    public async Task<ActionResult<AccessResult>> ValidateSubscriptionAccess()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var canAdd = await _licenseService.CanAddAzureSubscription(customerId);
            var result = new AccessResult
            {
                HasAccess = canAdd,
                Message = canAdd ? "Azure subscription addition allowed" : "Maximum Azure subscriptions reached for current plan"
            };
            if (!canAdd)
            {
                var limits = await _licenseService.GetCurrentLimits(customerId);
                // TODO: Get actual subscription count
                var currentCount = 0; // Placeholder
                result.LimitInfo = new LimitInfo
                {
                    LimitValue = limits.MaxSubscriptions?.ToString() ?? "unlimited",
                    UsageCount = currentCount,
                    LimitRemaining = limits.MaxSubscriptions.HasValue
                        ? Math.Max(0, limits.MaxSubscriptions.Value - currentCount)
                        : null
                };
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating subscription access");
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpPost("track-usage")]
    public async Task<IActionResult> TrackUsage(TrackUsageRequest request)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            switch (request.MetricType.ToLower())
            {
                case "assessment":
                    await _usageService.TrackAssessmentRun(customerId);
                    break;
                case "api":
                    await _usageService.TrackAPICall(customerId, request.Details ?? "unknown");
                    break;
                default:
                    await _usageService.TrackFeatureUsage(customerId, request.MetricType, request.Count);
                    break;
            }
            return Ok(new { Message = "Usage tracked successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(402, new { Message = ex.Message }); // Payment Required
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking usage for {MetricType}", request.MetricType);
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpGet("usage-report")]
    public async Task<ActionResult<UsageReport>> GetUsageReport(string? billingPeriod = null)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var report = await _licenseService.GetUsageReport(customerId, billingPeriod);
            return Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage report");
            return StatusCode(500, "Internal server error");
        }
    }
    private Guid GetCustomerIdFromContext()
    {
        // TODO: Implement proper authentication and extract customer ID from JWT token or session
        // For now, return a placeholder - this should be replaced with actual authentication
        return Guid.Parse("00000000-0000-0000-0000-000000000001");
    }
}
public class FeatureAccess
{
    public string FeatureName { get; set; } = string.Empty;
    public bool HasAccess { get; set; }
    public string LimitValue { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public int? LimitRemaining { get; set; }
}
public class AccessResult
{
    public bool HasAccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public LimitInfo LimitInfo { get; set; } = new();
}
public class LimitInfo
{
    public string LimitValue { get; set; }
    public int UsageCount { get; set; }
    public int? LimitRemaining { get; set; }
}
public class CheckAccessRequest
{
    public string FeatureName { get; set; } = string.Empty;
}
public class TrackUsageRequest
{
    public string MetricType { get; set; } = string.Empty;
    public int Count { get; set; } = 1;
    public string Details { get; set; } = string.Empty;
}
public class FeatureLimitInfo
{
    public string FeatureName { get; set; } = string.Empty;
    public bool IsWithinLimit { get; set; }
    public string LimitValue { get; set; } = string.Empty;
}
public class LicenseViolationResponse
{
    public bool IsViolation { get; set; }
    public string Message { get; set; } = string.Empty;
    public FeatureLimitInfo LimitInfo { get; set; } = new();
}
public class CurrentLimitInfo
{
    public required string LimitValue { get; set; }
    public int CurrentUsage { get; set; }
    public bool IsExceeded { get; set; }
}

public class FeatureStatus
{
    public string FeatureName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string? Value { get; set; }
}
public class UsageDetail
{
    public string MetricType { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Details { get; set; } = string.Empty;
}