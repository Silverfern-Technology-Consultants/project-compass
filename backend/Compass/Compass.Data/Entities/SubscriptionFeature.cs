// Compass.Data/Entities/SubscriptionFeature.cs
using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class SubscriptionFeature
{
    public Guid SubscriptionId { get; set; }
    public Guid FeatureId { get; set; }

    [StringLength(255)]
    public string FeatureValue { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    // Navigation properties
    public Subscription Subscription { get; set; } = null!;
    public LicenseFeature LicenseFeature { get; set; } = null!;
}