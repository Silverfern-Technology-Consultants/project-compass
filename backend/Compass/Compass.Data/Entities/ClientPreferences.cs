using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class ClientPreferences
{
    [Key]
    public Guid ClientPreferencesId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public Guid OrganizationId { get; set; }

    // Naming Strategy
    public string? AllowedNamingPatterns { get; set; } // JSON array - ["Kebab-case", "Lowercase"]
    public string? RequiredNamingElements { get; set; } // JSON array - ["Environment indicator", "Resource type prefix"]
    public bool EnvironmentIndicators { get; set; } = false;

    // Tagging Strategy
    public string? RequiredTags { get; set; } // JSON array - ["Environment", "Owner", "Project"]
    public bool EnforceTagCompliance { get; set; } = true;

    // Governance Preferences
    public string? ComplianceFrameworks { get; set; } // JSON array - ["SOC2", "PCI DSS", "HIPAA"]

    // Audit fields
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByCustomerId { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public Guid? LastModifiedByCustomerId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Client Client { get; set; } = null!;
    public virtual Organization Organization { get; set; } = null!;
    public virtual Customer? CreatedBy { get; set; }
    public virtual Customer? LastModifiedBy { get; set; }
}