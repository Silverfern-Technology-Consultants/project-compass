// Compass.Api/Controllers/AccountController.cs
using Microsoft.AspNetCore.Mvc;
using Compass.Core.Services;
using Compass.Core.Models;
using Compass.Data.Repositories;
using Compass.Data.Entities;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILicenseValidationService _licenseService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        ICustomerRepository customerRepository,
        ISubscriptionRepository subscriptionRepository,
        ILicenseValidationService licenseService,
        ILogger<AccountController> logger)
    {
        _customerRepository = customerRepository;
        _subscriptionRepository = subscriptionRepository;
        _licenseService = licenseService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> RegisterAccount(RegisterAccountRequest request)
    {
        try
        {
            // Create new customer
            var customer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                CompanyName = request.CompanyName,
                FirstName = request.ContactName.Split(' ').FirstOrDefault() ?? request.ContactName,
                LastName = request.ContactName.Contains(' ') ? string.Join(" ", request.ContactName.Split(' ').Skip(1)) : "",
                Email = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                CompanySize = request.CompanySize,
                Industry = request.Industry,
                Country = request.Country,
                IsTrialAccount = true,
                TrialStartDate = DateTime.UtcNow,
                TrialEndDate = DateTime.UtcNow.AddDays(14), // 14-day trial
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                PasswordHash = "TempPassword123!" // Will be set during email verification
            };

            await _customerRepository.CreateAsync(customer);

            // Create trial subscription
            var trialSubscription = new Subscription
            {
                CustomerId = customer.CustomerId,
                PlanType = "Trial",
                Status = "Trial",
                BillingCycle = "Monthly",
                MonthlyPrice = 0,
                StartDate = DateTime.UtcNow,
                EndDate = customer.TrialEndDate,
                MaxSubscriptions = 10, // Professional trial features
                MaxAssessmentsPerMonth = 10, // Unlimited during trial
                IncludesAPI = true,
                IncludesWhiteLabel = false,
                IncludesCustomBranding = true,
                SupportLevel = "Email"
            };

            await _subscriptionRepository.CreateAsync(trialSubscription);

            _logger.LogInformation("New account registered: {CustomerEmail}, Company: {CompanyName}",
                request.ContactEmail, request.CompanyName);

            var response = new
            {
                CustomerId = customer.CustomerId,
                Message = "Account created successfully",
                TrialEndDate = customer.TrialEndDate
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering new account for {Email}", request.ContactEmail);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("profile")]
    public async Task<ActionResult<CustomerProfile>> GetProfile()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
            {
                return NotFound("Customer not found");
            }

            var subscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
            var limits = await _licenseService.GetCurrentLimits(customerId);
            var usage = await _licenseService.GetUsageReport(customerId);

            var profile = new CustomerProfile
            {
                CustomerId = customer.CustomerId,
                CompanyName = customer.CompanyName,
                ContactName = customer.FullName, // Use computed property
                ContactEmail = customer.Email,
                ContactPhone = customer.ContactPhone ?? "",
                CompanySize = customer.CompanySize ?? "",
                Industry = customer.Industry ?? "",
                Country = customer.Country ?? "",
                IsTrialAccount = customer.IsTrialAccount,
                TrialEndDate = customer.TrialEndDate,
                CurrentSubscription = subscription != null ? new SubscriptionDetails
                {
                    SubscriptionId = subscription.SubscriptionId,
                    PlanType = subscription.PlanType,
                    Status = subscription.Status,
                    MonthlyPrice = subscription.MonthlyPrice,
                    StartDate = subscription.StartDate,
                    NextBillingDate = subscription.NextBillingDate,
                    Limits = limits,
                    CurrentUsage = usage
                } : new SubscriptionDetails
                {
                    PlanType = "None",
                    Status = "Inactive",
                    MonthlyPrice = 0,
                    StartDate = DateTime.UtcNow,
                    Limits = limits,
                    CurrentUsage = usage
                }
            };

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer profile");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
            {
                return NotFound("Customer not found");
            }

            // Update customer information using writable properties
            customer.CompanyName = request.CompanyName ?? customer.CompanyName;

            // Split ContactName into FirstName and LastName if provided
            if (!string.IsNullOrEmpty(request.ContactName))
            {
                var nameParts = request.ContactName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                customer.FirstName = nameParts.FirstOrDefault() ?? customer.FirstName;
                customer.LastName = nameParts.Length > 1 ? string.Join(" ", nameParts.Skip(1)) : customer.LastName;
            }

            customer.Email = request.ContactEmail ?? customer.Email;
            customer.ContactPhone = request.ContactPhone ?? customer.ContactPhone;
            customer.Address = request.Address ?? customer.Address;
            customer.City = request.City ?? customer.City;
            customer.State = request.State ?? customer.State;
            customer.Country = request.Country ?? customer.Country;
            customer.PostalCode = request.PostalCode ?? customer.PostalCode;
            customer.CompanySize = request.CompanySize ?? customer.CompanySize;
            customer.Industry = request.Industry ?? customer.Industry;
            customer.TimeZone = request.TimeZone ?? customer.TimeZone;

            await _customerRepository.UpdateAsync(customer);

            _logger.LogInformation("Profile updated for customer {CustomerId}", customerId);

            return Ok(new { Message = "Profile updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer profile");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("trial")]
    public async Task<IActionResult> StartTrial(StartTrialRequest request)
    {
        try
        {
            var customerId = GetCustomerIdFromContext();
            var customer = await _customerRepository.GetByIdAsync(customerId);

            if (customer == null)
            {
                return NotFound("Customer not found");
            }

            if (customer.IsTrialAccount && customer.TrialEndDate > DateTime.UtcNow)
            {
                return BadRequest("Trial is already active");
            }

            // Start new trial
            customer.IsTrialAccount = true;
            customer.TrialStartDate = DateTime.UtcNow;
            customer.TrialEndDate = DateTime.UtcNow.AddDays(14);

            await _customerRepository.UpdateAsync(customer);

            // Create or update trial subscription
            var existingSubscription = await _subscriptionRepository.GetActiveByCustomerIdAsync(customerId);
            if (existingSubscription != null)
            {
                existingSubscription.Status = "Cancelled";
                await _subscriptionRepository.UpdateAsync(existingSubscription);
            }

            var trialSubscription = new Subscription
            {
                CustomerId = customerId,
                PlanType = "Trial",
                Status = "Trial",
                BillingCycle = "Monthly",
                MonthlyPrice = 0,
                StartDate = DateTime.UtcNow,
                EndDate = customer.TrialEndDate,
                MaxSubscriptions = 10,
                MaxAssessmentsPerMonth = null,
                IncludesAPI = true,
                IncludesWhiteLabel = false,
                IncludesCustomBranding = true,
                SupportLevel = "Email"
            };

            await _subscriptionRepository.CreateAsync(trialSubscription);

            _logger.LogInformation("Trial started for customer {CustomerId}", customerId);

            return Ok(new
            {
                Message = "Trial started successfully",
                TrialEndDate = customer.TrialEndDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting trial");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection()
    {
        try
        {
            var customerId = GetCustomerIdFromContext();

            // Test database connection and license validation
            var hasActiveSubscription = await _licenseService.HasActiveSubscription(customerId);
            var limits = await _licenseService.GetCurrentLimits(customerId);

            var result = new ConnectionTestResult
            {
                Success = true,
                Message = "Connection successful",
                Details = new Dictionary<string, object>
                {
                    ["HasActiveSubscription"] = hasActiveSubscription,
                    ["MaxSubscriptions"] = limits.MaxSubscriptions ?? 0,
                    ["SupportLevel"] = limits.SupportLevel,
                    ["TestTime"] = DateTime.UtcNow
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed");

            var result = new ConnectionTestResult
            {
                Success = false,
                Message = "Connection test failed",
                ErrorCode = "CONNECTION_ERROR",
                Details = new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["TestTime"] = DateTime.UtcNow
                }
            };

            return Ok(result);
        }
    }

    private Guid GetCustomerIdFromContext()
    {
        // TODO: Implement proper authentication and extract customer ID from JWT token or session
        // For now, return a placeholder - this should be replaced with actual authentication
        return Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");
    }
}

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