namespace Compass.Core.Models;

public class AssessmentStartResponse
{
    public Guid AssessmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class AssessmentStatusResponse
{
    public Guid AssessmentId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int Progress { get; set; }
}

public class AssessmentSummary
{
    public Guid AssessmentId { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int TotalResourcesAnalyzed { get; set; }
    public int IssuesFound { get; set; }
}