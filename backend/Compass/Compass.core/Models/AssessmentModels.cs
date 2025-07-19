using Compass.Core.Models.Assessment;

namespace Compass.Core.Models;

public class AssessmentOptions
{
    public bool AnalyzeNamingConventions { get; set; } = true;
    public bool AnalyzeTagging { get; set; } = true;
    public bool IncludeRecommendations { get; set; } = true;
    public string[]? ResourceTypesToInclude { get; set; }
    public string[]? ResourceTypesToExclude { get; set; }

    // NEW: IAM-specific options
    public bool AnalyzeEnterpriseApplications { get; set; } = false;
    public bool AnalyzeStaleUsers { get; set; } = false;
    public bool AnalyzeResourceIam { get; set; } = false;
    public bool AnalyzeConditionalAccess { get; set; } = false;

    // NEW: Business Continuity options
    public bool AnalyzeBackupCoverage { get; set; } = false;
    public bool AnalyzeRecoveryConfiguration { get; set; } = false;

    // NEW: Security options
    public bool AnalyzeNetworkSecurity { get; set; } = false;
    public bool AnalyzeDefenderForCloud { get; set; } = false;
}

public enum AssessmentCategory
{
    ResourceGovernance,
    IdentityAccessManagement,
    BusinessContinuity,
    SecurityPosture
}

public enum AssessmentType
{
    // Resource Governance
    NamingConvention,
    Tagging,
    GovernanceFull,

    // Identity & Access Management  
    EnterpriseApplications,
    StaleUsersDevices,
    ResourceIamRbac,
    ConditionalAccess,
    IdentityFull,

    // Business Continuity & Disaster Recovery
    BackupCoverage,
    RecoveryConfiguration,
    BusinessContinuityFull,

    // Security Posture
    NetworkSecurity,
    DefenderForCloud,
    SecurityFull,

    // Legacy (for backward compatibility)
    Full // Maps to GovernanceFull
}

public static class AssessmentModelStructure
{
    public static readonly Dictionary<AssessmentCategory, List<AssessmentType>> CategoryModels = new()
    {
        [AssessmentCategory.ResourceGovernance] = new()
        {
            AssessmentType.NamingConvention,
            AssessmentType.Tagging,
            AssessmentType.GovernanceFull
        },
        [AssessmentCategory.IdentityAccessManagement] = new()
        {
            AssessmentType.EnterpriseApplications,
            AssessmentType.StaleUsersDevices,
            AssessmentType.ResourceIamRbac,
            AssessmentType.ConditionalAccess,
            AssessmentType.IdentityFull
        },
        [AssessmentCategory.BusinessContinuity] = new()
        {
            AssessmentType.BackupCoverage,
            AssessmentType.RecoveryConfiguration,
            AssessmentType.BusinessContinuityFull
        },
        [AssessmentCategory.SecurityPosture] = new()
        {
            AssessmentType.NetworkSecurity,
            AssessmentType.DefenderForCloud,
            AssessmentType.SecurityFull
        }
    };

    public static readonly Dictionary<AssessmentType, AssessmentCategory> ModelToCategory =
        CategoryModels.SelectMany(kvp => kvp.Value.Select(model => new { Model = model, Category = kvp.Key }))
                     .ToDictionary(x => x.Model, x => x.Category);

    public static readonly Dictionary<AssessmentType, string> ModelDescriptions = new()
    {
        // Resource Governance
        [AssessmentType.NamingConvention] = "Analyze resource naming patterns and consistency",
        [AssessmentType.Tagging] = "Evaluate tagging strategy and compliance",
        [AssessmentType.GovernanceFull] = "Comprehensive governance assessment (naming + tagging)",

        // Identity & Access Management
        [AssessmentType.EnterpriseApplications] = "Review enterprise applications and app registrations for security risks",
        [AssessmentType.StaleUsersDevices] = "Identify inactive users and unmanaged devices",
        [AssessmentType.ResourceIamRbac] = "Analyze role assignments and permissions at resource level",
        [AssessmentType.ConditionalAccess] = "Evaluate conditional access policies and coverage",
        [AssessmentType.IdentityFull] = "Complete identity and access management security assessment",

        // Business Continuity & Disaster Recovery
        [AssessmentType.BackupCoverage] = "Analyze backup configuration and success rates",
        [AssessmentType.RecoveryConfiguration] = "Review disaster recovery setup and procedures",
        [AssessmentType.BusinessContinuityFull] = "Comprehensive business continuity and disaster recovery assessment",

        // Security Posture
        [AssessmentType.NetworkSecurity] = "Evaluate network security configuration and controls",
        [AssessmentType.DefenderForCloud] = "Review Microsoft Defender for Cloud security posture",
        [AssessmentType.SecurityFull] = "Complete security posture assessment",

        // Legacy
        [AssessmentType.Full] = "Legacy full assessment (maps to governance assessment)"
    };

    public static AssessmentCategory GetCategory(AssessmentType type)
    {
        // Handle legacy mapping
        if (type == AssessmentType.Full)
            return AssessmentCategory.ResourceGovernance;

        return ModelToCategory.TryGetValue(type, out var category) ? category : AssessmentCategory.ResourceGovernance;
    }

    public static List<AssessmentType> GetModelsForCategory(AssessmentCategory category)
    {
        return CategoryModels.TryGetValue(category, out var models) ? models : new List<AssessmentType>();
    }

    public static string GetDescription(AssessmentType type)
    {
        return ModelDescriptions.TryGetValue(type, out var description) ? description : "Assessment model";
    }

    public static bool IsFullAssessment(AssessmentType type)
    {
        return type == AssessmentType.GovernanceFull ||
               type == AssessmentType.IdentityFull ||
               type == AssessmentType.BusinessContinuityFull ||
               type == AssessmentType.SecurityFull ||
               type == AssessmentType.Full;
    }

    public static List<AssessmentType> GetSubModelsForFull(AssessmentType fullType)
    {
        return fullType switch
        {
            AssessmentType.GovernanceFull or AssessmentType.Full => new() { AssessmentType.NamingConvention, AssessmentType.Tagging },
            AssessmentType.IdentityFull => new() { AssessmentType.EnterpriseApplications, AssessmentType.StaleUsersDevices, AssessmentType.ResourceIamRbac, AssessmentType.ConditionalAccess },
            AssessmentType.BusinessContinuityFull => new() { AssessmentType.BackupCoverage, AssessmentType.RecoveryConfiguration },
            AssessmentType.SecurityFull => new() { AssessmentType.NetworkSecurity, AssessmentType.DefenderForCloud },
            _ => new List<AssessmentType>()
        };
    }
}

public class AssessmentResult
{
    public Guid AssessmentId { get; set; }
    public Guid EnvironmentId { get; set; }
    public AssessmentType Type { get; set; }
    public AssessmentCategory Category { get; set; } // NEW
    public AssessmentStatus Status { get; set; }
    public decimal OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? ErrorMessage { get; set; }

    // Existing results
    public NamingConventionResults? NamingResults { get; set; }
    public TaggingResults? TaggingResults { get; set; }

    // NEW: Enhanced results
    public IdentityAccessResults? IdentityResults { get; set; }
    public BusinessContinuityResults? BusinessContinuityResults { get; set; }
    public SecurityPostureResults? SecurityResults { get; set; }

    public List<AssessmentRecommendation> Recommendations { get; set; } = new();
    public int TotalResourcesAnalyzed { get; set; }
    public int IssuesFound { get; set; }
    public DependencyAnalysisResults? DependencyAnalysis { get; set; }
    public Dictionary<string, object> DetailedMetrics { get; set; } = new();
}

public enum AssessmentStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}