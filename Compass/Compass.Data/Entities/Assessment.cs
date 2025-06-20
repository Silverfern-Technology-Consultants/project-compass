using Compass.Data.Entities;

namespace Compass.Data.Entities;

public class Assessment
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public string AssessmentType { get; set; } = string.Empty; // "NamingConvention", "Tagging", "Full"
    public decimal? OverallScore { get; set; }
    public string Status { get; set; } = string.Empty; // "Pending", "InProgress", "Completed", "Failed"
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? ReportBlobUrl { get; set; }

    // Navigation property
    public virtual ICollection<AssessmentFinding> Findings { get; set; } = new List<AssessmentFinding>();
}