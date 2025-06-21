// Compass.Data/Entities/LicenseFeature.cs
using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class LicenseFeature
{
    public Guid FeatureId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string FeatureName { get; set; }

    [StringLength(500)]
    public string FeatureDescription { get; set; }

    [StringLength(50)]
    public string FeatureType { get; set; } // Limit, Toggle, Value

    [StringLength(255)]
    public string DefaultValue { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<SubscriptionFeature> SubscriptionFeatures { get; set; } = new List<SubscriptionFeature>();
}