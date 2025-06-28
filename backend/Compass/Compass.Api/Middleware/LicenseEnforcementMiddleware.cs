using Compass.Core.Services;
using System.Text.Json;
namespace Compass.Api.Middleware
{
    public class LicenseEnforcementMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LicenseEnforcementMiddleware> _logger;
        private readonly Dictionary<string, string> _endpointFeatureMap;
        public LicenseEnforcementMiddleware(RequestDelegate next, ILogger<LicenseEnforcementMiddleware> logger)
        {
            _next = next;
            _logger = logger;
            // Map API endpoints to required features
            _endpointFeatureMap = new Dictionary<string, string>
            {
                ["/api/assessments"] = "assessment-access",
                ["/api/licensing/track-usage"] = "api-access",
                ["/api/subscription/upgrade"] = "subscription-management"
            };
        }
        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLower();
            var method = context.Request.Method;
            // Skip license check for certain endpoints
            if (ShouldSkipLicenseCheck(path, method))
            {
                await _next(context);
                return;
            }
            try
            {
                var customerId = ExtractCustomerIdFromContext(context);
                if (customerId == Guid.Empty)
                {
                    await WriteUnauthorizedResponse(context, "Customer identification required");
                    return;
                }
                // Get license service from DI container
                var licenseService = context.RequestServices.GetRequiredService<ILicenseValidationService>();
                // Check if customer has active subscription
                var hasActiveSubscription = await licenseService.HasActiveSubscription(customerId);
                if (!hasActiveSubscription)
                {
                    await WritePaymentRequiredResponse(context, "Active subscription required");
                    return;
                }
                // Check specific feature requirements
                var requiredFeature = GetRequiredFeature(path, method);
                if (!string.IsNullOrEmpty(requiredFeature))
                {
                    var featureAccess = await licenseService.GetFeatureAccess(customerId, requiredFeature);
                    if (!featureAccess.HasAccess)
                    {
                        await WritePaymentRequiredResponse(context, $"Feature '{requiredFeature}' not available in current plan");
                        return;
                    }
                }
                // Check assessment-specific limits
                if (IsAssessmentCreationRequest(path, method))
                {
                    var canCreate = await licenseService.CanCreateAssessment(customerId);
                    if (!canCreate)
                    {
                        await WritePaymentRequiredResponse(context, "Assessment limit reached for current billing period");
                        return;
                    }
                }
                // Track usage for API calls
                if (ShouldTrackUsage(path, method))
                {
                    var usageService = context.RequestServices.GetRequiredService<IUsageTrackingService>();
                    await usageService.TrackAPICall(customerId, path ?? "/unknown");
                }
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in license enforcement middleware");
                await WriteErrorResponse(context, "License validation failed");
            }
        }
        private bool ShouldSkipLicenseCheck(string? path, string method)
        {
            if (string.IsNullOrEmpty(path)) return true;
            var skipPaths = new[]
            {
        "/health",
        "/api/account/register",
        "/api/account/test-connection",
        "/api/subscription/plans",
        "/swagger",
        "/favicon.ico"
        };
            return skipPaths.Any(skipPath => path.StartsWith(skipPath.ToLower()));
        }
        private Guid ExtractCustomerIdFromContext(HttpContext context)
        {
            // TODO: Extract from JWT token claims
            // For now, return a placeholder - implement proper authentication
            // Example of how this might work with JWT:
            // var customerIdClaim = context.User.FindFirst("CustomerId");
            // if (customerIdClaim != null && Guid.TryParse(customerIdClaim.Value, out var customerId))
            //     return customerId;
            return Guid.Parse("00000000-0000-0000-0000-000000000001");
        }
        private string? GetRequiredFeature(string? path, string method)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (path.Contains("/api/licensing") && method != "GET")
                return "api-access";
            if (path.Contains("/api/assessments") && method == "POST")
                return "assessment-creation";
            return _endpointFeatureMap.TryGetValue(path, out var feature) ? feature : null;
        }
        private bool IsAssessmentCreationRequest(string? path, string method)
        {
            return path?.Contains("/api/assessments") == true && method == "POST";
        }
        private bool ShouldTrackUsage(string? path, string method)
        {
            if (string.IsNullOrEmpty(path)) return false;

            // Track all API calls except GET requests to public endpoints
            var publicGetPaths = new[]
            {
        "/api/subscription/plans",
        "/api/licensing/features",
        "/health"
        };

            if (method == "GET" && publicGetPaths.Any(p => path.StartsWith(p.ToLower())))
                return false;

            return path.StartsWith("/api/");
        }
        private async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var response = new
            {
                Error = "Unauthorized",
                Message = message,
                Timestamp = DateTime.UtcNow
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        private async Task WritePaymentRequiredResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 402; // Payment Required
            context.Response.ContentType = "application/json";
            var response = new
            {
                Error = "PaymentRequired",
                Message = message,
                Timestamp = DateTime.UtcNow,
                UpgradeUrl = "/subscription/upgrade"
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        private async Task WriteErrorResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            var response = new
            {
                Error = "InternalServerError",
                Message = message,
                Timestamp = DateTime.UtcNow
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}