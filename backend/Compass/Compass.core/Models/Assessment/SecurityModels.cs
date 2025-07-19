namespace Compass.Core.Models.Assessment;

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