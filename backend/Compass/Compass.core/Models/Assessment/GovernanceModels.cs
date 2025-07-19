namespace Compass.Core.Models.Assessment;

public class NamingConventionResults
{
    public decimal Score { get; set; }
    public int TotalResources { get; set; }
    public int CompliantResources { get; set; }
    public Dictionary<string, NamingPatternAnalysis> PatternsByResourceType { get; set; } = new();
    public List<NamingViolation> Violations { get; set; } = new();
    public NamingConsistencyMetrics Consistency { get; set; } = new();

    // Enhanced properties
    public Dictionary<string, NamingPatternStats> PatternDistribution { get; set; } = new();
    public EnvironmentIndicatorAnalysis EnvironmentIndicators { get; set; } = new();
    public Dictionary<string, List<string>> RepresentativeExamples { get; set; } = new();
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

    // Enhanced properties
    public Dictionary<string, int> PatternDistribution { get; set; } = new();
}

public class NamingConsistencyMetrics
{
    public decimal OverallConsistency { get; set; }
    public bool UsesEnvironmentPrefixes { get; set; }
    public bool UsesResourceTypePrefixes { get; set; }
    public bool UsesConsistentSeparators { get; set; }
    public string? PrimarySeparator { get; set; }
    public decimal ClientPreferenceCompliance { get; set; }
    public List<string> InconsistentPatterns { get; set; } = new();

    // Enhanced properties
    public string? DominantPattern { get; set; }
    public decimal DominantPatternPercentage { get; set; }
    public decimal ResourceTypePrefixPercentage { get; set; }
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

// Enhanced Naming Convention Models
public class NamingPatternStats
{
    public string Pattern { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
    public List<string> Examples { get; set; } = new();
}

public class EnvironmentIndicatorAnalysis
{
    public int ResourcesWithEnvironmentIndicators { get; set; }
    public bool MeetsClientRequirements { get; set; }
    public decimal PercentageWithEnvironmentIndicators { get; set; }
    public Dictionary<string, int> EnvironmentDistribution { get; set; } = new();
}