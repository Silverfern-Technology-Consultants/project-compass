using Compass.Core.Interfaces;
using Compass.Core.Models.Assessment;
using Compass.Core.Services.BusinessContinuity;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services.BusinessContinuity;

public interface IBusinessContinuityFullAnalyzer
{
    Task<BusinessContinuityResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default);

    Task<BusinessContinuityResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

public class BusinessContinuityFullAnalyzer : IBusinessContinuityFullAnalyzer
{
    private readonly IBackupCoverageAnalyzer _backupCoverageAnalyzer;
    private readonly IRecoveryConfigurationAnalyzer _recoveryConfigurationAnalyzer;
    private readonly ILogger<BusinessContinuityFullAnalyzer> _logger;

    public BusinessContinuityFullAnalyzer(
        IBackupCoverageAnalyzer backupCoverageAnalyzer,
        IRecoveryConfigurationAnalyzer recoveryConfigurationAnalyzer,
        ILogger<BusinessContinuityFullAnalyzer> logger)
    {
        _backupCoverageAnalyzer = backupCoverageAnalyzer;
        _recoveryConfigurationAnalyzer = recoveryConfigurationAnalyzer;
        _logger = logger;
    }

    public async Task<BusinessContinuityResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive Business Continuity analysis for {SubscriptionCount} subscriptions", subscriptionIds.Length);

