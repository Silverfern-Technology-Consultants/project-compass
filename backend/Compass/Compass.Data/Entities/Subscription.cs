using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Compass.Data.Entities;

public class Subscription
{
    public Guid SubscriptionId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [StringLength(50)]
    public string PlanType { get; set; } = string.Empty; // "Trial", "Basic", "Professional", "Enterprise"

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = string.Empty; // "Active", "Cancelled", "Expired", "Trial"

    [StringLength(20)]
    public string? BillingCycle { get; set; } // "Monthly", "Yearly"

    [Column(TypeName = "decimal(10,2)")]
    public decimal MonthlyPrice { get; set; } = 0m; // Changed to non-nullable with default

    [Column(TypeName = "decimal(10,2)")]
    public decimal? AnnualPrice { get; set; }

    public int? MaxAssessmentsPerMonth { get; set; }
    public int? MaxEnvironments { get; set; }
    public int? MaxSubscriptions { get; set; }
    public int? MaxUsersPerEnvironment { get; set; }

    public bool SupportIncluded { get; set; } = false;
    public bool PrioritySupport { get; set; } = false;
    public bool CustomReporting { get; set; } = false;
    public bool ApiAccess { get; set; } = false;
    public bool IncludesAPI { get; set; } = false;
    public bool IncludesWhiteLabel { get; set; } = false;
    public bool IncludesCustomBranding { get; set; } = false;
    public bool AutoRenew { get; set; } = true; // ADDED MISSING PROPERTY

    [StringLength(50)]
    public string SupportLevel { get; set; } = "Email";

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public DateTime? TrialEndDate { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastBillingDate { get; set; }
    public DateTime? NextBillingDate { get; set; }

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual ICollection<UsageRecord> UsageRecords { get; set; } = new List<UsageRecord>();
    public virtual ICollection<UsageMetric> UsageMetrics { get; set; } = new List<UsageMetric>();
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public virtual ICollection<SubscriptionFeature> SubscriptionFeatures { get; set; } = new List<SubscriptionFeature>();
}