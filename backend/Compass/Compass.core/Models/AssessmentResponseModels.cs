namespace Compass.Core.Models;

// Enhanced to include resolved environment and client information
public class AssessmentStartResponse
{
    public Guid AssessmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    // NEW: Additional context information resolved by the API
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int SubscriptionCount { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

public class AssessmentSummary
{
    public Guid AssessmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int TotalResourcesAnalyzed { get; set; }
    public int IssuesFound { get; set; }

    // NEW: Client context
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
}