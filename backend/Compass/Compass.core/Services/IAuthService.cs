using Compass.Core.Models;
using Compass.Data;
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Compass.Core.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthResponse> VerifyEmailAsync(string token);
    Task<bool> ResendVerificationEmailAsync(string email);
    Task<Customer?> GetCustomerByIdAsync(Guid customerId);
    Task<bool> IsEmailTakenAsync(string email);

    // NEW METHODS FOR MFA SUPPORT
    Task<string> GenerateJwtTokenAsync(Customer customer);
    Task InitiatePasswordResetAsync(string email);
    Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request);
}

public class AuthService : IAuthService
{
    private readonly CompassDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        CompassDbContext context,
        IJwtService jwtService,
        IEmailService emailService,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
    }

    private static string GenerateSecureToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null)
    {
        try
        {
            // Check if email already exists
            if (await IsEmailTakenAsync(request.Email))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "An account with this email already exists"
                };
            }

            // ADDED: Check if this is an invitation-based registration
            bool isInvitedUser = !string.IsNullOrEmpty(request.InvitationToken);

            _logger.LogInformation("Registration for {Email}, InvitationToken: {HasToken}",
                request.Email, isInvitedUser ? "Yes" : "No");

            // Log IP for monitoring (anti-abuse)
            if (!string.IsNullOrEmpty(ipAddress))
            {
                var recentRegistrations = await _context.Customers
                    .Where(c => c.RegistrationIP == ipAddress &&
                               c.CreatedDate > DateTime.UtcNow.AddHours(-24))
                    .CountAsync();

                _logger.LogInformation("Registration from IP {IP}, recent registrations: {Count}",
                    ipAddress, recentRegistrations);
            }

            // Generate verification token only if NOT invited
            string? verificationToken = null;
            if (!isInvitedUser)
            {
                verificationToken = GenerateSecureToken();
                _logger.LogInformation("Generated verification token for non-invited user: {Token}", verificationToken);
            }

            // Create customer
            var customer = new Customer
            {
                CompanyName = request.CompanyName,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password), // FIXED: Use actual password, not temp
                EmailVerificationToken = verificationToken,
                EmailVerificationExpiry = isInvitedUser ? null : DateTime.UtcNow.AddDays(7),
                RegistrationIP = ipAddress,
                EmailVerified = isInvitedUser, // CRITICAL: Auto-verify invited users
                IsActive = isInvitedUser // CRITICAL: Auto-activate invited users
            };

            _context.Customers.Add(customer);

            // Create trial subscription
            var trialSubscription = new Subscription
            {
                CustomerId = customer.CustomerId,
                PlanType = "Trial",
                Status = "Trial",
                MaxAssessmentsPerMonth = 5, // 5 assessments for trial
                MaxEnvironments = 2,
                SupportIncluded = true,
                TrialEndDate = DateTime.UtcNow.AddDays(14)
            };

            _context.Subscriptions.Add(trialSubscription);
            await _context.SaveChangesAsync();

            if (isInvitedUser)
            {
                _logger.LogInformation("Invited user registered and auto-verified: {Email}", customer.Email);
            }
            else
            {
                _logger.LogInformation("Customer saved to database. Verification token: {Token}", customer.EmailVerificationToken);
            }

            // Send verification email only if NOT invited
            if (!isInvitedUser && !string.IsNullOrEmpty(verificationToken))
            {
                await _emailService.SendVerificationEmailAsync(customer.Email, verificationToken);
                _logger.LogInformation("Verification email sent to non-invited user: {Email}", customer.Email);
            }

            _logger.LogInformation("Customer registered: {Email}, Company: {Company}, EmailVerified: {EmailVerified}",
                request.Email, request.CompanyName, customer.EmailVerified);

            // Return different messages based on invitation status
            var message = isInvitedUser
                ? "Account created successfully! You can now log in."
                : "Registration successful! Please check your email to verify your account.";

            return new AuthResponse
            {
                Success = true,
                Message = message,
                Customer = new CustomerInfo
                {
                    CustomerId = customer.CustomerId,
                    Email = customer.Email,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    CompanyName = customer.CompanyName,
                    EmailVerified = customer.EmailVerified,
                    SubscriptionStatus = "Trial"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed for email: {Email}", request.Email);
            return new AuthResponse
            {
                Success = false,
                Message = "Registration failed. Please try again."
            };
        }
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        try
        {
            var customer = await _context.Customers
                .Include(c => c.Subscriptions)
                .Include(c => c.Organization) // ✅ ADDED: Include Organization data
                .FirstOrDefaultAsync(c => c.Email == request.Email.ToLowerInvariant());

            if (customer == null || !BCrypt.Net.BCrypt.Verify(request.Password, customer.PasswordHash))
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid email or password"
                };
            }

            if (!customer.EmailVerified)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Please verify your email before logging in"
                };
            }

            if (!customer.IsActive)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Account is disabled"
                };
            }

            // Update login info
            customer.LastLoginDate = DateTime.UtcNow;
            customer.LastLoginIP = ipAddress;
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(customer);

            var activeSubscription = customer.Subscriptions
                .FirstOrDefault(s => s.Status == "Active" || s.Status == "Trial");

            return new AuthResponse
            {
                Success = true,
                Message = "Login successful",
                Token = token,
                Customer = new CustomerInfo
                {
                    CustomerId = customer.CustomerId,
                    Email = customer.Email,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    CompanyName = customer.CompanyName,
                    EmailVerified = customer.EmailVerified,
                    SubscriptionStatus = activeSubscription?.Status ?? "None",
                    TrialEndDate = activeSubscription?.TrialEndDate
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for email: {Email}", request.Email);
            return new AuthResponse
            {
                Success = false,
                Message = "Login failed. Please try again."
            };
        }
    }

    public async Task<AuthResponse> VerifyEmailAsync(string token)
    {
        try
        {
            _logger.LogInformation("Attempting to verify email with token: {Token}", token);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.EmailVerificationToken == token &&
                                         c.EmailVerificationExpiry > DateTime.UtcNow);

            if (customer == null)
            {
                _logger.LogWarning("No customer found with token: {Token}", token);

                // Debug: Check if token exists but is expired
                var expiredCustomer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.EmailVerificationToken == token);

                if (expiredCustomer != null)
                {
                    _logger.LogWarning("Found customer with token but it's expired. Expiry: {Expiry}, Now: {Now}",
                        expiredCustomer.EmailVerificationExpiry, DateTime.UtcNow);
                }

                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid or expired verification token"
                };
            }

            _logger.LogInformation("Found customer for verification: {Email}", customer.Email);

            customer.EmailVerified = true;
            customer.EmailVerificationToken = null;
            customer.EmailVerificationExpiry = null;
            customer.IsActive = true; // Activate account

            await _context.SaveChangesAsync();

            _logger.LogInformation("Email verified for customer: {Email}", customer.Email);

            return new AuthResponse
            {
                Success = true,
                Message = "Email verified successfully! You can now log in."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email verification failed for token: {Token}", token);
            return new AuthResponse
            {
                Success = false,
                Message = "Email verification failed"
            };
        }
    }

    public async Task<bool> ResendVerificationEmailAsync(string email)
    {
        try
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant() && !c.EmailVerified);

            if (customer == null) return false;

            customer.EmailVerificationToken = GenerateSecureToken();
            customer.EmailVerificationExpiry = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();
            await _emailService.SendVerificationEmailAsync(customer.Email, customer.EmailVerificationToken);

            _logger.LogInformation("Resent verification email to: {Email} with token: {Token}",
                email, customer.EmailVerificationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend verification email to: {Email}", email);
            return false;
        }
    }

    public async Task<Customer?> GetCustomerByIdAsync(Guid customerId)
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .Include(c => c.Organization) // ✅ ADDED: Include Organization data
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
    }

    public async Task<bool> IsEmailTakenAsync(string email)
    {
        return await _context.Customers
            .AnyAsync(c => c.Email == email.ToLowerInvariant());
    }

    // NEW METHODS FOR MFA SUPPORT
    public async Task<string> GenerateJwtTokenAsync(Customer customer)
    {
        // ✅ UPDATED: Ensure customer has Organization data loaded
        var customerWithOrg = await _context.Customers
            .Include(c => c.Organization)
            .FirstOrDefaultAsync(c => c.CustomerId == customer.CustomerId);

        return _jwtService.GenerateToken(customerWithOrg ?? customer);
    }

    public async Task InitiatePasswordResetAsync(string email)
    {
        try
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant());

            if (customer == null)
            {
                // Don't reveal if email exists - security best practice
                _logger.LogInformation("Password reset requested for non-existent email: {Email}", email);
                return;
            }

            // Generate password reset token
            var resetToken = GenerateSecureToken();
            customer.PasswordResetToken = resetToken;
            customer.PasswordResetExpiry = DateTime.UtcNow.AddHours(1); // 1 hour expiry

            await _context.SaveChangesAsync();

            // Send password reset email
            await _emailService.SendPasswordResetEmailAsync(customer.Email, resetToken);

            _logger.LogInformation("Password reset initiated for: {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate password reset for: {Email}", email);
            throw;
        }
    }

    public async Task<AuthResponse> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.PasswordResetToken == request.Token &&
                                         c.PasswordResetExpiry > DateTime.UtcNow);

            if (customer == null)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid or expired reset token"
                };
            }

            // Update password
            customer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            customer.PasswordResetToken = null;
            customer.PasswordResetExpiry = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Password reset successful for: {Email}", customer.Email);

            return new AuthResponse
            {
                Success = true,
                Message = "Password has been reset successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Password reset failed for token: {Token}", request.Token);
            return new AuthResponse
            {
                Success = false,
                Message = "Password reset failed"
            };
        }
    }
}