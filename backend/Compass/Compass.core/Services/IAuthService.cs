using Compass.Core.Models;
using Compass.Data;
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, string? ipAddress = null);
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<AuthResponse> VerifyEmailAsync(string token);
    Task<bool> ResendVerificationEmailAsync(string email);
    Task<Customer?> GetCustomerByIdAsync(Guid customerId);
    Task<bool> IsEmailTakenAsync(string email);
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

            // Create customer
            var customer = new Customer
            {
                CompanyName = request.CompanyName,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email.ToLowerInvariant(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TempPassword123!"), // Temporary
                EmailVerificationToken = _jwtService.GenerateEmailVerificationToken(),
                EmailVerificationExpiry = DateTime.UtcNow.AddDays(7),
                RegistrationIP = ipAddress,
                EmailVerified = false,
                IsActive = false // Inactive until email verified
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

            // Send verification email
            await _emailService.SendVerificationEmailAsync(customer.Email,
                customer.EmailVerificationToken!);

            _logger.LogInformation("New customer registered: {Email}, Company: {Company}",
                request.Email, request.CompanyName);

            return new AuthResponse
            {
                Success = true,
                Message = "Registration successful! Please check your email to verify your account.",
                Customer = new CustomerInfo
                {
                    CustomerId = customer.CustomerId,
                    Email = customer.Email,
                    FirstName = customer.FirstName,
                    LastName = customer.LastName,
                    CompanyName = customer.CompanyName,
                    EmailVerified = false,
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
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.EmailVerificationToken == token &&
                                         c.EmailVerificationExpiry > DateTime.UtcNow);

            if (customer == null)
            {
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid or expired verification token"
                };
            }

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

            customer.EmailVerificationToken = _jwtService.GenerateEmailVerificationToken();
            customer.EmailVerificationExpiry = DateTime.UtcNow.AddDays(7);

            await _context.SaveChangesAsync();
            await _emailService.SendVerificationEmailAsync(customer.Email, customer.EmailVerificationToken);

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
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
    }

    public async Task<bool> IsEmailTakenAsync(string email)
    {
        return await _context.Customers
            .AnyAsync(c => c.Email == email.ToLowerInvariant());
    }
}