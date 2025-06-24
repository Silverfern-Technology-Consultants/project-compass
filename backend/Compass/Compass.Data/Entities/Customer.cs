using System.ComponentModel.DataAnnotations;
namespace Compass.Data.Entities;
public class Customer
{
    public Guid CustomerId { get; set; } = Guid.NewGuid();

    // Organization relationship
    public Guid? OrganizationId { get; set; } // Nullable during migration
    public virtual Organization? Organization { get; set; }

    [Required]
    [StringLength(100)]
    public string CompanyName { get; set; } = string.Empty;
    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;
    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
    [Required]
    [StringLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Team/Organization role
    [StringLength(50)]
    public string Role { get; set; } = "Owner"; // Owner, Admin, Member, Viewer

    // IP tracking for anti-abuse
    public string? RegistrationIP { get; set; }
    public string? LastLoginIP { get; set; }

    // MFA Properties
    public bool IsMfaEnabled { get; set; } = false;
    public string? MfaSecret { get; set; }  // TOTP secret key
    public string? MfaBackupCodes { get; set; } // JSON array of backup codes
    public DateTime? MfaSetupDate { get; set; }
    public DateTime? LastMfaUsedDate { get; set; }
    public bool RequireMfaSetup { get; set; } = false; // Force MFA setup on next login

    // Contact info
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? CompanySize { get; set; }
    public string? Industry { get; set; }
    public string? TimeZone { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsTrialAccount { get; set; } = true;
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }

    // COMPUTED PROPERTIES (read-only)
    public string Name => $"{FirstName} {LastName}";
    public string ContactEmail => Email;
    public string ContactName => $"{FirstName} {LastName}";

    // Navigation properties - scoped to organization
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public virtual ICollection<AzureEnvironment> AzureEnvironments { get; set; } = new List<AzureEnvironment>();
    public virtual ICollection<UsageMetric> UsageMetrics { get; set; } = new List<UsageMetric>();
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    // Team invitations
    public virtual ICollection<TeamInvitation> SentInvitations { get; set; } = new List<TeamInvitation>();
    public virtual ICollection<TeamInvitation> AcceptedInvitations { get; set; } = new List<TeamInvitation>();

    // Helper properties
    public string FullName => $"{FirstName} {LastName}";
    public bool HasActiveSubscription => Subscriptions.Any(s => s.Status == "Active" || s.Status == "Trial");
    public bool IsOrganizationOwner => Role == "Owner";
    public bool CanManageTeam => Role == "Owner" || Role == "Admin";
}