// Compass.Core/Models/LoginActivityModels.cs
namespace Compass.Core.Models;

public class LoginActivityDto
{
    public Guid LoginActivityId { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime? LogoutTime { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceType { get; set; }
    public string? Browser { get; set; }
    public string? OperatingSystem { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastActivityTime { get; set; }
    public string LoginMethod { get; set; } = string.Empty;
    public bool MfaUsed { get; set; }
    public bool SuspiciousActivity { get; set; }
    public string? SecurityNotes { get; set; }

    // Computed properties
    public string DeviceInfo { get; set; } = string.Empty;
    public string LocationDisplay { get; set; } = string.Empty;
    public bool IsCurrentSession { get; set; }
    public TimeSpan? SessionDuration { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
}

public class LoginHistoryRequest
{
    public int Days { get; set; } = 7;
    public bool IncludeSuspicious { get; set; } = false;
}

public class LoginHistoryResponse
{
    public List<LoginActivityDto> LoginHistory { get; set; } = new();
    public List<LoginActivityDto> ActiveSessions { get; set; } = new();
    public LoginStatistics Statistics { get; set; } = new();
}

public class LoginStatistics
{
    public int TotalLogins { get; set; }
    public int UniqueLocations { get; set; }
    public int UniqueBrowsers { get; set; }
    public int SuspiciousActivities { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime? FirstLogin { get; set; }
    public List<string> TopLocations { get; set; } = new();
    public List<string> TopBrowsers { get; set; } = new();
}

public class RevokeSessionRequest
{
    public Guid LoginActivityId { get; set; }
}

public class RevokeAllSessionsRequest
{
    public bool ExceptCurrent { get; set; } = true;
}