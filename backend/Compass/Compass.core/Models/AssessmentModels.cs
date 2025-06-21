namespace Compass.Core.Models;

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

    // Enhanced properties
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
    public Guid CustomerId { get; set; }
    public List<string> RequiredTags { get; set; } = new();
    public List<string> NamingConventions { get; set; } = new();
    public List<string> ComplianceFrameworks { get; set; } = new();
    public bool EnvironmentSeparationRequired { get; set; }
    public DateTime ConfigurationDate { get; set; }

    // Missing properties that PreferenceAwareNamingAnalyzer expects:
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; }
}