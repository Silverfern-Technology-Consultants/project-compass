using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class Organization
{
    [Key]
    public Guid OrganizationId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    public Guid OwnerId { get; set; } // Primary owner/admin of the organization

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Active"; // Active, Suspended, Deleted

    [StringLength(50)]
    public string OrganizationType { get; set; } = "MSP"; // MSP, MSSP, Enterprise

    // Subscription/Billing info
    public bool IsTrialOrganization { get; set; } = true;
    public DateTime? TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }

    // Settings
    public string? Settings { get; set; } // JSON blob for org-specific settings
    public string? TimeZone { get; set; }
    public string? Country { get; set; }

    // Navigation properties
    public virtual Customer Owner { get; set; } = null!;
    public virtual ICollection<Customer> Members { get; set; } = new List<Customer>();
    public virtual ICollection<TeamInvitation> TeamInvitations { get; set; } = new List<TeamInvitation>();
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public virtual ICollection<AzureEnvironment> AzureEnvironments { get; set; } = new List<AzureEnvironment>();
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    // Helper properties
    public int MemberCount => Members?.Count ?? 0;
    public bool IsActive => Status == "Active";
}