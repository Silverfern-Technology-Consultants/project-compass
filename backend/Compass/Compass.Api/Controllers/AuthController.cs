using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IMfaService _mfaService;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IMfaService mfaService,
        ICustomerRepository customerRepository,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _mfaService = mfaService;
        _customerRepository = customerRepository;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] Compass.Core.Models.RegisterRequest request)
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
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginWithMfaRequest request)
    {
        try
        {
            // First, validate email and password
            var customer = await _customerRepository.GetByEmailAsync(request.Email);
            if (customer == null || !BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
            {
                return Ok(new LoginResponse
                {
                    Success = false,
                    Message = "Invalid email or password"
                });
            }

            if (!customer.EmailVerified)
            {
                return Ok(new LoginResponse
                {
                    Success = false,
                    RequiresEmailVerification = true, // ✅ Add explicit flag
                    Message = "Please verify your email address before logging in",
                    Customer = new CustomerDto
                    {
                        CustomerId = customer.CustomerId,
                        Email = customer.Email,
                        EmailVerified = customer.EmailVerified, // ✅ Include EmailVerified
                        FirstName = customer.FirstName,
                        LastName = customer.LastName,
                        CompanyName = customer.CompanyName
                    }
                });
            }

            if (!customer.IsActive)
            {
                return Ok(new LoginResponse
                {
                    Success = false,
                    Message = "Account is deactivated"
                });
            }

            // Check if MFA is required
            if (customer.IsMfaEnabled && !string.IsNullOrEmpty(customer.MfaSecret))
            {
                // MFA is enabled, check if token provided
                if (string.IsNullOrEmpty(request.MfaToken))
                {
                    return Ok(new LoginResponse
                    {
                        Success = false,
                        RequiresMfa = true,
                        Message = "MFA code required"
                    });
                }

                // Validate MFA token
                bool mfaValid = false;
                if (request.IsBackupCode)
                {
                    if (!string.IsNullOrEmpty(customer.MfaBackupCodes))
                    {
                        var backupCodes = JsonSerializer.Deserialize<List<string>>(customer.MfaBackupCodes) ?? new List<string>();
                        mfaValid = _mfaService.ValidateBackupCode(request.MfaToken, backupCodes);

                        if (mfaValid)
                        {
                            // Remove used backup code
                            backupCodes.Remove(request.MfaToken.Trim().ToLower());
                            customer.MfaBackupCodes = JsonSerializer.Serialize(backupCodes);
                            customer.LastMfaUsedDate = DateTime.UtcNow;
                            await _customerRepository.UpdateAsync(customer);
                        }
                    }
                }
                else
                {
                    mfaValid = _mfaService.ValidateTotp(customer.MfaSecret, request.MfaToken);
                    if (mfaValid)
                    {
                        customer.LastMfaUsedDate = DateTime.UtcNow;
                        await _customerRepository.UpdateAsync(customer);
                    }
                }

                if (!mfaValid)
                {
                    return Ok(new LoginResponse
                    {
                        Success = false,
                        RequiresMfa = true,
                        Message = "Invalid MFA code"
                    });
                }
            }

            // Check if MFA setup is required
            if (customer.RequireMfaSetup)
            {
                var token = await _authService.GenerateJwtTokenAsync(customer);
                return Ok(new LoginResponse
                {
                    Success = true,
                    Token = token,
                    RequiresMfaSetup = true,
                    Message = "MFA setup required",
                    Customer = new CustomerDto
                    {
                        CustomerId = customer.CustomerId,
                        Email = customer.Email,
                        EmailVerified = customer.EmailVerified, // ✅ Include EmailVerified
                        FirstName = customer.FirstName,
                        LastName = customer.LastName,
                        CompanyName = customer.CompanyName
                    }
                });
            }

            // Update last login info
            var ipAddress = GetClientIpAddress();
            customer.LastLoginDate = DateTime.UtcNow;
            customer.LastLoginIP = ipAddress;
            await _customerRepository.UpdateAsync(customer);

            // Generate JWT token
            var jwtToken = await _authService.GenerateJwtTokenAsync(customer);

            // ✅ CRITICAL FIX: Include EmailVerified in successful login response
            return Ok(new LoginResponse
            {
                Success = true,
                Token = jwtToken,
                RequiresMfa = false,
                RequiresMfaSetup = false,
                Message = "Login successful",
                Customer = new CustomerDto
                {
                    CustomerId = customer.CustomerId,
                    Email = customer.Email,
                    EmailVerified = customer.EmailVerified, // ✅ This was missing!
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    CompanyName = customer.CompanyName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for {Email}", request.Email);
            return StatusCode(500, new LoginResponse
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

    [HttpPost("logout")]
    public ActionResult Logout()
    {
        // Since we're using stateless JWT, logout is handled client-side
        // In a production app, you might want to maintain a token blacklist
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult> ForgotPassword([FromBody] Compass.Core.Models.ForgotPasswordRequest request)
    {
        try
        {
            await _authService.InitiatePasswordResetAsync(request.Email);
            return Ok(new { message = "If an account with that email exists, password reset instructions have been sent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset initiation");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] Compass.Core.Models.ResetPasswordRequest request)
    {
        try
        {
            var result = await _authService.ResetPasswordAsync(request);
            if (result.Success)
            {
                return Ok(new { message = "Password has been reset successfully" });
            }
            return BadRequest(new { message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset");
            return StatusCode(500, "An error occurred while resetting your password");
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