        try
        {
            // Run both backup and recovery analyses in parallel
            var backupTask = _backupCoverageAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);
            var recoveryTask = _recoveryConfigurationAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);

            await Task.WhenAll(backupTask, recoveryTask);

            var backupResults = await backupTask;
            var recoveryResults = await recoveryTask;

            // Combine results into comprehensive assessment
            var combinedResults = CombineResults(backupResults, recoveryResults);

            _logger.LogInformation("Comprehensive Business Continuity analysis completed. Overall Score: {Score}%, " +
                                 "Backup Coverage: {BackupCoverage}%, DR Plan: {HasDRPlan}",
                combinedResults.Score, backupResults.BackupAnalysis.BackupCoveragePercentage,
                recoveryResults.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan);

            return combinedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete comprehensive Business Continuity analysis for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<BusinessContinuityResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled comprehensive Business Continuity analysis for client {ClientId}", clientId);

        try
        {
            // Run both backup and recovery analyses with OAuth in parallel
            var backupTask = _backupCoverageAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);
            var recoveryTask = _recoveryConfigurationAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);

            await Task.WhenAll(backupTask, recoveryTask);

            var backupResults = await backupTask;
            var recoveryResults = await recoveryTask;

            // Combine results into comprehensive assessment
            var combinedResults = CombineResults(backupResults, recoveryResults);

            _logger.LogInformation("OAuth-enabled comprehensive Business Continuity analysis completed for client {ClientId}. " +
                                 "Overall Score: {Score}%, Backup Coverage: {BackupCoverage}%, DR Plan: {HasDRPlan}",
                clientId, combinedResults.Score, backupResults.BackupAnalysis.BackupCoveragePercentage,
                recoveryResults.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan);

            return combinedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete OAuth-enabled comprehensive Business Continuity analysis for client {ClientId}", clientId);

            // Fall back to standard analysis
            _logger.LogInformation("Falling back to standard comprehensive Business Continuity analysis for client {ClientId}", clientId);
            return await AnalyzeAsync(subscriptionIds, cancellationToken);
        }
    }

    private BusinessContinuityResults CombineResults(BusinessContinuityResults backupResults, BusinessContinuityResults recoveryResults)
    {
        var combinedResults = new BusinessContinuityResults
        {
            // Combine backup analysis data
            BackupAnalysis = backupResults.BackupAnalysis,

            // Combine disaster recovery analysis data
            DisasterRecoveryAnalysis = recoveryResults.DisasterRecoveryAnalysis,

            // Merge all findings
            Findings = backupResults.Findings.Concat(recoveryResults.Findings).ToList()
        };

        // Calculate comprehensive score using weighted methodology
        combinedResults.Score = CalculateComprehensiveScore(backupResults, recoveryResults, combinedResults.Findings);

        // Add cross-domain analysis findings
        AddCrossDomainAnalysisFindings(combinedResults);

        // Enhance analysis with business impact assessment
        EnhanceWithBusinessImpactAnalysis(combinedResults);

        return combinedResults;
    }

    private decimal CalculateComprehensiveScore(BusinessContinuityResults backupResults, BusinessContinuityResults recoveryResults, List<BusinessContinuityFinding> allFindings)
    {
        // Weighted scoring: 60% backup coverage, 40% recovery configuration
        var backupWeight = 0.60m;
        var recoveryWeight = 0.40m;

        var weightedScore = (backupResults.Score * backupWeight) + (recoveryResults.Score * recoveryWeight);

        // Apply cross-domain penalties
        var crossDomainPenalties = CalculateCrossDomainPenalties(backupResults, recoveryResults);

        // Apply critical finding penalty
        var criticalFindings = allFindings.Count(f => f.Severity == "High");
        var mediumFindings = allFindings.Count(f => f.Severity == "Medium");
        var findingPenalty = (criticalFindings * 5) + (mediumFindings * 2);

        var finalScore = Math.Max(0, weightedScore - crossDomainPenalties - findingPenalty);

        _logger.LogInformation("Comprehensive BCDR Score: Backup {BackupScore}% (weight {BackupWeight}), " +
                             "Recovery {RecoveryScore}% (weight {RecoveryWeight}), " +
                             "Cross-domain penalty {CrossPenalty}, Finding penalty {FindingPenalty}, Final {FinalScore}%",
            backupResults.Score, backupWeight, recoveryResults.Score, recoveryWeight,
            crossDomainPenalties, findingPenalty, finalScore);

        return Math.Round(finalScore, 2);
    }

    private decimal CalculateCrossDomainPenalties(BusinessContinuityResults backupResults, BusinessContinuityResults recoveryResults)
    {
        decimal penalty = 0;

        // Penalty if backup coverage is low AND no DR plan exists
        if (backupResults.BackupAnalysis.BackupCoveragePercentage < 50 && !recoveryResults.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan)
        {
            penalty += 15; // Major penalty for both backup and DR gaps
        }

        // Penalty if good backup coverage but no DR testing capability
        if (backupResults.BackupAnalysis.BackupCoveragePercentage > 80 && recoveryResults.DisasterRecoveryAnalysis.ReplicationEnabledResources == 0)
        {
            penalty += 10; // Penalty for imbalanced BCDR strategy
        }

        // Penalty for inconsistent regional strategy
        var hasMultiRegionDR = recoveryResults.DisasterRecoveryAnalysis.RecoveryObjectives.ContainsKey("MultiRegionDeployment") &&
                              recoveryResults.DisasterRecoveryAnalysis.RecoveryObjectives["MultiRegionDeployment"] == "Yes";

        if (hasMultiRegionDR && backupResults.BackupAnalysis.BackupCoveragePercentage < 70)
        {
            penalty += 8; // Penalty for multi-region deployment without adequate backup coverage
        }

        return penalty;
    }

    private void AddCrossDomainAnalysisFindings(BusinessContinuityResults combinedResults)
    {
        var backupCoverage = combinedResults.BackupAnalysis.BackupCoveragePercentage;
        var hasDRPlan = combinedResults.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan;
        var hasMultiRegion = combinedResults.DisasterRecoveryAnalysis.RecoveryObjectives.ContainsKey("MultiRegionDeployment") &&
                            combinedResults.DisasterRecoveryAnalysis.RecoveryObjectives["MultiRegionDeployment"] == "Yes";

        // Cross-domain analysis: Backup and DR alignment
        if (backupCoverage < 50 && !hasDRPlan)
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "cross.domain.analysis",
                ResourceName = "BCDR Strategy Alignment",
                Issue = "Both backup coverage and disaster recovery capabilities are insufficient",
                Recommendation = "Develop comprehensive BCDR strategy addressing both data protection and service continuity",
                Severity = "High",
                BusinessImpact = "Organization lacks fundamental business continuity protections against data loss and service disruption"
            });
        }

        // Cross-domain analysis: Regional consistency
        if (hasMultiRegion && backupCoverage < 70)
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "cross.domain.regional",
                ResourceName = "Regional BCDR Consistency",
                Issue = "Multi-region deployment exists but backup coverage is inadequate across regions",
                Recommendation = "Ensure backup policies cover resources in all deployed regions consistently",
                Severity = "Medium",
                BusinessImpact = "Inconsistent protection across regions could lead to partial data loss during regional disasters"
            });
        }

        // Cross-domain analysis: RTO/RPO alignment
        if (backupCoverage > 80 && hasDRPlan && combinedResults.DisasterRecoveryAnalysis.ReplicationEnabledResources == 0)
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "cross.domain.objectives",
                ResourceName = "RTO/RPO Alignment",
                Issue = "Good backup coverage exists but lacks real-time replication for aggressive RTO requirements",
                Recommendation = "Consider implementing Azure Site Recovery or database geo-replication for critical workloads requiring low RTO",
                Severity = "Medium",
                BusinessImpact = "Recovery time objectives may not be met despite good backup coverage"
            });
        }

        // Cross-domain analysis: Compliance framework integration
        var complianceIndicators = combinedResults.Findings.Count(f =>
            f.Issue.ToLowerInvariant().Contains("compliance") ||
            f.Issue.ToLowerInvariant().Contains("retention") ||
            f.Issue.ToLowerInvariant().Contains("audit"));

        if (complianceIndicators > 3)
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "cross.domain.compliance",
                ResourceName = "Compliance Framework Integration",
                Issue = "Multiple compliance-related BCDR gaps identified across backup and recovery domains",
                Recommendation = "Develop compliance-aware BCDR policies aligned with regulatory requirements (SOC 2, ISO 27001, etc.)",
                Severity = "Medium",
                BusinessImpact = "Potential compliance violations and audit findings related to business continuity controls"
            });
        }
    }

    private void EnhanceWithBusinessImpactAnalysis(BusinessContinuityResults combinedResults)
    {
        var totalResources = combinedResults.BackupAnalysis.TotalResources;
        var backupCoverage = combinedResults.BackupAnalysis.BackupCoveragePercentage;
        var hasDRPlan = combinedResults.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan;

        // Calculate business impact metrics
        var unprotectedResources = totalResources - combinedResults.BackupAnalysis.BackedUpResources;
        var criticalFindings = combinedResults.Findings.Count(f => f.Severity == "High");

        // Add business impact summary finding
        var businessImpactLevel = DetermineBusinessImpactLevel(backupCoverage, hasDRPlan, criticalFindings);

        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "business.impact.assessment",
            ResourceName = "Business Impact Assessment",
            Issue = $"Overall business continuity posture assessment: {businessImpactLevel} risk level",
            Recommendation = GenerateBusinessImpactRecommendation(businessImpactLevel, backupCoverage, hasDRPlan, criticalFindings),
            Severity = businessImpactLevel == "High" ? "High" : businessImpactLevel == "Medium" ? "Medium" : "Low",
            BusinessImpact = GenerateBusinessImpactStatement(businessImpactLevel, unprotectedResources, totalResources)
        });

        // Add priority recommendations based on business impact
        AddPriorityRecommendations(combinedResults, businessImpactLevel, backupCoverage, hasDRPlan);

        // Add compliance and monitoring recommendations
        AddComplianceAndMonitoringFindings(combinedResults, businessImpactLevel);

        // Add cost optimization insights
        AddCostOptimizationFindings(combinedResults, backupCoverage, hasDRPlan);
    }

    private string DetermineBusinessImpactLevel(decimal backupCoverage, bool hasDRPlan, int criticalFindings)
    {
        // High risk: Poor backup coverage AND no DR plan AND multiple critical findings
        if (backupCoverage < 50 && !hasDRPlan && criticalFindings >= 3)
            return "High";

        // Medium risk: Either poor backup OR no DR plan OR several critical findings
        if (backupCoverage < 70 || !hasDRPlan || criticalFindings >= 2)
            return "Medium";

        // Low risk: Good backup coverage AND DR plan AND few critical findings
        return "Low";
    }

    private string GenerateBusinessImpactRecommendation(string impactLevel, decimal backupCoverage, bool hasDRPlan, int criticalFindings)
    {
        return impactLevel switch
        {
            "High" => "Immediate action required: Implement comprehensive BCDR strategy with backup protection and disaster recovery planning. Address all critical findings within 30 days.",
            "Medium" => "Significant improvements needed: Enhance backup coverage to >80%, establish disaster recovery procedures, and remediate critical findings within 60 days.",
            "Low" => "Maintain current posture: Continue monitoring and testing BCDR procedures. Address remaining findings during next maintenance window.",
            _ => "Review and enhance business continuity capabilities based on organizational risk tolerance."
        };
    }

    private string GenerateBusinessImpactStatement(string impactLevel, int unprotectedResources, int totalResources)
    {
        var protectionRate = totalResources > 0 ? Math.Round((decimal)(totalResources - unprotectedResources) / totalResources * 100, 1) : 100;

        return impactLevel switch
        {
            "High" => $"Critical business continuity gaps expose organization to significant data loss and extended downtime. Only {protectionRate}% of critical resources adequately protected.",
            "Medium" => $"Moderate business continuity risks may impact recovery capabilities during disasters. {protectionRate}% resource protection rate requires improvement.",
            "Low" => $"Business continuity posture is generally adequate with {protectionRate}% resource protection. Minor improvements recommended for optimization.",
            _ => $"Business continuity assessment completed with {protectionRate}% resource protection rate."
        };
    }

    private void AddPriorityRecommendations(BusinessContinuityResults combinedResults, string impactLevel, decimal backupCoverage, bool hasDRPlan)
    {
        if (impactLevel == "High")
        {
            // Priority 1: Immediate backup protection
            if (backupCoverage < 50)
            {
                combinedResults.Findings.Add(new BusinessContinuityFinding
                {
                    Category = "BusinessContinuity",
                    ResourceId = "priority.backup.immediate",
                    ResourceName = "Priority Action: Backup Protection",
                    Issue = "Less than 50% of critical resources have backup protection",
                    Recommendation = "IMMEDIATE: Deploy Recovery Services Vaults and configure backup for all critical VMs and databases within 7 days",
                    Severity = "High",
                    BusinessImpact = "Each day without backup protection increases risk of permanent data loss"
                });
            }

            // Priority 2: Basic DR capabilities
            if (!hasDRPlan)
            {
                combinedResults.Findings.Add(new BusinessContinuityFinding
                {
                    Category = "BusinessContinuity",
                    ResourceId = "priority.dr.basic",
                    ResourceName = "Priority Action: Disaster Recovery",
                    Issue = "No disaster recovery plan or capabilities identified",
                    Recommendation = "IMMEDIATE: Establish basic DR procedures and document recovery processes within 14 days",
                    Severity = "High",
                    BusinessImpact = "Without DR capabilities, any regional outage could cause extended business disruption"
                });
            }
        }
        else if (impactLevel == "Medium")
        {
            // Optimization recommendations for medium risk
            if (backupCoverage < 80)
            {
                combinedResults.Findings.Add(new BusinessContinuityFinding
                {
                    Category = "BusinessContinuity",
                    ResourceId = "optimization.backup.coverage",
                    ResourceName = "Optimization: Expand Backup Coverage",
                    Issue = "Backup coverage below industry best practice of 80%+",
                    Recommendation = "Expand backup coverage to include all production and critical development resources",
                    Severity = "Medium",
                    BusinessImpact = "Incomplete backup coverage may result in data loss for unprotected resources"
                });
            }

            if (hasDRPlan && combinedResults.DisasterRecoveryAnalysis.ReplicationEnabledResources == 0)
            {
                combinedResults.Findings.Add(new BusinessContinuityFinding
                {
                    Category = "BusinessContinuity",
                    ResourceId = "optimization.dr.automation",
                    ResourceName = "Optimization: Automate DR Processes",
                    Issue = "DR plan exists but lacks automated replication and failover capabilities",
                    Recommendation = "Implement Azure Site Recovery for critical workloads to reduce RTO and automate failover",
                    Severity = "Medium",
                    BusinessImpact = "Manual DR processes increase recovery time and risk of human error during disasters"
                });
            }
        }
        else // Low risk
        {
            // Fine-tuning recommendations for low risk
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "maintenance.testing.schedule",
                ResourceName = "Maintenance: DR Testing Schedule",
                Issue = "Establish regular disaster recovery testing schedule",
                Recommendation = "Implement quarterly DR tests and annual business continuity exercises to validate procedures",
                Severity = "Low",
                BusinessImpact = "Regular testing ensures DR procedures remain effective and staff maintain readiness"
            });

            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "maintenance.documentation.review",
                ResourceName = "Maintenance: Documentation Review",
                Issue = "BCDR documentation should be reviewed and updated regularly",
                Recommendation = "Establish semi-annual review process for backup policies and recovery procedures",
                Severity = "Low",
                BusinessImpact = "Outdated procedures may be ineffective during actual disaster scenarios"
            });
        }
    }

    private void AddComplianceAndMonitoringFindings(BusinessContinuityResults combinedResults, string businessImpactLevel)
    {
        // Backup monitoring and alerting
        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "backup.monitoring.alerting",
            ResourceName = "Backup Monitoring & Alerting",
            Issue = "Backup job monitoring and alerting should be configured for proactive failure detection",
            Recommendation = "Implement Azure Monitor alerts for backup job failures, configure notification channels, and establish backup success rate monitoring",
            Severity = businessImpactLevel == "High" ? "High" : "Medium",
            BusinessImpact = "Backup failures may go unnoticed without proper monitoring, leading to data loss"
        });

        // DR testing and automation
        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "dr.testing.automation",
            ResourceName = "DR Testing & Automation",
            Issue = "Disaster recovery testing schedule and automation should be established",
            Recommendation = "Implement quarterly DR tests, automate recovery procedures where possible, and maintain documented runbooks",
            Severity = "Medium",
            BusinessImpact = "Untested DR procedures may fail during actual disasters"
        });

        // Recovery metrics and SLA monitoring
        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "recovery.metrics.sla",
            ResourceName = "Recovery Metrics & SLA Monitoring",
            Issue = "Recovery Time Objective (RTO) and Recovery Point Objective (RPO) metrics should be monitored",
            Recommendation = "Define and monitor RTO/RPO metrics, establish SLA dashboards, and track backup/recovery performance against business requirements",
            Severity = "Low",
            BusinessImpact = "Without metrics, it's difficult to validate if BCDR capabilities meet business requirements"
        });

        // Governance and policy enforcement
        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "governance.policy.enforcement",
            ResourceName = "BCDR Governance & Policy Enforcement",
            Issue = "Azure Policy should be used to enforce backup and DR requirements",
            Recommendation = "Implement Azure Policies to require backup configuration for critical resources, enforce retention policies, and ensure compliance with BCDR standards",
            Severity = "Low",
            BusinessImpact = "Manual governance processes are prone to human error and inconsistent application"
        });

        // Always add compliance consideration (maintained for backward compatibility)
        if (!combinedResults.Findings.Any(f => f.ResourceId == "compliance.framework.alignment"))
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "compliance.framework.alignment",
                ResourceName = "Compliance Framework Alignment",
                Issue = "BCDR capabilities should align with applicable compliance requirements",
                Recommendation = "Review BCDR procedures against SOC 2, ISO 27001, HIPAA, PCI-DSS, or industry-specific compliance frameworks as applicable",
                Severity = "Low",
                BusinessImpact = "Non-compliant BCDR procedures may result in audit findings and regulatory issues"
            });
        }
    }

    private void AddCostOptimizationFindings(BusinessContinuityResults combinedResults, decimal backupCoverage, bool hasDRPlan)
    {
        // Backup cost optimization
        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "backup.cost.optimization",
            ResourceName = "Backup Cost Optimization",
            Issue = "Backup storage costs should be optimized through retention policy review and archive tier usage",
            Recommendation = "Review backup retention policies, implement archive tiers for long-term retention, and optimize backup frequency based on criticality",
            Severity = "Low",
            BusinessImpact = "Inefficient backup policies can result in unnecessary storage costs without improving protection"
        });

        // DR cost efficiency
        if (hasDRPlan)
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "dr.cost.efficiency",
                ResourceName = "DR Cost Efficiency",
                Issue = "Disaster recovery infrastructure costs should be optimized for cost-effectiveness",
                Recommendation = "Consider Azure Site Recovery for cost-effective DR, use reserved instances for DR resources, and implement automated shutdown for non-production DR environments",
                Severity = "Low",
                BusinessImpact = "Expensive always-on DR infrastructure may not be cost-effective for all workloads"
            });
        }

        // Storage tier optimization
        combinedResults.Findings.Add(new BusinessContinuityFinding
        {
            Category = "BusinessContinuity",
            ResourceId = "storage.tier.optimization",
            ResourceName = "Storage Tier Optimization",
            Issue = "Storage tiers should be optimized based on access patterns and retention requirements",
            Recommendation = "Implement lifecycle management policies to automatically move older backups to cool and archive tiers, reducing long-term storage costs",
            Severity = "Low",
            BusinessImpact = "Manual storage tier management results in higher costs for infrequently accessed backup data"
        });

        // Resource tagging for cost allocation
        var untaggedResourcesIndicator = combinedResults.Findings.Count(f =>
            f.Issue.ToLowerInvariant().Contains("tagging") ||
            f.Issue.ToLowerInvariant().Contains("lacks proper tag"));

        if (untaggedResourcesIndicator > 2)
        {
            combinedResults.Findings.Add(new BusinessContinuityFinding
            {
                Category = "BusinessContinuity",
                ResourceId = "cost.allocation.tagging",
                ResourceName = "Cost Allocation Through Tagging",
                Issue = "BCDR resources lack proper tagging for cost allocation and chargeback",
                Recommendation = "Implement consistent tagging for backup and DR resources to enable cost allocation by department, project, or cost center",
                Severity = "Low",
                BusinessImpact = "Difficult to track and allocate BCDR costs without proper resource tagging"
            });
        }
    }
}