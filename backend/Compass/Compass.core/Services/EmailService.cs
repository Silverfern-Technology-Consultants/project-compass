using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Azure.Identity;

namespace Compass.Core.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string FrontendUrl { get; set; } = string.Empty; // Add this for frontend URLs
    public string NoReplyAddress { get; set; } = string.Empty;
    public string SupportAddress { get; set; } = string.Empty;
    public string NotificationsAddress { get; set; } = string.Empty;
}

public interface IEmailService
{
    Task SendVerificationEmailAsync(string email, string verificationToken);
    Task SendPasswordResetEmailAsync(string email, string resetToken);
    Task SendWelcomeEmailAsync(string email, string firstName);
    Task SendTeamInvitationEmailAsync(string email, string inviterName, string companyName, string inviteToken);

}

public class EmailService : IEmailService
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly GraphServiceClient? _graphServiceClient;

    public EmailService(IOptions<EmailOptions> emailOptions, ILogger<EmailService> logger, IConfiguration configuration)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;
        _configuration = configuration;

        // DEBUG: Log configuration values
        _logger.LogInformation("Email Config Debug - TenantId: {TenantId}", _emailOptions.TenantId);
        _logger.LogInformation("Email Config Debug - ClientId: {ClientId}", _emailOptions.ClientId);
        _logger.LogInformation("Email Config Debug - ClientSecret Length: {Length}",
            string.IsNullOrEmpty(_emailOptions.ClientSecret) ? 0 : _emailOptions.ClientSecret.Length);
        _logger.LogInformation("Email Config Debug - NoReplyAddress: {NoReply}", _emailOptions.NoReplyAddress);
        // Only initialize Graph client if we have proper configuration
        if (!string.IsNullOrEmpty(_emailOptions.ClientSecret) &&
            !string.IsNullOrEmpty(_emailOptions.ClientId) &&
            !string.IsNullOrEmpty(_emailOptions.TenantId))
        {
            try
            {
                // Create Graph Service Client with Client Secret Credential (modern approach)
                var credential = new ClientSecretCredential(
                    _emailOptions.TenantId,
                    _emailOptions.ClientId,
                    _emailOptions.ClientSecret
                );

                _graphServiceClient = new GraphServiceClient(credential);
                _logger.LogInformation("Microsoft Graph email service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Microsoft Graph client. Falling back to console logging.");
                _graphServiceClient = null;
            }
        }
        else
        {
            _logger.LogWarning("Email configuration incomplete. Using console logging for development.");
            _graphServiceClient = null;
        }
    }

    public async Task SendVerificationEmailAsync(string email, string verificationToken)
    {
        try
        {
            // Use frontend URL for verification links
            var frontendUrl = _configuration["App:FrontendUrl"] ?? _emailOptions.FrontendUrl ?? "http://localhost:3000";
            var verificationUrl = $"{frontendUrl}/verify-email?token={verificationToken}";
            var subject = "Verify Your Sentinel Cloud Account";
            var htmlBody = GenerateVerificationEmailTemplate(verificationUrl);

            if (_graphServiceClient != null)
            {
                await SendEmailAsync(_emailOptions.NoReplyAddress, email, subject, htmlBody);
                _logger.LogInformation("Verification email sent via Microsoft Graph to: {Email}", email);
            }
            else
            {
                // Fallback to console logging for development
                _logger.LogInformation("Verification email for {Email}. Link: {Link}", email, verificationUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to: {Email}", email);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string email, string resetToken)
    {
        try
        {
            // Use frontend URL for password reset links
            var frontendUrl = _configuration["App:FrontendUrl"] ?? _emailOptions.FrontendUrl ?? "http://localhost:3000";
            var resetUrl = $"{frontendUrl}/reset-password?token={resetToken}";
            var subject = "Reset Your Sentinel Cloud Password";
            var htmlBody = GeneratePasswordResetEmailTemplate(resetUrl);

            if (_graphServiceClient != null)
            {
                await SendEmailAsync(_emailOptions.NoReplyAddress, email, subject, htmlBody);
                _logger.LogInformation("Password reset email sent via Microsoft Graph to: {Email}", email);
            }
            else
            {
                // Fallback to console logging for development
                _logger.LogInformation("Password reset email for {Email}. Link: {Link}", email, resetUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to: {Email}", email);
            throw;
        }
    }

    public async Task SendWelcomeEmailAsync(string email, string firstName)
    {
        try
        {
            var subject = "Welcome to Sentinel Cloud!";
            var htmlBody = GenerateWelcomeEmailTemplate(firstName, "Your Company"); // Default company name

            if (_graphServiceClient != null)
            {
                await SendEmailAsync(_emailOptions.NoReplyAddress, email, subject, htmlBody);
                _logger.LogInformation("Welcome email sent via Microsoft Graph to: {Email}", email);
            }
            else
            {
                // Fallback to console logging for development
                _logger.LogInformation("Welcome email sent to {Email} ({FirstName})", email, firstName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to: {Email}", email);
            throw;
        }
    }

    public async Task SendTeamInvitationEmailAsync(string email, string inviterName, string companyName, string inviteToken)
    {
        try
        {
            // Use frontend URL for team invitation links
            var frontendUrl = _configuration["App:FrontendUrl"] ?? _emailOptions.FrontendUrl ?? "http://localhost:3000";
            var inviteUrl = $"{frontendUrl}/accept-invite?token={inviteToken}";
            var subject = $"You're Invited to Join {companyName} on Sentinel Cloud";
            var htmlBody = GenerateTeamInvitationEmailTemplate(inviterName, companyName, inviteUrl);

            if (_graphServiceClient != null)
            {
                await SendEmailAsync(_emailOptions.NoReplyAddress, email, subject, htmlBody);
                _logger.LogInformation("Team invitation email sent via Microsoft Graph to: {Email} for company: {Company}", email, companyName);
            }
            else
            {
                // Fallback to console logging for development
                _logger.LogInformation("Team invitation email for {Email} from {Inviter} at {Company}. Link: {Link}", email, inviterName, companyName, inviteUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send team invitation email to: {Email}", email);
            throw;
        }
    }

    private async Task SendEmailAsync(string fromEmail, string toEmail, string subject, string htmlBody)
    {
        if (_graphServiceClient == null)
        {
            throw new InvalidOperationException("Microsoft Graph client is not initialized");
        }

        try
        {
            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = htmlBody
                },
                ToRecipients = new List<Recipient>
                {
                    new Recipient
                    {
                        EmailAddress = new EmailAddress
                        {
                            Address = toEmail
                        }
                    }
                }
            };

            // Send email using the shared mailbox (updated API syntax)
            await _graphServiceClient.Users[fromEmail]
                .SendMail
                .PostAsync(new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
                {
                    Message = message,
                    SaveToSentItems = true
                });

            _logger.LogInformation("Email sent successfully from {From} to {To}", fromEmail, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email from {From} to {To}: {Subject}", fromEmail, toEmail, subject);
            throw;
        }
    }

    private string GenerateVerificationEmailTemplate(string verificationUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Verify Your Email</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #1a1a1a; color: #ffffff;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: #202020; border-radius: 8px; overflow: hidden;'>
        <!-- Header -->
        <div style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); padding: 40px 30px; text-align: center;'>
            <h1 style='margin: 0; color: #1a1a1a; font-size: 28px; font-weight: bold;'>Sentinel Cloud</h1>
            <p style='margin: 10px 0 0 0; color: #1a1a1a; font-size: 16px;'>Azure Governance Platform</p>
        </div>

        <!-- Content -->
        <div style='padding: 40px 30px;'>
            <h2 style='color: #c7ae6a; margin: 0 0 20px 0; font-size: 24px;'>Verify Your Email Address</h2>
            
            <p style='margin: 0 0 20px 0; line-height: 1.6; color: #cccccc;'>
                Thank you for signing up for Sentinel Cloud! To complete your registration and start analyzing your Azure governance, please verify your email address.
            </p>

            <div style='text-align: center; margin: 30px 0;'>
                <a href='{verificationUrl}' 
                   style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); 
                          color: #1a1a1a; 
                          text-decoration: none; 
                          padding: 15px 30px; 
                          border-radius: 6px; 
                          font-weight: bold; 
                          font-size: 16px; 
                          display: inline-block;'>
                    Verify Email Address
                </a>
            </div>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                If the button doesn't work, copy and paste this link into your browser:<br>
                <a href='{verificationUrl}' style='color: #c7ae6a; word-break: break-all;'>{verificationUrl}</a>
            </p>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                This verification link will expire in 7 days for security reasons.
            </p>
        </div>

        <!-- Footer -->
        <div style='background-color: #1a1a1a; padding: 20px 30px; text-align: center; border-top: 1px solid #333333;'>
            <p style='margin: 0; color: #666666; font-size: 12px;'>
                © 2025 Silverfern Technology Consultants. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";
    }

    private string GeneratePasswordResetEmailTemplate(string resetUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Reset Your Password</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #1a1a1a; color: #ffffff;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: #202020; border-radius: 8px; overflow: hidden;'>
        <!-- Header -->
        <div style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); padding: 40px 30px; text-align: center;'>
            <h1 style='margin: 0; color: #1a1a1a; font-size: 28px; font-weight: bold;'>Sentinel Cloud</h1>
            <p style='margin: 10px 0 0 0; color: #1a1a1a; font-size: 16px;'>Azure Governance Platform</p>
        </div>

        <!-- Content -->
        <div style='padding: 40px 30px;'>
            <h2 style='color: #c7ae6a; margin: 0 0 20px 0; font-size: 24px;'>Reset Your Password</h2>
            
            <p style='margin: 0 0 20px 0; line-height: 1.6; color: #cccccc;'>
                We received a request to reset your password for your Sentinel Cloud account. Click the button below to create a new password.
            </p>

            <div style='text-align: center; margin: 30px 0;'>
                <a href='{resetUrl}' 
                   style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); 
                          color: #1a1a1a; 
                          text-decoration: none; 
                          padding: 15px 30px; 
                          border-radius: 6px; 
                          font-weight: bold; 
                          font-size: 16px; 
                          display: inline-block;'>
                    Reset Password
                </a>
            </div>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                If the button doesn't work, copy and paste this link into your browser:<br>
                <a href='{resetUrl}' style='color: #c7ae6a; word-break: break-all;'>{resetUrl}</a>
            </p>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                If you didn't request this password reset, please ignore this email. This link will expire in 1 hour for security reasons.
            </p>
        </div>

        <!-- Footer -->
        <div style='background-color: #1a1a1a; padding: 20px 30px; text-align: center; border-top: 1px solid #333333;'>
            <p style='margin: 0; color: #666666; font-size: 12px;'>
                © 2025 Silverfern Technology Consultants. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateTeamInvitationEmailTemplate(string inviterName, string companyName, string inviteUrl)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Team Invitation</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #1a1a1a; color: #ffffff;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: #202020; border-radius: 8px; overflow: hidden;'>
        <!-- Header -->
        <div style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); padding: 40px 30px; text-align: center;'>
            <h1 style='margin: 0; color: #1a1a1a; font-size: 28px; font-weight: bold;'>Sentinel Cloud</h1>
            <p style='margin: 10px 0 0 0; color: #1a1a1a; font-size: 16px;'>Azure Governance Platform</p>
        </div>

        <!-- Content -->
        <div style='padding: 40px 30px;'>
            <h2 style='color: #c7ae6a; margin: 0 0 20px 0; font-size: 24px;'>You're Invited!</h2>
            
            <p style='margin: 0 0 20px 0; line-height: 1.6; color: #cccccc;'>
                <strong>{inviterName}</strong> has invited you to join <strong>{companyName}</strong> on Sentinel Cloud, our Azure governance and compliance platform.
            </p>

            <p style='margin: 0 0 20px 0; line-height: 1.6; color: #cccccc;'>
                With Sentinel Cloud, you'll be able to:
            </p>

            <ul style='margin: 0 0 20px 0; padding-left: 20px; color: #cccccc;'>
                <li>Analyze Azure resource naming conventions</li>
                <li>Monitor tagging compliance</li>
                <li>Generate governance reports</li>
                <li>Track compliance improvements</li>
            </ul>

            <div style='text-align: center; margin: 30px 0;'>
                <a href='{inviteUrl}' 
                   style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); 
                          color: #1a1a1a; 
                          text-decoration: none; 
                          padding: 15px 30px; 
                          border-radius: 6px; 
                          font-weight: bold; 
                          font-size: 16px; 
                          display: inline-block;'>
                    Accept Invitation
                </a>
            </div>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                If the button doesn't work, copy and paste this link into your browser:<br>
                <a href='{inviteUrl}' style='color: #c7ae6a; word-break: break-all;'>{inviteUrl}</a>
            </p>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                This invitation will expire in 7 days for security reasons.
            </p>
        </div>

        <!-- Footer -->
        <div style='background-color: #1a1a1a; padding: 20px 30px; text-align: center; border-top: 1px solid #333333;'>
            <p style='margin: 0; color: #666666; font-size: 12px;'>
                © 2025 Silverfern Technology Consultants. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateWelcomeEmailTemplate(string firstName, string companyName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Welcome to Sentinel Cloud</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #1a1a1a; color: #ffffff;'>
    <div style='max-width: 600px; margin: 0 auto; background-color: #202020; border-radius: 8px; overflow: hidden;'>
        <!-- Header -->
        <div style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); padding: 40px 30px; text-align: center;'>
            <h1 style='margin: 0; color: #1a1a1a; font-size: 28px; font-weight: bold;'>Sentinel Cloud</h1>
            <p style='margin: 10px 0 0 0; color: #1a1a1a; font-size: 16px;'>Azure Governance Platform</p>
        </div>

        <!-- Content -->
        <div style='padding: 40px 30px;'>
            <h2 style='color: #c7ae6a; margin: 0 0 20px 0; font-size: 24px;'>Welcome to Sentinel Cloud, {firstName}!</h2>
            
            <p style='margin: 0 0 20px 0; line-height: 1.6; color: #cccccc;'>
                Thank you for choosing Sentinel Cloud for {companyName}'s Azure governance needs. Your account is now active and ready to help you maintain compliance and optimize your cloud infrastructure.
            </p>

            <h3 style='color: #c7ae6a; margin: 20px 0 15px 0; font-size: 18px;'>Getting Started</h3>
            
            <ol style='margin: 0 0 20px 0; padding-left: 20px; color: #cccccc; line-height: 1.6;'>
                <li>Connect your Azure environment</li>
                <li>Run your first governance assessment</li>
                <li>Review naming convention compliance</li>
                <li>Analyze tagging coverage</li>
                <li>Generate your governance report</li>
            </ol>

            <div style='background-color: #1a1a1a; padding: 20px; border-radius: 6px; margin: 20px 0; border-left: 4px solid #c7ae6a;'>
                <h4 style='color: #c7ae6a; margin: 0 0 10px 0; font-size: 16px;'>Your Trial Includes:</h4>
                <ul style='margin: 0; padding-left: 20px; color: #cccccc;'>
                    <li>5 assessments per month</li>
                    <li>2 Azure environments</li>
                    <li>Full governance analysis</li>
                    <li>Email support</li>
                </ul>
            </div>

            <div style='text-align: center; margin: 30px 0;'>
                <a href='https://sentinelcloud.io/dashboard' 
                   style='background: linear-gradient(135deg, #c7ae6a 0%, #d4c17a 100%); 
                          color: #1a1a1a; 
                          text-decoration: none; 
                          padding: 15px 30px; 
                          border-radius: 6px; 
                          font-weight: bold; 
                          font-size: 16px; 
                          display: inline-block;'>
                    Start Your First Assessment
                </a>
            </div>

            <p style='margin: 20px 0 0 0; line-height: 1.6; color: #999999; font-size: 14px;'>
                Need help? Reply to this email or contact us at <a href='mailto:support@sentinelcloud.io' style='color: #c7ae6a;'>support@sentinelcloud.io</a>
            </p>
        </div>

        <!-- Footer -->
        <div style='background-color: #1a1a1a; padding: 20px 30px; text-align: center; border-top: 1px solid #333333;'>
            <p style='margin: 0; color: #666666; font-size: 12px;'>
                © 2025 Silverfern Technology Consultants. All rights reserved.
            </p>
        </div>
    </div>
</body>
</html>";
    }
}