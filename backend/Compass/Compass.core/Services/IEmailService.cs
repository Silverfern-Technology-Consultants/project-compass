using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string verificationToken);
    Task SendPasswordResetEmailAsync(string email, string resetToken);
    Task SendWelcomeEmailAsync(string email, string firstName);
}

// Simple implementation for now - replace with SendGrid/etc later
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SendVerificationEmailAsync(string email, string verificationToken)
    {
        // For now, just log the verification link
        // Replace with actual email service later
        var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7163";
        var verificationUrl = $"{baseUrl}/verify-email?token={verificationToken}";

        _logger.LogInformation("Verification email for {Email}. Link: {Link}", email, verificationUrl);

        // TODO: Implement actual email sending
        await Task.CompletedTask;
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetToken)
    {
        var baseUrl = _configuration["App:BaseUrl"] ?? "https://localhost:7163";
        var resetUrl = $"{baseUrl}/reset-password?token={resetToken}";

        _logger.LogInformation("Password reset email for {Email}. Link: {Link}", email, resetUrl);

        await Task.CompletedTask;
    }

    public async Task SendWelcomeEmailAsync(string email, string firstName)
    {
        _logger.LogInformation("Welcome email sent to {Email} ({FirstName})", email, firstName);
        await Task.CompletedTask;
    }
}