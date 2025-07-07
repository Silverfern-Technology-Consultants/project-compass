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
public class IdentityAccessResults
{
    public decimal Score { get; set; }
    public int TotalApplications { get; set; }
    public int RiskyApplications { get; set; }
    public int InactiveUsers { get; set; }
    public int UnmanagedDevices { get; set; }
    public int OverprivilegedAssignments { get; set; }
    public ConditionalAccessCoverage ConditionalAccessCoverage { get; set; } = new();
    public List<IdentitySecurityFinding> SecurityFindings { get; set; } = new();
    public Dictionary<string, object> DetailedMetrics { get; set; } = new();
}

public class ConditionalAccessCoverage
{
    public int TotalPolicies { get; set; }
    public int EnabledPolicies { get; set; }
    public decimal CoveragePercentage { get; set; }
    public List<string> PolicyGaps { get; set; } = new();
    public List<string> ConflictingPolicies { get; set; } = new();
}
public class BusinessContinuityResults
{
    public decimal Score { get; set; }
    public BackupAnalysis BackupAnalysis { get; set; } = new();
    public DisasterRecoveryAnalysis DisasterRecoveryAnalysis { get; set; } = new();
    public List<BusinessContinuityFinding> Findings { get; set; } = new();
}

public class BackupAnalysis
{
    public int TotalResources { get; set; }
    public int BackedUpResources { get; set; }
    public decimal BackupCoveragePercentage { get; set; }
    public Dictionary<string, int> BackupStatusByResourceType { get; set; } = new();
    public List<string> UnbackedUpCriticalResources { get; set; } = new();
}
public class SecurityPostureResults
{
    public decimal Score { get; set; }
    public NetworkSecurityAnalysis NetworkSecurity { get; set; } = new();
    public DefenderForCloudAnalysis DefenderAnalysis { get; set; } = new();
    public List<SecurityFinding> SecurityFindings { get; set; } = new();
}

public class NetworkSecurityAnalysis
{
    public int NetworkSecurityGroups { get; set; }
    public int OpenToInternetRules { get; set; }
    public int OverlyPermissiveRules { get; set; }
    public List<string> HighRiskNetworkPaths { get; set; } = new();
    public Dictionary<string, int> SecurityGroupsByCompliance { get; set; } = new();
}

public class DefenderForCloudAnalysis
{
    public bool IsEnabled { get; set; }
    public decimal SecurityScore { get; set; }
    public int HighSeverityRecommendations { get; set; }
    public int MediumSeverityRecommendations { get; set; }
    public Dictionary<string, string> DefenderPlansStatus { get; set; } = new();
}

