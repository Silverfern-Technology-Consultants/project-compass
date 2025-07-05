// Compass.Data/Entities/LoginActivity.cs
using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class LoginActivity
{
    [Key]
    public Guid LoginActivityId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public DateTime LoginTime { get; set; } = DateTime.UtcNow;

    public DateTime? LogoutTime { get; set; }

    [StringLength(45)] // IPv6 max length
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? UserAgent { get; set; }

    [StringLength(100)]
    public string? DeviceType { get; set; } // Desktop, Mobile, Tablet

    [StringLength(100)]
    public string? Browser { get; set; } // Chrome, Safari, Edge, etc.

    [StringLength(100)]
    public string? OperatingSystem { get; set; } // Windows, macOS, iOS, Android

    [StringLength(100)]
    public string? Location { get; set; } // City, State/Country (from IP geolocation)

    [StringLength(50)]
    public string SessionId { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true; // Current session indicator

    [StringLength(50)]
    public string Status { get; set; } = "Active"; // Active, Expired, Revoked

    public DateTime? LastActivityTime { get; set; }

    // Login method tracking
    [StringLength(50)]
    public string LoginMethod { get; set; } = "Password"; // Password, MFA, SSO, etc.

    public bool MfaUsed { get; set; } = false;

    // Security tracking
    public bool SuspiciousActivity { get; set; } = false;

    [StringLength(500)]
    public string? SecurityNotes { get; set; }

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;

    // Helper properties
    public TimeSpan? SessionDuration => LogoutTime.HasValue ? LogoutTime.Value - LoginTime : null;
    public bool IsCurrentSession => IsActive && Status == "Active" && LogoutTime == null;
    public string DeviceInfo => $"{Browser} on {OperatingSystem}";
    public string LocationDisplay => Location ?? "Unknown Location";
}