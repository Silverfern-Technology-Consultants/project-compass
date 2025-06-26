using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

/// <summary>
/// Junction table for User-Client access permissions
/// Allows MSP staff to have different access levels to different clients
/// </summary>
public class ClientAccess
{
    [Key]
    public Guid ClientAccessId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid ClientId { get; set; }

    [Required]
    [StringLength(50)]
    public string AccessLevel { get; set; } = "Read"; // Read, Write, Admin, None

    public bool CanViewAssessments { get; set; } = true;
    public bool CanCreateAssessments { get; set; } = false;
    public bool CanDeleteAssessments { get; set; } = false;
    public bool CanManageEnvironments { get; set; } = false;
    public bool CanViewReports { get; set; } = true;
    public bool CanExportData { get; set; } = false;

    // Audit fields
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    public Guid? GrantedByCustomerId { get; set; }

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual Client Client { get; set; } = null!;
    public virtual Customer? GrantedBy { get; set; }

    // Helper methods
    public bool HasWriteAccess => AccessLevel == "Write" || AccessLevel == "Admin";
    public bool HasAdminAccess => AccessLevel == "Admin";
    public bool HasReadAccess => AccessLevel != "None";
}