public class SecurityFinding
{
    public string Category { get; set; } = string.Empty; // "Network", "DefenderForCloud"
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string SecurityControl { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string ComplianceFramework { get; set; } = string.Empty;
}
public class DisasterRecoveryAnalysis
{
    public bool HasDisasterRecoveryPlan { get; set; }
    public int ReplicationEnabledResources { get; set; }
    public List<string> SinglePointsOfFailure { get; set; } = new();
    public Dictionary<string, string> RecoveryObjectives { get; set; } = new();
}

public class BusinessContinuityFinding
{
    public string Category { get; set; } = string.Empty; // "Backup", "DisasterRecovery"
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string BusinessImpact { get; set; } = string.Empty;
}

public class IdentitySecurityFinding
{
    public string FindingType { get; set; } = string.Empty; // "ExcessivePermissions", "StaleUser", "UnmanagedDevice", etc.
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "Critical", "High", "Medium", "Low"
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string BusinessImpact { get; set; } = string.Empty;
    public Dictionary<string, string> AdditionalData { get; set; } = new();
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

public class ResourceListResponse
{
    public List<ResourceDto> Resources { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ResourceFilters Filters { get; set; } = new();
}

public class ResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ResourceTypeName { get; set; } = string.Empty;
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? SubscriptionId { get; set; }
    public string? Kind { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public int TagCount { get; set; }
    public string? Environment { get; set; }
    public string? Sku { get; set; }
}

public class ResourceFilters
{
    public Dictionary<string, int> ResourceTypes { get; set; } = new();
    public Dictionary<string, int> ResourceGroups { get; set; } = new();
    public Dictionary<string, int> Locations { get; set; } = new();
    public Dictionary<string, int> Environments { get; set; } = new();
}

public class ResourceGroupSummary
{
    public string Name { get; set; } = string.Empty;
    public int ResourceCount { get; set; }
    public List<string> Locations { get; set; } = new();
    public Dictionary<string, int> ResourceTypes { get; set; } = new();
}

public class ResourceTypeSummary
{
    public string Type { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> Examples { get; set; } = new();
    public List<string> ResourceGroups { get; set; } = new();
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

// Dependency Analysis Models
public class DependencyAnalysisResults
{
    public int TotalResources { get; set; }
    public List<VirtualMachineDependency> VirtualMachineDependencies { get; set; } = new();
    public List<NetworkDependency> NetworkDependencies { get; set; } = new();
    public List<StorageDependency> StorageDependencies { get; set; } = new();
    public List<DatabaseDependency> DatabaseDependencies { get; set; } = new();
    public ResourceGroupAnalysis ResourceGroupAnalysis { get; set; } = new();
    public NetworkTopology NetworkTopology { get; set; } = new();
    public EnvironmentSeparationAnalysis EnvironmentSeparation { get; set; } = new();
}

public class VirtualMachineDependency
{
    public AzureResource VirtualMachine { get; set; } = new();
    public List<AzureResource> NetworkInterfaces { get; set; } = new();
    public List<AzureResource> PublicIPs { get; set; } = new();
    public List<AzureResource> NetworkSecurityGroups { get; set; } = new();
    public List<AzureResource> VirtualNetworks { get; set; } = new();
    public List<AzureResource> ManagedDisks { get; set; } = new();

    public string DependencyChain => BuildDependencyChain();

    private string BuildDependencyChain()
    {
        var chain = $"VM: {VirtualMachine.Name}";
        if (NetworkInterfaces.Any())
            chain += $" → NIC: {string.Join(", ", NetworkInterfaces.Select(n => n.Name))}";
        if (PublicIPs.Any())
            chain += $" → Public IP: {string.Join(", ", PublicIPs.Select(p => p.Name))}";
        if (NetworkSecurityGroups.Any())
            chain += $" → NSG: {string.Join(", ", NetworkSecurityGroups.Select(n => n.Name))}";
        if (VirtualNetworks.Any())
            chain += $" → VNet: {string.Join(", ", VirtualNetworks.Select(v => v.Name))}";
        return chain;
    }
}

public class NetworkDependency
{
    public AzureResource VirtualNetwork { get; set; } = new();
    public List<string> Subnets { get; set; } = new();
    public List<AzureResource> NetworkSecurityGroups { get; set; } = new();
    public List<AzureResource> NetworkInterfaces { get; set; } = new();
    public List<AzureResource> VirtualNetworkGateways { get; set; } = new();
    public List<AzureResource> ConnectedResources { get; set; } = new();
}

public class StorageDependency
{
    public AzureResource StorageAccount { get; set; } = new();
    public List<AzureResource> AssociatedVMs { get; set; } = new();
    public List<AzureResource> Disks { get; set; } = new();
    public List<AzureResource> PrivateEndpoints { get; set; } = new();
}

public class DatabaseDependency
{
    public AzureResource DatabaseServer { get; set; } = new();
    public List<AzureResource> Databases { get; set; } = new();
    public List<AzureResource> PrivateEndpoints { get; set; } = new();
    public List<AzureResource> ConnectedApplications { get; set; } = new();
}

public class ResourceGroupAnalysis
{
    public int TotalResourceGroups { get; set; }
    public decimal AverageResourcesPerGroup { get; set; }
    public List<ResourceGroupStats> ResourceGroups { get; set; } = new();
}

public class ResourceGroupStats
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public int ResourceCount { get; set; }
    public Dictionary<string, int> ResourceTypes { get; set; } = new();
    public Dictionary<string, int> NamingPatterns { get; set; } = new();
    public string PrimaryPurpose { get; set; } = string.Empty;
}

public class NetworkTopology
{
    public int VirtualNetworkCount { get; set; }
    public int NetworkGatewayCount { get; set; }
    public int PublicIPCount { get; set; }
    public string TopologyType { get; set; } = string.Empty;
    public List<NetworkSegment> NetworkSegments { get; set; } = new();
}

public class NetworkSegment
{
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public int ConnectedResourceCount { get; set; }
    public bool HasGateway { get; set; }
    public Dictionary<string, int> ResourceTypeDistribution { get; set; } = new();
}

public class EnvironmentSeparationAnalysis
{
    public bool HasProperSeparation { get; set; }
    public Dictionary<string, int> EnvironmentDistribution { get; set; } = new();
    public List<EnvironmentMixingIssue> MixedEnvironmentNetworks { get; set; } = new();
}

public class EnvironmentMixingIssue
{
    public string NetworkName { get; set; } = string.Empty;
    public List<string> DetectedEnvironments { get; set; } = new();
    public List<string> AffectedResources { get; set; } = new();
    public string RiskLevel { get; set; } = string.Empty;
}

// CLIENT PREFERENCES MODELS
public class ClientPreferences
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public bool IsActive { get; set; }

