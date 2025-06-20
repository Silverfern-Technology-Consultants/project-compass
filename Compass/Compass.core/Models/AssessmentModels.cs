namespace Compass.Core.Models;

public class AssessmentRequest
{
    public Guid EnvironmentId { get; set; }
    public string[] SubscriptionIds { get; set; } = Array.Empty<string>();
    public AssessmentType Type { get; set; } = AssessmentType.Full;
    public AssessmentOptions? Options { get; set; }
}

public class AssessmentOptions
{
    public bool AnalyzeNamingConventions { get; set; } = true;
    public bool AnalyzeTagging { get; set; } = true;
    public bool IncludeRecommendations { get; set; } = true;
    public string[]? ResourceTypesToInclude { get; set; }
    public string[]? ResourceTypesToExclude { get; set; }
}

public enum AssessmentType
{
    NamingConvention,
    Tagging,
    Full
}

public class AssessmentResult
{
    public Guid AssessmentId { get; set; }
    public Guid EnvironmentId { get; set; }
    public AssessmentType Type { get; set; }
    public AssessmentStatus Status { get; set; }
    public decimal OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? ErrorMessage { get; set; }

    public NamingConventionResults? NamingResults { get; set; }
    public TaggingResults? TaggingResults { get; set; }
    public List<AssessmentRecommendation> Recommendations { get; set; } = new();

    public int TotalResourcesAnalyzed { get; set; }
    public int IssuesFound { get; set; }
}

public enum AssessmentStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public class NamingConventionResults
{
    public decimal Score { get; set; }
    public int TotalResources { get; set; }
    public int CompliantResources { get; set; }
    public Dictionary<string, NamingPatternAnalysis> PatternsByResourceType { get; set; } = new();
    public List<NamingViolation> Violations { get; set; } = new();
    public NamingConsistencyMetrics Consistency { get; set; } = new();
}

public class TaggingResults
{
    public decimal Score { get; set; }
    public int TotalResources { get; set; }
    public int TaggedResources { get; set; }
    public decimal TagCoveragePercentage { get; set; }
    public Dictionary<string, int> TagUsageFrequency { get; set; } = new();
    public List<string> MissingRequiredTags { get; set; } = new();
    public List<TaggingViolation> Violations { get; set; } = new();
}

public class NamingPatternAnalysis
{
    public string ResourceType { get; set; } = string.Empty;
    public List<string> DetectedPatterns { get; set; } = new();
    public string? MostCommonPattern { get; set; }
    public decimal ConsistencyScore { get; set; }
    public int TotalResources { get; set; }
    public int PatternCompliantResources { get; set; }
}

public class NamingConsistencyMetrics
{
    public decimal OverallConsistency { get; set; }
    public bool UsesEnvironmentPrefixes { get; set; }
    public bool UsesResourceTypePrefixes { get; set; }
    public bool UsesConsistentSeparators { get; set; }
    public string? PrimarySeparator { get; set; }
    public List<string> InconsistentPatterns { get; set; } = new();
}

public class NamingViolation
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
}

public class TaggingViolation
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ViolationType { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public List<string> MissingTags { get; set; } = new();
    public string Severity { get; set; } = string.Empty;
}

public class AssessmentRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string EstimatedEffort { get; set; } = string.Empty;
    public List<string> AffectedResources { get; set; } = new();
    public string? ActionPlan { get; set; }
}