using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class Client
{
    [Key]
    public Guid ClientId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrganizationId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty; // Client company name

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(100)]
    public string? ContactName { get; set; }

    [EmailAddress]
    [StringLength(255)]
    public string? ContactEmail { get; set; }

    [StringLength(20)]
    public string? ContactPhone { get; set; }

    [StringLength(200)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? State { get; set; }

    [StringLength(50)]
    public string? Country { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(50)]
    public string Status { get; set; } = "Active"; // Active, Inactive, Suspended

    [StringLength(50)]
    public string? TimeZone { get; set; }

    // Client-specific settings
    public string? Settings { get; set; } // JSON blob for client-specific settings

    // Billing/Contract info
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Audit fields
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    public Guid? CreatedByCustomerId { get; set; }
    public Guid? LastModifiedByCustomerId { get; set; }

    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual Customer? CreatedBy { get; set; }
    public virtual Customer? LastModifiedBy { get; set; }

    // Child entities - this is where client isolation happens
    public virtual ICollection<AzureEnvironment> AzureEnvironments { get; set; } = new List<AzureEnvironment>();
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();

    // Client access permissions (many-to-many with Users)
    public virtual ICollection<ClientAccess> ClientAccess { get; set; } = new List<ClientAccess>();

    // Helper properties
    public string DisplayName => Name;
    public bool HasActiveContract => ContractEndDate == null || ContractEndDate > DateTime.UtcNow;
    public int TotalEnvironments => AzureEnvironments?.Count ?? 0;
    public int TotalAssessments => Assessments?.Count ?? 0;
}