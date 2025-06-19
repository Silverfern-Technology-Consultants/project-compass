namespace Compass.Core.Models
{
    public class AssessmentFinding
    {
        public Guid Id { get; set; }
        public Guid AssessmentId { get; set; }
        public FindingCategory Category { get; set; }
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public FindingSeverity Severity { get; set; }
        public string Issue { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
        public string? EstimatedEffort { get; set; }
        public FindingStatus Status { get; set; }
        public DateTime CreatedDate { get; set; }

        // Navigation property
        public Assessment Assessment { get; set; } = null!;
    }

    public enum FindingCategory
    {
        NamingConvention,
        TagCoverage,
        TagQuality,
        Security,
        Cost
    }

    public enum FindingSeverity
    {
        Critical,
        High,
        Medium,
        Low
    }

    public enum FindingStatus
    {
        New,
        InProgress,
        Resolved,
        Ignored
    }
}