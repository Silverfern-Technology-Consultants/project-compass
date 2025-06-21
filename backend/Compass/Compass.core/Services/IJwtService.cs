using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using Compass.Data.Entities;

namespace Compass.Core.Services;

public interface IJwtService
{
    string GenerateToken(Customer customer);
    string GenerateEmailVerificationToken();
    ClaimsPrincipal? ValidateToken(string token);
    Guid? GetCustomerIdFromToken(string token);
}

public class JwtService : IJwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration configuration)
    {
        _secretKey = configuration["Jwt:SecretKey"] ?? throw new ArgumentNullException("Jwt:SecretKey");
        _issuer = configuration["Jwt:Issuer"] ?? "compass-api";
        _audience = configuration["Jwt:Audience"] ?? "compass-client";
    }

    public string GenerateToken(Customer customer)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = System.Text.Encoding.ASCII.GetBytes(_secretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.CustomerId.ToString()),
            new(ClaimTypes.Email, customer.Email),
            new(ClaimTypes.Name, customer.FullName),
            new("company", customer.CompanyName),
            new("email_verified", customer.EmailVerified.ToString().ToLower())
        };

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