    public OrganizationalStructurePreferences OrganizationalStructure { get; set; } = new();
    public NamingConventionStrategy NamingStrategy { get; set; } = new();
    public TaggingStrategy TaggingStrategy { get; set; } = new();
    public GovernancePreferences Governance { get; set; } = new();
    public ImplementationPreferences Implementation { get; set; } = new();
}

public class OrganizationalStructurePreferences
{
    public List<string> EnvironmentSeparationMethods { get; set; } = new();
    public string EnvironmentIsolationLevel { get; set; } = string.Empty;
    public List<string> ComplianceRequirements { get; set; } = new();
    public string PrimaryResourceOrganizationMethod { get; set; } = string.Empty;
}

public class NamingConventionStrategy
{
    public List<string> PreferredNamingStyles { get; set; } = new();
    public List<string> RequiredNameElements { get; set; } = new();
    public bool HasAutomationRequirements { get; set; }
    public bool IncludeEnvironmentIndicator { get; set; }
}

public class TaggingStrategy
{
    public List<string> RequiredTags { get; set; } = new();
    public Dictionary<string, List<string>> TagValueStandards { get; set; } = new();
    public bool EnforceTagCompliance { get; set; }
}

public class GovernancePreferences
{
    public string AccessControlGranularity { get; set; } = string.Empty;
    public List<string> ComplianceFrameworks { get; set; } = new();
}

public class ImplementationPreferences
{
    public string MigrationStrategy { get; set; } = string.Empty;
    public string RiskTolerance { get; set; } = string.Empty;
}

public class ClientAssessmentConfiguration
{
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    // Legacy naming preferences (for backward compatibility)
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; } = false;

    // Enhanced naming preferences
    public string? NamingStyle { get; set; } // 'standardized', 'mixed', 'legacy'
    public string? EnvironmentSize { get; set; } // 'small', 'medium', 'large', 'enterprise'
    public string? OrganizationMethod { get; set; } // 'environment', 'application', 'business-unit'
    public string? EnvironmentIndicatorLevel { get; set; } // 'required', 'recommended', 'optional', 'none'

    // Legacy tagging preferences (for backward compatibility)
    public List<string> RequiredTags { get; set; } = new();
    public bool EnforceTagCompliance { get; set; } = true;

