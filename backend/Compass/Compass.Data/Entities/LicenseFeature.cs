﻿// Compass.Data/Entities/LicenseFeature.cs
using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class LicenseFeature
{
    public Guid FeatureId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(100)]
    public string FeatureName { get; set; } = string.Empty;

    [StringLength(500)]
    public string FeatureDescription { get; set; } = string.Empty;

    [StringLength(50)]
    public string FeatureType { get; set; } = string.Empty;

    [StringLength(255)]
    public string DefaultValue { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<SubscriptionFeature> SubscriptionFeatures { get; set; } = new List<SubscriptionFeature>();
}