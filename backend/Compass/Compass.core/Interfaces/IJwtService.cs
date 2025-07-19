using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Compass.Data.Entities;
using Compass.Data;
using Microsoft.EntityFrameworkCore;

namespace Compass.core.Interfaces;

public interface IJwtService
{
    string GenerateToken(Customer customer);
    Task<string> GenerateTokenAsync(Customer customer); // Add async version
    string GenerateEmailVerificationToken();
    ClaimsPrincipal? ValidateToken(string token);
    Guid? GetCustomerIdFromToken(string token);
}

public class JwtService : IJwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly CompassDbContext _context;

    public JwtService(IConfiguration configuration, CompassDbContext context)
    {
        _secretKey = configuration["jwt-secret-key"] ?? throw new ArgumentNullException("jwt-secret-key");
        _issuer = configuration["Jwt:Issuer"] ?? "compass-api";
        _audience = configuration["Jwt:Audience"] ?? "compass-client";
        _context = context;
    }

    public string GenerateToken(Customer customer)
    {
        // For backward compatibility, call async version synchronously
        return GenerateTokenAsync(customer).GetAwaiter().GetResult();
    }

    public async Task<string> GenerateTokenAsync(Customer customer)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes(_secretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
            new(ClaimTypes.Email, customer.Email),
            new(ClaimTypes.Name, customer.FullName),
            new("given_name", customer.FirstName),
            new("family_name", customer.LastName),
            new("company_name", customer.CompanyName),
            new("email_verified", customer.EmailVerified.ToString().ToLower())
        };

        // Add Organization claims if customer has an organization
        if (customer.OrganizationId.HasValue)
        {
            claims.Add(new("organization_id", customer.OrganizationId.Value.ToString()));
            claims.Add(new(ClaimTypes.Role, customer.Role));

            // ✅ NEW: Get organization data including subscription status
            var organization = await _context.Organizations
                .Include(o => o.Subscriptions.Where(s => s.Status == "Active" || s.Status == "Trial"))
                .FirstOrDefaultAsync(o => o.OrganizationId == customer.OrganizationId.Value);

            if (organization != null)
            {
                claims.Add(new("organization_name", organization.Name));

                // ✅ CRITICAL: Add organization-level subscription status to JWT
                var activeSubscription = await _context.Subscriptions
                    .Include(s => s.Customer)
                    .Where(s => s.Customer.OrganizationId == customer.OrganizationId.Value)
                    .Where(s => s.Status == "Active" || s.Status == "Trial")
                    .Where(s => s.EndDate == null || s.EndDate > DateTime.UtcNow)
                    .OrderByDescending(s => s.CreatedDate)
                    .FirstOrDefaultAsync();

                var subscriptionStatus = activeSubscription?.Status ?? "None";
                claims.Add(new("subscription_status", subscriptionStatus));

                if (activeSubscription?.TrialEndDate.HasValue == true)
                {
                    claims.Add(new("trial_end_date", activeSubscription.TrialEndDate.Value.ToString("yyyy-MM-dd")));
                }
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7), // 7-day expiry
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public string GenerateEmailVerificationToken()
    {
        return Guid.NewGuid().ToString("N")[..16]; // 16-character token
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = System.Text.Encoding.ASCII.GetBytes(_secretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _issuer,
                ValidateAudience = true,
                ValidAudience = _audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public Guid? GetCustomerIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        var customerIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(customerIdClaim, out var customerId))
            return customerId;

        return null;
    }
}