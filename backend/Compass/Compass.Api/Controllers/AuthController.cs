using Compass.core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data; // Add this for direct context access
using Compass.Data.Entities;
using Compass.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Add this for Entity Framework
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
    private readonly CompassDbContext _context;
    private readonly LoginActivityService _loginActivityService;
    private readonly ILogger<AuthController> _logger;
    public AuthController(
        IAuthService authService,
        IMfaService mfaService,
        ICustomerRepository customerRepository,
        CompassDbContext context,
        LoginActivityService loginActivityService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _mfaService = mfaService;
        _customerRepository = customerRepository;
        _context = context;
        _loginActivityService = loginActivityService;
        _logger = logger;
    }
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] Compass.Core.Models.RegisterRequest request)
    {
        try
        {
            _logger.LogInformation("Registration attempt for email: {Email}", request.Email);
            TeamInvitation? invitation = null;
            // If invitation token is provided, validate it first
            if (!string.IsNullOrEmpty(request.InvitationToken))
            {
                invitation = await _context.TeamInvitations
                    .Include(ti => ti.Organization) // Include organization data
                    .FirstOrDefaultAsync(ti => ti.InvitationToken == request.InvitationToken &&
                                              ti.Status == "Pending" &&
                                              ti.InvitedEmail.ToLower() == request.Email.ToLower());
                if (invitation == null)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid or expired invitation"
                    });
                }
                if (invitation.ExpirationDate < DateTime.UtcNow)
                {
                    return BadRequest(new AuthResponse
                    {
                        Success = false,
                        Message = "Invitation has expired"
                    });
                }
                _logger.LogInformation("Valid invitation found for {Email} to join organization {OrganizationId} as {Role}",
                    request.Email, invitation.OrganizationId, invitation.InvitedRole);
            }
            var ipAddress = GetClientIpAddress();
            var result = await _authService.RegisterAsync(request, ipAddress);
            if (result.Success)
            {
                // If registration with invitation token, complete the invitation process
                if (invitation != null)
                {
                    // Find the newly created customer
                    var newCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.Email.ToLower() == request.Email.ToLower());
                    if (newCustomer != null)
                    {
                        // CRITICAL FIX: Set the organization and role from the invitation
                        newCustomer.OrganizationId = invitation.OrganizationId;
                        newCustomer.Role = invitation.InvitedRole;
                        // Mark invitation as accepted
                        invitation.Status = "Accepted";
                        invitation.AcceptedDate = DateTime.UtcNow;
                        invitation.AcceptedByCustomerId = newCustomer.CustomerId;
                        // Save all changes
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("User {Email} successfully joined organization {OrganizationId} as {Role}",
                            request.Email, invitation.OrganizationId, invitation.InvitedRole);
                    }
                    else
                    {
                        _logger.LogError("Could not find newly created customer {Email} to assign to organization", request.Email);
                    }
                }
                _logger.LogInformation("User registration successful: {Email}", request.Email);
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for email: {Email}", request.Email);
            return StatusCode(500, new AuthResponse
            {
                Success = false,
                Message = "Registration failed. Please try again."
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
                    RequiresEmailVerification = true,
                    Message = "Please verify your email address before logging in",
                    Customer = new CustomerDto
                    {
                        CustomerId = customer.CustomerId,
                        Email = customer.Email,
                        EmailVerified = customer.EmailVerified,
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

            // Generate session ID for tracking
            var sessionId = Guid.NewGuid().ToString();
            var ipAddress = GetClientIpAddress();
            var userAgent = Request.Headers["User-Agent"].ToString();

            // Record initial login attempt (before MFA)
            var loginActivity = await _loginActivityService.RecordLoginAsync(
                customer.CustomerId,
                ipAddress ?? "Unknown",
                userAgent,
                sessionId);

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
                        Message = "MFA code required",
                        SessionId = sessionId // Include session ID for MFA completion
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

                            // Record MFA success
                            await _loginActivityService.RecordMfaLoginAsync(customer.CustomerId, sessionId);
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

                        // Record MFA success
                        await _loginActivityService.RecordMfaLoginAsync(customer.CustomerId, sessionId);
                    }
                }

                if (!mfaValid)
                {
                    // Revoke the login session since MFA failed
                    await _loginActivityService.RecordLogoutAsync(customer.CustomerId, sessionId);

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
                    SessionId = sessionId,
                    Customer = new CustomerDto
                    {
                        CustomerId = customer.CustomerId,
                        Email = customer.Email,
                        EmailVerified = customer.EmailVerified,
                        FirstName = customer.FirstName,
                        LastName = customer.LastName,
                        CompanyName = customer.CompanyName
                    }
                });
            }

            // Update last login info
            customer.LastLoginDate = DateTime.UtcNow;
            customer.LastLoginIP = ipAddress;
            await _customerRepository.UpdateAsync(customer);

            // Generate JWT token
            var jwtToken = await _authService.GenerateJwtTokenAsync(customer);

            // Successful login
            return Ok(new LoginResponse
            {
                Success = true,
                Token = jwtToken,
                RequiresMfa = false,
                RequiresMfaSetup = false,
                Message = "Login successful",
                SessionId = sessionId, // Include session ID in response
                Customer = new CustomerDto
                {
                    CustomerId = customer.CustomerId,
                    Email = customer.Email,
                    EmailVerified = customer.EmailVerified,
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

            var id = customerId.Value; // Now safe to use

            var customer = await _context.Customers
             .Include(c => c.Organization)
             .ThenInclude(o => o.Subscriptions.Where(s => s.Status == "Active" || s.Status == "Trial"))
             .FirstOrDefaultAsync(c => c.CustomerId == id);
            if (customer == null)
            {
                return NotFound();
            }
            // Get organization-level subscription instead of user-level
            Subscription? activeSubscription = null;
            if (customer.Organization != null)
            {
                // Get the organization's active subscription
                activeSubscription = await _context.Subscriptions
                    .Include(s => s.Customer)
                    .Where(s => s.Customer.OrganizationId == customer.OrganizationId)
                    .Where(s => s.Status == "Active" || s.Status == "Trial")
                    .Where(s => s.EndDate == null || s.EndDate > DateTime.UtcNow)
                    .OrderByDescending(s => s.CreatedDate)
                    .FirstOrDefaultAsync();
            }
            return Ok(new CustomerInfo
            {
                CustomerId = customer.CustomerId,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                CompanyName = customer.CompanyName,
                EmailVerified = customer.EmailVerified,
                Role = customer.Role,
                OrganizationId = customer.OrganizationId,
                OrganizationName = customer.Organization?.Name,
                SubscriptionStatus = activeSubscription?.Status ?? "None",
                TrialEndDate = activeSubscription?.TrialEndDate
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Get current user error");
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
    [Authorize]
    public async Task<ActionResult> Logout()
    {
        try
        {
            var customerId = GetCurrentCustomerId();
            var sessionId = HttpContext.Request.Headers["X-Session-Id"].FirstOrDefault();

            if (customerId.HasValue && !string.IsNullOrEmpty(sessionId))
            {
                await _loginActivityService.RecordLogoutAsync(customerId.Value, sessionId);
            }

            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout error");
            return Ok(new { message = "Logged out successfully" }); // Don't expose errors on logout
        }
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