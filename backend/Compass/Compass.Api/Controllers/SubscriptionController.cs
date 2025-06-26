// Compass.Api/Controllers/SubscriptionController.cs
using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var subscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId.Value);
            if (subscription == null)
            {
                return NotFound("No active subscription found for organization");
            }

            var limits = await _licenseService.GetCurrentLimits(organizationId.Value);
            var usage = await _licenseService.GetUsageReport(organizationId.Value);

            var subscriptionDetails = new SubscriptionDetails
            {
                SubscriptionId = subscription.SubscriptionId,
                PlanType = subscription.PlanType,
                Status = subscription.Status,
                MonthlyPrice = subscription.MonthlyPrice,
                StartDate = subscription.StartDate,
                NextBillingDate = subscription.NextBillingDate,
                Limits = limits,
                CurrentUsage = usage
            };

            return Ok(subscriptionDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving subscription details for organization");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("upgrade")]
    public async Task<IActionResult> UpgradeSubscription(UpgradeRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify user has permission to upgrade (Owner/Admin only)
            var userRole = GetUserRoleFromContext();
            if (userRole != "Owner" && userRole != "Admin")
            {
                return Forbid("Only organization owners and admins can upgrade subscriptions");
            }

            var currentSubscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId.Value);
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

            _logger.LogInformation("Subscription upgraded for organization {OrganizationId} to {PlanType}",
                organizationId, request.NewPlanType);

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
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify user has permission to cancel (Owner only)
            var userRole = GetUserRoleFromContext();
            if (userRole != "Owner")
            {
                return Forbid("Only organization owners can cancel subscriptions");
            }

            var subscription = await _subscriptionRepository.GetActiveByOrganizationIdAsync(organizationId.Value);
            if (subscription == null)
            {
                return NotFound("No active subscription found");
            }

            subscription.Status = "Cancelled";
            subscription.EndDate = request.ImmediateCancel ? DateTime.UtcNow : subscription.NextBillingDate;
            subscription.AutoRenew = false;

            await _subscriptionRepository.UpdateAsync(subscription);

            _logger.LogInformation("Subscription cancelled for organization {OrganizationId}", organizationId);

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
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            string? billingPeriod = null;
            if (startDate.HasValue)
            {
                billingPeriod = startDate.Value.ToString("yyyy-MM");
            }

            var usage = await _licenseService.GetUsageReport(organizationId.Value, billingPeriod);
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
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // TODO: Implement invoice repository and get invoices by organization
            var invoices = new List<Compass.Data.Entities.Invoice>();

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

    /// <summary>
    /// Extract organization ID from JWT claims
    /// </summary>
    private Guid? GetOrganizationIdFromContext()
    {
        var organizationIdClaim = User.FindFirst("organization_id")?.Value;
        if (string.IsNullOrEmpty(organizationIdClaim))
        {
            _logger.LogWarning("Organization ID not found in JWT claims");
            return null;
        }

        if (Guid.TryParse(organizationIdClaim, out var organizationId))
        {
            return organizationId;
        }

        _logger.LogWarning("Invalid organization ID format in JWT claims: {OrganizationId}", organizationIdClaim);
        return null;
    }

    /// <summary>
    /// Extract user role from JWT claims
    /// </summary>
    private string? GetUserRoleFromContext()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
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