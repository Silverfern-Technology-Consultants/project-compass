using Compass.Core.Models;
using Compass.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var ipAddress = GetClientIpAddress();
            var result = await _authService.RegisterAsync(request, ipAddress);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration error");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var ipAddress = GetClientIpAddress();
            var result = await _authService.LoginAsync(request, ipAddress);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<AuthResponse>> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        try
        {
            var result = await _authService.VerifyEmailAsync(request.Token);

            if (!result.Success)
            {
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email verification error");
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "Internal server error"
            });
        }
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
    {
        try
        {
            var success = await _authService.ResendVerificationEmailAsync(request.Email);

            if (!success)
            {
                return BadRequest(new { message = "Unable to resend verification email" });
            }

            return Ok(new { message = "Verification email sent" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resend verification error");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CustomerInfo>> GetCurrentUser()
    {
        try
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null)
            {
                return Unauthorized();
            }

            var customer = await _authService.GetCustomerByIdAsync(customerId.Value);
            if (customer == null)
            {
                return NotFound();
            }

            var activeSubscription = customer.Subscriptions
                .FirstOrDefault(s => s.Status == "Active" || s.Status == "Trial");

            return Ok(new CustomerInfo
            {
                CustomerId = customer.CustomerId,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                CompanyName = customer.CompanyName,
                EmailVerified = customer.EmailVerified,
                SubscriptionStatus = activeSubscription?.Status ?? "None",
                TrialEndDate = activeSubscription?.TrialEndDate
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get current user error");
            return StatusCode(500);
        }
    }

    [HttpPost("check-email")]
    public async Task<IActionResult> CheckEmailAvailability([FromBody] CheckEmailRequest request)
    {
        try
        {
            var isTaken = await _authService.IsEmailTakenAsync(request.Email);
            return Ok(new { available = !isTaken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check email error");
            return StatusCode(500);
        }
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private Guid? GetCurrentCustomerId()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(customerIdClaim, out var customerId))
        {
            return customerId;
        }
        return null;
    }
}

public class CheckEmailRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}