    // Enhanced tagging preferences
    public string? TaggingApproach { get; set; } // 'comprehensive', 'basic', 'minimal', 'custom'
    public List<string> SelectedTags { get; set; } = new(); // Combined standard + custom tags
    public List<string> CustomTags { get; set; } = new(); // User-defined custom tags

    // Compliance preferences
    public List<string> ComplianceFrameworks { get; set; } = new(); // Legacy
    public List<string> SelectedCompliances { get; set; } = new(); // Enhanced
    public bool NoSpecificRequirements { get; set; } = false;

    // Legacy properties (keep for backward compatibility)
    public Guid CustomerId { get; set; } // Legacy - use ClientId instead
    public List<string> NamingConventions { get; set; } = new(); // Legacy - use AllowedNamingPatterns
    public bool EnvironmentSeparationRequired { get; set; } // Legacy - use EnvironmentIndicators
    public DateTime ConfigurationDate { get; set; } = DateTime.UtcNow;

    // Helper methods
    public bool HasNamingPreferences => AllowedNamingPatterns.Any() || RequiredNamingElements.Any() || !string.IsNullOrEmpty(NamingStyle);
    public bool HasTaggingPreferences => RequiredTags.Any() || SelectedTags.Any() || !string.IsNullOrEmpty(TaggingApproach);
    public bool HasCompliancePreferences => ComplianceFrameworks.Any() || SelectedCompliances.Any();

    /// <summary>
    /// Get effective required tags combining legacy and enhanced settings
    /// </summary>
    public List<string> GetEffectiveRequiredTags()
    {
        var effectiveTags = new List<string>();

        // Add legacy required tags
        effectiveTags.AddRange(RequiredTags);

        // Add enhanced selected tags
        effectiveTags.AddRange(SelectedTags);

        // Add custom tags
        effectiveTags.AddRange(CustomTags);

        return effectiveTags.Distinct().ToList();
    }

    /// <summary>
    /// Get effective naming patterns combining legacy and enhanced settings
    /// </summary>
    public List<string> GetEffectiveNamingPatterns()
    {
        var effectivePatterns = new List<string>();

        // Add explicit allowed patterns
        effectivePatterns.AddRange(AllowedNamingPatterns);

        // Add legacy naming conventions
        effectivePatterns.AddRange(NamingConventions);

        // Infer patterns from naming style
        if (!string.IsNullOrEmpty(NamingStyle))
        {
            switch (NamingStyle.ToLowerInvariant())
            {
                case "standardized":
                    effectivePatterns.AddRange(new[] { "Kebab-case", "Lowercase" });
                    break;
                case "legacy":
                    effectivePatterns.AddRange(new[] { "Other", "Uppercase", "Lowercase" });
                    break;
                case "mixed":
                    // Allow multiple patterns for mixed environments
                    break;
            }
        }

        return effectivePatterns.Distinct().ToList();
    }

    /// <summary>
    /// Check if environment indicators are required based on preferences
    /// </summary>
    public bool AreEnvironmentIndicatorsRequired()
    {
        return EnvironmentIndicators ||
               EnvironmentSeparationRequired || // Legacy
               EnvironmentIndicatorLevel == "required" ||
               RequiredNamingElements.Contains("Environment indicator");
    }

    /// <summary>
    /// Get the strictness level for compliance checking
    /// </summary>
    public string GetComplianceStrictnessLevel()
    {
        if (NoSpecificRequirements) return "none";
        if (SelectedCompliances.Any(c => c.Contains("SOC") || c.Contains("PCI") || c.Contains("HIPAA"))) return "high";
        if (ComplianceFrameworks.Any(c => c.Contains("SOC") || c.Contains("PCI") || c.Contains("HIPAA"))) return "high";
        if (SelectedCompliances.Any() || ComplianceFrameworks.Any()) return "medium";
        return "low";
    }

