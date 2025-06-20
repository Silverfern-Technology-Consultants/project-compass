namespace Compass.Data.Entities;

public class AssessmentFinding
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string Category { get; set; } = string.Empty; // "NamingConvention", "Tagging"
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "Critical", "High", "Medium", "Low"
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? EstimatedEffort { get; set; }

    // Navigation property
    public virtual Assessment Assessment { get; set; } = null!;
}