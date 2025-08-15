using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class AzureEnvironment
{
    public Guid AzureEnvironmentId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }

    // NEW: Client scoping for MSP isolation
    public Guid? ClientId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    public List<string> SubscriptionIds { get; set; } = new List<string>();

    [StringLength(36)]
    public string? ServicePrincipalId { get; set; }

    [StringLength(100)]
    public string? ServicePrincipalName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessDate { get; set; }

    // Connection test results
    public bool? LastConnectionTest { get; set; }
    public DateTime? LastConnectionTestDate { get; set; }
    public string? LastConnectionError { get; set; }

    // NEW: Cost Management permission tracking
    public bool HasCostManagementAccess { get; set; } = false;
    public DateTime? CostManagementLastChecked { get; set; }
    public string? CostManagementSetupStatus { get; set; } // "NotTested", "SetupRequired", "Ready", "Error"
    public string? CostManagementLastError { get; set; }

    // NEW: Detailed permission tracking
    public string? AvailablePermissions { get; set; } // JSON array of available APIs
    public string? MissingPermissions { get; set; } // JSON array of missing permissions

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual Client? Client { get; set; } // NEW: Client navigation
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}