// Compass.Core/Services/LoginActivityService.cs
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Compass.Core.Services;

public class LoginActivityService
{
    private readonly ILoginActivityRepository _loginActivityRepository;
    private readonly ILogger<LoginActivityService> _logger;

    public LoginActivityService(
        ILoginActivityRepository loginActivityRepository,
        ILogger<LoginActivityService> logger)
    {
        _loginActivityRepository = loginActivityRepository;
        _logger = logger;
    }

    public async Task<LoginActivity> RecordLoginAsync(Guid customerId, string ipAddress, string userAgent, string sessionId)
    {
        try
        {
            var deviceInfo = ParseUserAgent(userAgent);
            var location = await GetLocationFromIpAsync(ipAddress);

            var loginActivity = new LoginActivity
            {
                CustomerId = customerId,
                LoginTime = DateTime.UtcNow,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceType = deviceInfo.DeviceType,
                Browser = deviceInfo.Browser,
                OperatingSystem = deviceInfo.OperatingSystem,
                Location = location,
                SessionId = sessionId,
                IsActive = true,
                Status = "Active",
                LastActivityTime = DateTime.UtcNow,
                LoginMethod = "Password",
                MfaUsed = false
            };

            // Check for suspicious activity
            await CheckSuspiciousActivityAsync(loginActivity);

            var result = await _loginActivityRepository.CreateAsync(loginActivity);

            _logger.LogInformation("Login recorded for customer {CustomerId} from {IpAddress} using {DeviceDescription}",
                customerId, ipAddress, deviceInfo.DeviceDescription);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record login for customer {CustomerId}", customerId);
            throw;
        }
    }

    public async Task<bool> RecordMfaLoginAsync(Guid customerId, string sessionId)
    {
        try
        {
            var session = await _loginActivityRepository.GetActiveSessionAsync(customerId, sessionId);
            if (session == null) return false;

            session.MfaUsed = true;
            session.LoginMethod = "MFA";
            session.LastActivityTime = DateTime.UtcNow;

            await _loginActivityRepository.UpdateAsync(session);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record MFA login for customer {CustomerId}", customerId);
            return false;
        }
    }

    public async Task<bool> RecordLogoutAsync(Guid customerId, string sessionId)
    {
        try
        {
            var session = await _loginActivityRepository.GetActiveSessionAsync(customerId, sessionId);
            if (session == null) return false;

            session.LogoutTime = DateTime.UtcNow;
            session.IsActive = false;
            session.Status = "Logged Out";

            await _loginActivityRepository.UpdateAsync(session);

            _logger.LogInformation("Logout recorded for customer {CustomerId} session {SessionId}",
                customerId, sessionId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record logout for customer {CustomerId}", customerId);
            return false;
        }
    }

    public async Task<List<LoginActivity>> GetLoginHistoryAsync(Guid customerId, int days = 30)
    {
        return await _loginActivityRepository.GetCustomerLoginHistoryAsync(customerId, days);
    }

    public async Task<List<LoginActivity>> GetActiveSessionsAsync(Guid customerId)
    {
        return await _loginActivityRepository.GetActiveSessionsAsync(customerId);
    }

    public async Task<bool> RevokeSessionAsync(Guid loginActivityId)
    {
        return await _loginActivityRepository.RevokeSessionAsync(loginActivityId);
    }

    public async Task<bool> RevokeAllOtherSessionsAsync(Guid customerId, string currentSessionId)
    {
        // Find the current session ID by session string
        var currentSession = await _loginActivityRepository.GetActiveSessionAsync(customerId, currentSessionId);
        var currentSessionGuid = currentSession?.LoginActivityId;

        return await _loginActivityRepository.RevokeAllSessionsAsync(customerId, currentSessionGuid);
    }

    public async Task<bool> UpdateActivityAsync(Guid customerId, string sessionId)
    {
        try
        {
            var session = await _loginActivityRepository.GetActiveSessionAsync(customerId, sessionId);
            if (session == null) return false;

            session.LastActivityTime = DateTime.UtcNow;
            await _loginActivityRepository.UpdateAsync(session);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update activity for customer {CustomerId}", customerId);
            return false;
        }
    }

    private DeviceInfo ParseUserAgent(string userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
        {
            return new DeviceInfo
            {
                DeviceType = "Unknown",
                Browser = "Unknown",
                OperatingSystem = "Unknown",
                DeviceDescription = "Unknown Device" // Changed from DeviceInfo to DeviceDescription
            };
        }

        var deviceType = "Desktop";
        var browser = "Unknown";
        var operatingSystem = "Unknown";

        // Detect mobile/tablet
        if (Regex.IsMatch(userAgent, @"Mobile|Android|iPhone|iPad", RegexOptions.IgnoreCase))
        {
            if (userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
                deviceType = "Tablet";
            else
                deviceType = "Mobile";
        }

        // Detect browser
        if (userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            browser = "Chrome";
        else if (userAgent.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
            browser = "Firefox";
        else if (userAgent.Contains("Safari", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase))
            browser = "Safari";
        else if (userAgent.Contains("Edg", StringComparison.OrdinalIgnoreCase))
            browser = "Edge";
        else if (userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
            browser = "Opera";

        // Detect OS
        if (userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase))
            operatingSystem = "Windows";
        else if (userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase))
            operatingSystem = "macOS";
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
            operatingSystem = "Linux";
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
            operatingSystem = "Android";
        else if (userAgent.Contains("iOS", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
            operatingSystem = "iOS";

        return new DeviceInfo
        {
            DeviceType = deviceType,
            Browser = browser,
            OperatingSystem = operatingSystem,
            DeviceDescription = $"{browser} on {operatingSystem}" // Changed from DeviceInfo to DeviceDescription
        };
    }

    private async Task<string> GetLocationFromIpAsync(string ipAddress)
    {
        // Simplified location detection
        // In production, you might want to use a real IP geolocation service
        try
        {
            if (string.IsNullOrEmpty(ipAddress) || ipAddress.StartsWith("127.") || ipAddress.StartsWith("::1"))
                return "Local Network";

            if (ipAddress.StartsWith("192.168.") || ipAddress.StartsWith("10.") || ipAddress.StartsWith("172."))
                return "Private Network";

            // Placeholder - in production, integrate with IP geolocation service
            await Task.Delay(1); // Simulate async call
            return "Unknown Location";
        }
        catch
        {
            return "Unknown Location";
        }
    }

    private async Task CheckSuspiciousActivityAsync(LoginActivity loginActivity)
    {
        try
        {
            // Check for multiple login attempts from different locations
            var recentLogins = await _loginActivityRepository.GetCustomerLoginHistoryAsync(
                loginActivity.CustomerId, 1);

            if (recentLogins.Any())
            {
                var lastLogin = recentLogins.First();

                // Flag if location changed dramatically in short time
                if (lastLogin.Location != loginActivity.Location &&
                    DateTime.UtcNow.Subtract(lastLogin.LoginTime).TotalHours < 2)
                {
                    loginActivity.SuspiciousActivity = true;
                    loginActivity.SecurityNotes = "Location change detected";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check suspicious activity for customer {CustomerId}",
                loginActivity.CustomerId);
        }
    }

    public async Task<int> CleanupOldSessionsAsync(int daysOld = 90)
    {
        return await _loginActivityRepository.CleanupExpiredSessionsAsync(daysOld);
    }
}

public class DeviceInfo
{
    public string DeviceType { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public string DeviceDescription { get; set; } = string.Empty; // Changed from DeviceInfo to DeviceDescription
}