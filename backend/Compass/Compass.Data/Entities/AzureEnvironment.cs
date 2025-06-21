using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class AzureEnvironment
{
    public Guid AzureEnvironmentId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }

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

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual ICollection<Assessment> Assessments { get; set; } = new List<Assessment>();
}