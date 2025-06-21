// Compass.Api/Controllers/SubscriptionController.cs
using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILicenseValidationService _licenseService;
    private readonly IUsageTrackingService _usageService;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionRepository subscriptionRepository,
        ILicenseValidationService licenseService,
        IUsageTrackingService usageService,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _licenseService = licenseService;
        _usageService = usageService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<SubscriptionDetails>> GetCurrentSubscription()
    {
        try
        {
            // TODO: Get customer ID from authenticated user context
            var customerId = GetCustomerIdFromContext();

            var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
            if (subscription == null)
            {
                return NotFound("No active subscription found");
            }

            var limits = await _licenseService.GetCurrentLimits(customerId);
            var usage = await _licenseService.GetUsageReport(customerId);

            var subscriptionDetails = new SubscriptionDetails
            {
                SubscriptionId = subscription.SubscriptionId,
                PlanType = subscription.PlanType,
                Status = subscription.Status,
                MonthlyPrice = subscription.MonthlyPrice, // Now non-nullable
                StartDate = subscription.StartDate,
                NextBillingDate = subscription.NextBillingDate,
                Limits = limits,
                CurrentUsage = usage
            };

            return Ok(subscriptionDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription details");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeSubscription(UpgradeRequest request)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();

            var currentSubscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
            if (currentSubscription == null)
            {
                return BadRequest("No active subscription found to upgrade");
            }

            // Validate the new plan
            var planConfig = GetPlanConfiguration(request.NewPlanType);
            if (planConfig == null)
            {
                return BadRequest("Invalid plan type");
            }

            // Update subscription
            currentSubscription.PlanType = request.NewPlanType;
            currentSubscription.BillingCycle = request.BillingCycle;
            currentSubscription.MonthlyPrice = planConfig.MonthlyPrice;
            currentSubscription.AnnualPrice = planConfig.AnnualPrice;
            currentSubscription.MaxSubscriptions = planConfig.MaxSubscriptions ?? 0;
            currentSubscription.MaxAssessmentsPerMonth = planConfig.MaxAssessmentsPerMonth;
            currentSubscription.IncludesAPI = planConfig.Features.Contains("api-access");
            currentSubscription.IncludesWhiteLabel = planConfig.Features.Contains("white-label");
            currentSubscription.IncludesCustomBranding = planConfig.Features.Contains("custom-branding");
            currentSubscription.SupportLevel = planConfig.SupportLevel;

            await _subscriptionRepository.UpdateAsync(currentSubscription);

            _logger.LogInformation("Subscription upgraded for customer {CustomerId} to {PlanType}", customerId, request.NewPlanType);

            return Ok(new { Message = "Subscription upgraded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error upgrading subscription");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscription([FromBody] CancelRequest request)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();

            var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
            if (subscription == null)
            {
                return NotFound("No active subscription found");
            }

            subscription.Status = "Cancelled";
            subscription.EndDate = request.ImmediateCancel ? DateTime.UtcNow : subscription.NextBillingDate;
            subscription.AutoRenew = false;

            await _subscriptionRepository.UpdateAsync(subscription);

            _logger.LogInformation("Subscription cancelled for customer {CustomerId}", customerId);

            return Ok(new { Message = "Subscription cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("usage")]
    public async Task<ActionResult<UsageReport>> GetUsageMetrics(DateTime? startDate, DateTime? endDate)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();

            string? billingPeriod = null;
            if (startDate.HasValue)
            {
                billingPeriod = startDate.Value.ToString("yyyy-MM");
            }

            var usage = await _licenseService.GetUsageReport(customerId, billingPeriod);
            return Ok(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving usage metrics");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<List<Compass.Data.Entities.Invoice>>> GetInvoices()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();

            // TODO: Implement invoice repository and get invoices
            var invoices = new List<Compass.Data.Entities.Invoice>(); // Use fully qualified name

            return Ok(invoices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving invoices");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("plans")]
    public ActionResult<List<SubscriptionPlan>> GetAvailablePlans()
    {
        try
        {
            var plans = new List<SubscriptionPlan>
                {
                    new SubscriptionPlan
                    {
                        Name = "Starter",
                        MonthlyPrice = 99.00m,
                        AnnualPrice = 990.00m,
                        MaxSubscriptions = 3,
                        MaxAssessmentsPerMonth = 1,
                        Features = new List<string> { "basic-reports", "email-support" },
                        SupportLevel = "Email"
                    },
                    new SubscriptionPlan
                    {
                        Name = "Professional",
                        MonthlyPrice = 299.00m,
                        AnnualPrice = 2990.00m,
                        MaxSubscriptions = 10,
                        MaxAssessmentsPerMonth = null,
                        Features = new List<string> { "unlimited-assessments", "advanced-analytics", "priority-support", "custom-branding" },
                        SupportLevel = "Priority"
                    },
                    new SubscriptionPlan
                    {
                        Name = "Enterprise",
                        MonthlyPrice = 699.00m,
                        AnnualPrice = 6990.00m,
                        MaxSubscriptions = null,
                        MaxAssessmentsPerMonth = null,
                        Features = new List<string> { "white-label", "api-access", "dedicated-support", "multi-tenant" },
                        SupportLevel = "Dedicated"
                    }
                };

            return Ok(plans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available plans");
            return StatusCode(500, "Internal server error");
        }
    }

    private Guid GetCustomerIdFromContext()
    {
        // TODO: Implement proper authentication and extract customer ID from JWT token or session
        // For now, return a placeholder - this should be replaced with actual authentication
        return Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

    }

    private SubscriptionPlan? GetPlanConfiguration(string planType)
    {
        return planType switch
        {
            "Starter" => new SubscriptionPlan
            {
                Name = "Starter",
                MonthlyPrice = 99.00m,
                AnnualPrice = 990.00m,
                MaxSubscriptions = 3,
                MaxAssessmentsPerMonth = 1,
                Features = new List<string> { "basic-reports", "email-support" },
                SupportLevel = "Email"
            },
            "Professional" => new SubscriptionPlan
            {
                Name = "Professional",
                MonthlyPrice = 299.00m,
                AnnualPrice = 2990.00m,
                MaxSubscriptions = 10,
                MaxAssessmentsPerMonth = null,
                Features = new List<string> { "unlimited-assessments", "advanced-analytics", "priority-support", "custom-branding" },
                SupportLevel = "Priority"
            },
            "Enterprise" => new SubscriptionPlan
            {
                Name = "Enterprise",
                MonthlyPrice = 699.00m,
                AnnualPrice = 6990.00m,
                MaxSubscriptions = null,
                MaxAssessmentsPerMonth = null,
                Features = new List<string> { "white-label", "api-access", "dedicated-support", "multi-tenant" },
                SupportLevel = "Dedicated"
            },
            _ => null
        };
    }
}