namespace Compass.Core.Models
{
    public class Assessment
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public AssessmentType Type { get; set; }
        public AssessmentStatus Status { get; set; }
        public int OverallScore { get; set; }
        public DateTime StartedDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public string? ReportBlobUrl { get; set; }

        // Navigation properties
        public List<AssessmentFinding> Findings { get; set; } = new();
    }

    public enum AssessmentType
    {
        NamingConvention,
        Tagging,
        Full
    }

    public enum AssessmentStatus
    {
        InProgress,
        Completed,
        Failed
    }
}