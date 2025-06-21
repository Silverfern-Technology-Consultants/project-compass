// Compass.Data/Entities/UsageMetric.cs
using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class UsageMetric
{
    public Guid UsageId { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid SubscriptionId { get; set; }

    [Required]
    [StringLength(50)]
    public string MetricType { get; set; } // AssessmentRun, SubscriptionCount, APICall, StorageGB

    public int MetricValue { get; set; }

    [StringLength(20)]
    public string BillingPeriod { get; set; } // 2025-01, 2025-Q1

    public DateTime RecordedDate { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Customer Customer { get; set; }
    public Subscription Subscription { get; set; }
}