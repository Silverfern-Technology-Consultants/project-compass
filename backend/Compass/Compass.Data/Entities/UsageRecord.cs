using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class UsageRecord
{
    public Guid UsageRecordId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SubscriptionId { get; set; }

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [StringLength(50)]
    public string UsageType { get; set; } = string.Empty; // "Assessment", "Environment", "User"

    public Guid? AssessmentId { get; set; }
    public Guid? EnvironmentId { get; set; }

    public int Quantity { get; set; } = 1;

    [StringLength(500)]
    public string? Description { get; set; }

    public DateTime UsageDate { get; set; } = DateTime.UtcNow;
    public int BillingMonth { get; set; } // YYYYMM format
    public int BillingYear { get; set; }

    // Navigation properties
    public virtual Subscription Subscription { get; set; } = null!;
    public virtual Customer Customer { get; set; } = null!;
    public virtual Assessment? Assessment { get; set; }
    public virtual AzureEnvironment? AzureEnvironment { get; set; }
}