    /// <summary>
    /// Get tagging enforcement level based on client preferences
    /// </summary>
    public string GetTaggingEnforcementLevel()
    {
        if (!EnforceTagCompliance) return "none";

        return TaggingApproach?.ToLowerInvariant() switch
        {
            "comprehensive" => "strict",
            "basic" => "moderate",
            "minimal" => "light",
            "custom" => "custom",
            _ => "moderate"
        };
    }

    /// <summary>
    /// Get expected environment patterns based on organization method
    /// </summary>
    public List<string> GetExpectedEnvironmentPatterns()
    {
        var patterns = new List<string>();

        if (AreEnvironmentIndicatorsRequired())
        {
            switch (OrganizationMethod?.ToLowerInvariant())
            {
                case "environment":
                    patterns.AddRange(new[] { "dev", "test", "staging", "prod", "production" });
                    break;
                case "application":
                    patterns.AddRange(new[] { "app", "web", "api", "db", "cache" });
                    break;
                case "business-unit":
                    patterns.AddRange(new[] { "hr", "finance", "ops", "sales", "marketing" });
                    break;
                default:
                    patterns.AddRange(new[] { "dev", "test", "prod" });
                    break;
            }
        }

        return patterns;
    }

    /// <summary>
    /// Get severity adjustment for violations based on client preferences
    /// </summary>
    public string AdjustViolationSeverity(string baseSeverity, string violationType)
    {
        // Increase severity for client preference violations
        if (violationType.Contains("ClientPreference") || violationType.Contains("RequiredElement"))
        {
            return baseSeverity switch
            {
                "Low" => "Medium",
                "Medium" => "High",
                "High" => "Critical",
                _ => baseSeverity
            };
        }

        // Adjust based on compliance strictness
        var strictnessLevel = GetComplianceStrictnessLevel();
        if (strictnessLevel == "high" && (violationType.Contains("Tag") || violationType.Contains("Naming")))
        {
            return baseSeverity switch
            {
                "Low" => "Medium",
                "Medium" => "High",
                _ => baseSeverity
            };
        }

        return baseSeverity;
    }

    /// <summary>
    /// Generate client-specific recommendations based on preferences
    /// </summary>
    public List<string> GenerateClientSpecificRecommendations(string category)
    {
        var recommendations = new List<string>();

        switch (category.ToLowerInvariant())
        {
            case "naming":
            case "namingconvention":
                if (HasNamingPreferences)
                {
                    var patterns = GetEffectiveNamingPatterns();
                    if (patterns.Any())
                    {
                        recommendations.Add($"Apply client-preferred naming patterns: {string.Join(", ", patterns)}");
                    }

                    if (AreEnvironmentIndicatorsRequired())
                    {
                        var envPatterns = GetExpectedEnvironmentPatterns();
                        recommendations.Add($"Include environment indicators: {string.Join(", ", envPatterns)}");
                    }
                }
                break;

            case "tagging":
                if (HasTaggingPreferences)
                {
                    var requiredTags = GetEffectiveRequiredTags();
                    if (requiredTags.Any())
                    {
                        recommendations.Add($"Apply client-required tags: {string.Join(", ", requiredTags.Take(5))}");
                    }

                    var enforcementLevel = GetTaggingEnforcementLevel();
                    if (enforcementLevel != "none")
                    {
                        recommendations.Add($"Follow {enforcementLevel} tagging enforcement as per client preferences");
                    }
                }
                break;

            case "compliance":
                if (HasCompliancePreferences && !NoSpecificRequirements)
                {
                    if (SelectedCompliances.Any() || ComplianceFrameworks.Any())
                    {
                        var allCompliances = SelectedCompliances.Concat(ComplianceFrameworks).Distinct();
                        recommendations.Add($"Ensure compliance with: {string.Join(", ", allCompliances)}");
                    }

                    var strictness = GetComplianceStrictnessLevel();
                    recommendations.Add($"Apply {strictness} compliance standards as specified by client");
                }
                break;
        }

        return recommendations;
    }
}