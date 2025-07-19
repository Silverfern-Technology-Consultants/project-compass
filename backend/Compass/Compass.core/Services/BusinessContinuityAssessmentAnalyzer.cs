using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services;

public interface IBusinessContinuityAssessmentAnalyzer
{
    Task<BusinessContinuityResults> AnalyzeBusinessContinuityAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);

    Task<BusinessContinuityResults> AnalyzeBusinessContinuityWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);
}

public class BusinessContinuityAssessmentAnalyzer : IBusinessContinuityAssessmentAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<BusinessContinuityAssessmentAnalyzer> _logger;

    public BusinessContinuityAssessmentAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        ILogger<BusinessContinuityAssessmentAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<BusinessContinuityResults> AnalyzeBusinessContinuityAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Business Continuity analysis for assessment type: {AssessmentType}", assessmentType);

        var results = new BusinessContinuityResults();
        var findings = new List<BusinessContinuityFinding>();

        try
        {
            switch (assessmentType)
            {
                case AssessmentType.BackupCoverage:
                    await AnalyzeBackupCoverageAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.RecoveryConfiguration:
                    await AnalyzeRecoveryConfigurationAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.BusinessContinuityFull:
                    await AnalyzeBackupCoverageAsync(subscriptionIds, results, findings, cancellationToken);
                    await AnalyzeRecoveryConfigurationAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported BCDR assessment type: {assessmentType}");
            }

            results.Findings = findings;
            results.Score = CalculateOverallBcdrScore(results);

            _logger.LogInformation("Business Continuity analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Business Continuity for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<BusinessContinuityResults> AnalyzeBusinessContinuityWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Business Continuity analysis for client {ClientId}", clientId);

        // For now, fall back to standard analysis since we don't have OAuth-specific BCDR queries yet
        // In the future, this could use OAuth to access Azure Backup and Site Recovery APIs
        return await AnalyzeBusinessContinuityAsync(subscriptionIds, assessmentType, cancellationToken);
    }

    private async Task AnalyzeBackupCoverageAsync(
        string[] subscriptionIds,
        BusinessContinuityResults results,
        List<BusinessContinuityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Backup Coverage...");

        try
        {
            // Get all Azure resources that should have backup protection
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Identify backup-critical resources
            var virtualMachines = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();
            var databases = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers/databases")).ToList();
            var fileShares = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.storage/storageaccounts") &&
                                                     r.Kind?.ToLowerInvariant().Contains("filestorage") == true).ToList();

            // Look for Azure Backup resources
            var recoveryVaults = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.recoveryservices/vaults").ToList();
            var backupPolicies = allResources.Where(r => r.Type.ToLowerInvariant().Contains("backuppolicies")).ToList();

            // Calculate backup coverage
            var totalCriticalResources = virtualMachines.Count + databases.Count + fileShares.Count;
            var protectedResources = 0; // We'll estimate based on Recovery Vaults presence

            // Analyze Recovery Vaults
            foreach (var vault in recoveryVaults)
            {
                await AnalyzeRecoveryVaultAsync(vault, findings);
                // Estimate protected resources (in real implementation, would query vault contents)
                protectedResources += Math.Min(5, totalCriticalResources); // Rough estimate
            }

            results.BackupAnalysis = new BackupAnalysis
            {
                TotalResources = totalCriticalResources,
                BackedUpResources = Math.Min(protectedResources, totalCriticalResources),
                BackupCoveragePercentage = totalCriticalResources > 0 ?
                    Math.Round((decimal)protectedResources / totalCriticalResources * 100, 2) : 100m
            };

            // Analyze backup status by resource type
            results.BackupAnalysis.BackupStatusByResourceType = new Dictionary<string, int>
            {
                ["VirtualMachines"] = virtualMachines.Count,
                ["Databases"] = databases.Count,
                ["FileShares"] = fileShares.Count
            };

            // Identify unprotected critical resources
            if (recoveryVaults.Count == 0)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "Backup",
                    ResourceId = "backup.infrastructure",
                    ResourceName = "Backup Infrastructure",
                    Issue = "No Azure Recovery Services Vaults found for backup protection",
                    Recommendation = "Deploy Recovery Services Vaults and configure backup policies for critical resources",
                    Severity = "High",
                    BusinessImpact = "Critical data and systems are not protected against data loss or corruption"
                });

                // Add all critical resources as unprotected
                results.BackupAnalysis.UnbackedUpCriticalResources.AddRange(
                    virtualMachines.Concat(databases).Concat(fileShares).Select(r => r.Name).Take(10)
                );
            }

            // Analyze individual VMs for backup protection
            foreach (var vm in virtualMachines.Take(10)) // Limit for performance
            {
                await AnalyzeVMBackupStatusAsync(vm, findings, recoveryVaults.Any());
            }

            // Analyze databases for backup configuration
            foreach (var db in databases.Take(10)) // Limit for performance
            {
                await AnalyzeDatabaseBackupAsync(db, findings);
            }

            _logger.LogInformation("Backup Coverage analysis completed. Coverage: {Coverage}%, Protected: {Protected}/{Total}",
                results.BackupAnalysis.BackupCoveragePercentage, results.BackupAnalysis.BackedUpResources, results.BackupAnalysis.TotalResources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze backup coverage");

            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = "error.backup",
                ResourceName = "Backup Coverage Analysis",
                Issue = "Failed to analyze backup coverage due to an error",
                Recommendation = "Review Azure permissions and retry backup analysis",
                Severity = "High",
                BusinessImpact = "Cannot assess data protection and backup readiness"
            });
        }
    }

    private async Task AnalyzeRecoveryVaultAsync(AzureResource vault, List<BusinessContinuityFinding> findings)
    {
        string vaultName = vault.Name;
        string vaultId = vault.Id;

        // Check vault configuration through properties
        if (!string.IsNullOrEmpty(vault.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(vault.Properties);

                // Check for cross-region restore capability
                if (properties.RootElement.TryGetProperty("crossRegionRestore", out var crossRegion))
                {
                    if (!crossRegion.GetBoolean())
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "Backup",
                            ResourceId = vaultId,
                            ResourceName = vaultName,
                            Issue = "Recovery Services Vault does not have cross-region restore enabled",
                            Recommendation = "Enable cross-region restore for enhanced disaster recovery capabilities",
                            Severity = "Medium",
                            BusinessImpact = "Limited disaster recovery options if primary region becomes unavailable"
                        });
                    }
                }

                // Check for soft delete configuration
                if (properties.RootElement.TryGetProperty("softDeleteFeatureState", out var softDelete))
                {
                    if (softDelete.GetString()?.ToLowerInvariant() != "enabled")
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "Backup",
                            ResourceId = vaultId,
                            ResourceName = vaultName,
                            Issue = "Recovery Services Vault does not have soft delete enabled",
                            Recommendation = "Enable soft delete to protect against accidental or malicious backup deletion",
                            Severity = "Medium",
                            BusinessImpact = "Backup data vulnerable to accidental or malicious deletion"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed, continue
            }
        }

        // Check for proper tagging
        if (!vault.HasTags || !vault.Tags.ContainsKey("Environment"))
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = vaultId,
                ResourceName = vaultName,
                Issue = "Recovery Services Vault lacks proper tagging for governance",
                Recommendation = "Add Environment, Owner, and Purpose tags to Recovery Services Vault",
                Severity = "Low",
                BusinessImpact = "Difficult to manage and audit backup infrastructure without proper tagging"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVMBackupStatusAsync(AzureResource vm, List<BusinessContinuityFinding> findings, bool hasRecoveryVaults)
    {
        string vmName = vm.Name;
        string vmId = vm.Id;

        if (!hasRecoveryVaults)
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = vmId,
                ResourceName = vmName,
                Issue = "Virtual machine is not protected by Azure Backup",
                Recommendation = "Configure Azure Backup protection for this virtual machine",
                Severity = "High",
                BusinessImpact = "Virtual machine data and configuration are at risk of permanent loss"
            });
        }

        // Check VM size and criticality indicators
        if (!string.IsNullOrEmpty(vm.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(vm.Properties);
                if (properties.RootElement.TryGetProperty("hardwareProfile", out var hardware))
                {
                    if (hardware.TryGetProperty("vmSize", out var vmSize))
                    {
                        var size = vmSize.GetString()?.ToLowerInvariant();
                        if (size?.Contains("standard_d") == true || size?.Contains("standard_e") == true)
                        {
                            // Production-sized VMs should definitely have backup
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = vmId,
                                ResourceName = vmName,
                                Issue = $"Production-sized VM ({size}) appears to lack backup protection",
                                Recommendation = "Implement comprehensive backup strategy for production virtual machines",
                                Severity = "High",
                                BusinessImpact = "Production workloads are vulnerable to data loss and extended downtime"
                            });
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDatabaseBackupAsync(AzureResource database, List<BusinessContinuityFinding> findings)
    {
        string dbName = database.Name;
        string dbId = database.Id;

        // SQL databases have automated backup separate from Recovery Vaults
        if (database.Type.ToLowerInvariant().Contains("microsoft.sql"))
        {
            // Check for proper database tagging indicating backup requirements
            bool isCritical = database.Tags.ContainsKey("Environment") &&
                             (database.Tags["Environment"].ToLowerInvariant().Contains("prod") ||
                              database.Tags["Environment"].ToLowerInvariant().Contains("production"));

            // Analyze SQL Database backup configuration
            if (!string.IsNullOrEmpty(database.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(database.Properties);

                    // Check for backup retention policy
                    if (properties.RootElement.TryGetProperty("currentServiceObjectiveName", out var serviceObjective))
                    {
                        var tier = serviceObjective.GetString()?.ToLowerInvariant();

                        // Basic tier has limited backup retention (7 days PITR only)
                        if (tier == "basic")
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "SQL Database is using Basic tier with limited backup retention (7 days PITR only)",
                                Recommendation = "Upgrade to Standard or Premium tier for extended backup retention and Long-Term Retention (LTR) options",
                                Severity = isCritical ? "High" : "Medium",
                                BusinessImpact = "Basic tier provides only 7-day point-in-time recovery, insufficient for production workloads"
                            });
                        }
                        else if (tier?.Contains("standard") == true || tier?.Contains("premium") == true)
                        {
                            // Standard/Premium tiers support LTR but need to check if it's configured
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "SQL Database Long-Term Retention (LTR) configuration should be verified",
                                Recommendation = "Configure Long-Term Retention policies for weekly, monthly, and yearly backups beyond the default 35-day retention",
                                Severity = "Medium",
                                BusinessImpact = "Without LTR, only 35 days of backup history is available for compliance and long-term recovery"
                            });
                        }
                    }

                    // Check for geo-redundant backup storage
                    if (properties.RootElement.TryGetProperty("requestedBackupStorageRedundancy", out var backupRedundancy))
                    {
                        var redundancy = backupRedundancy.GetString()?.ToLowerInvariant();
                        if (redundancy == "local" && isCritical)
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "Production SQL Database is using locally redundant backup storage",
                                Recommendation = "Configure geo-redundant backup storage for production databases to protect against regional disasters",
                                Severity = "High",
                                BusinessImpact = "Database backups vulnerable to regional outages with locally redundant storage"
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed, provide general recommendations
                }
            }

            // General SQL backup recommendations
            if (isCritical || !database.HasTags)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "Backup",
                    ResourceId = dbId,
                    ResourceName = dbName,
                    Issue = "SQL Database backup configuration requires comprehensive review",
                    Recommendation = "Verify PITR settings, backup retention periods, LTR policies, and geo-redundant storage configuration",
                    Severity = isCritical ? "High" : "Medium",
                    BusinessImpact = "Inadequate backup configuration may result in data loss and compliance violations"
                });
            }

            // Check for transparent data encryption (related to backup security)
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = dbId,
                ResourceName = dbName,
                Issue = "SQL Database encryption status should be verified for backup security",
                Recommendation = "Ensure Transparent Data Encryption (TDE) is enabled to protect backups and data at rest",
                Severity = "Medium",
                BusinessImpact = "Unencrypted database backups may expose sensitive data if compromised"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeRecoveryConfigurationAsync(
        string[] subscriptionIds,
        BusinessContinuityResults results,
        List<BusinessContinuityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Recovery Configuration...");

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for Site Recovery resources
            var siteRecoveryVaults = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.recoveryservices/vaults").ToList();

            var replicationPolicies = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("replicationpolicies")).ToList();

            // Look for Traffic Manager (for DR routing)
            var trafficManagers = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/trafficmanagerprofiles").ToList();

            // Look for Application Gateways (for failover)
            var appGateways = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/applicationgateways").ToList();

            // Analyze multi-region deployment
            var regions = allResources.Select(r => r.Location).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();
            var hasMultiRegion = regions.Count > 1;

            results.DisasterRecoveryAnalysis = new DisasterRecoveryAnalysis
            {
                HasDisasterRecoveryPlan = siteRecoveryVaults.Any() || trafficManagers.Any(),
                ReplicationEnabledResources = replicationPolicies.Count,
                RecoveryObjectives = new Dictionary<string, string>
                {
                    ["MultiRegionDeployment"] = hasMultiRegion ? "Yes" : "No",
                    ["TrafficManagement"] = trafficManagers.Any() ? "Configured" : "Not Configured",
                    ["SiteRecoveryVaults"] = siteRecoveryVaults.Count.ToString()
                }
            };

            // Analyze disaster recovery readiness
            if (!hasMultiRegion)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = "infrastructure.regions",
                    ResourceName = "Multi-Region Deployment",
                    Issue = "All resources are deployed in a single region, creating a single point of failure",
                    Recommendation = "Consider deploying critical resources across multiple Azure regions for disaster recovery",
                    Severity = "High",
                    BusinessImpact = "Complete service outage if the primary region becomes unavailable"
                });

                results.DisasterRecoveryAnalysis.SinglePointsOfFailure.Add("Single region deployment");
            }

            if (!trafficManagers.Any() && hasMultiRegion)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = "traffic.management",
                    ResourceName = "Traffic Management",
                    Issue = "Multi-region deployment lacks traffic management for automated failover",
                    Recommendation = "Implement Azure Traffic Manager or Front Door for intelligent traffic routing and failover",
                    Severity = "Medium",
                    BusinessImpact = "Manual intervention required during regional outages, extending recovery time"
                });
            }

            if (siteRecoveryVaults.Count == 0)
            {
                var criticalVMs = allResources.Where(r =>
                    r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

                if (criticalVMs.Any())
                {
                    findings.Add(new BusinessContinuityFinding
                    {
                        Category = "DisasterRecovery",
                        ResourceId = "site.recovery",
                        ResourceName = "Site Recovery Configuration",
                        Issue = $"Found {criticalVMs.Count} virtual machines without Site Recovery protection",
                        Recommendation = "Implement Azure Site Recovery for critical virtual machine workloads",
                        Severity = "High",
                        BusinessImpact = "Extended recovery times and potential data loss during disasters"
                    });
                }
            }

            // Analyze application-level redundancy
            await AnalyzeApplicationRedundancyAsync(allResources, findings);

            // Check for backup dependencies
            await AnalyzeBackupDependenciesAsync(allResources, findings);

            _logger.LogInformation("Recovery Configuration analysis completed. DR Plan: {HasDRPlan}, Replicated Resources: {ReplicatedCount}",
                results.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan, results.DisasterRecoveryAnalysis.ReplicationEnabledResources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze recovery configuration");

            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = "error.recovery",
                ResourceName = "Recovery Configuration Analysis",
                Issue = "Failed to analyze disaster recovery configuration",
                Recommendation = "Review permissions and retry disaster recovery analysis",
                Severity = "Medium",
                BusinessImpact = "Cannot assess disaster recovery readiness and capabilities"
            });
        }
    }

    private async Task AnalyzeApplicationRedundancyAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        // Look for single-instance applications without redundancy
        var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();
        var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.sql/servers").ToList();

        // Check App Services for multiple instances
        var appServicesByName = appServices.GroupBy(a => a.Name.Split('-')[0]).ToList(); // Group by base name
        foreach (var appGroup in appServicesByName)
        {
            if (appGroup.Count() == 1)
            {
                var app = appGroup.First();
                bool isProduction = app.Tags.ContainsKey("Environment") &&
                                   app.Tags["Environment"].ToLowerInvariant().Contains("prod");

                if (isProduction)
                {
                    findings.Add(new BusinessContinuityFinding
                    {
                        Category = "DisasterRecovery",
                        ResourceId = app.Id,
                        ResourceName = app.Name,
                        Issue = "Production application appears to have only one instance deployed",
                        Recommendation = "Deploy multiple instances across availability zones or regions for redundancy",
                        Severity = "Medium",
                        BusinessImpact = "Application unavailable during maintenance or regional issues"
                    });
                }
            }
        }

        // Check SQL Servers for geo-redundancy
        foreach (var sqlServer in sqlServers)
        {
            bool isProduction = sqlServer.Tags.ContainsKey("Environment") &&
                               sqlServer.Tags["Environment"].ToLowerInvariant().Contains("prod");

            if (isProduction)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = sqlServer.Id,
                    ResourceName = sqlServer.Name,
                    Issue = "Production SQL Server should be reviewed for geo-redundancy configuration",
                    Recommendation = "Consider implementing geo-replication, failover groups, or read replicas for production databases",
                    Severity = "Medium",
                    BusinessImpact = "Database unavailable during regional outages without geo-redundancy"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeBackupDependenciesAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        // Look for shared storage accounts that many resources depend on
        var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();
        var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.keyvault/vaults").ToList();

        foreach (var storage in storageAccounts)
        {
            // Check if storage account has geo-redundancy
            if (!string.IsNullOrEmpty(storage.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(storage.Properties);
                    if (properties.RootElement.TryGetProperty("replication", out var replication))
                    {
                        var replicationType = replication.GetString()?.ToLowerInvariant();
                        if (replicationType?.Contains("lrs") == true)
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "DisasterRecovery",
                                ResourceId = storage.Id,
                                ResourceName = storage.Name,
                                Issue = "Storage account uses locally redundant storage (LRS), vulnerable to regional outages",
                                Recommendation = "Upgrade to geo-redundant storage (GRS) or read-access geo-redundant storage (RA-GRS)",
                                Severity = "Medium",
                                BusinessImpact = "Data loss risk during regional disasters with LRS configuration"
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed
                }
            }
        }

        // Check Key Vaults for backup and recovery
        foreach (var keyVault in keyVaults)
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = keyVault.Id,
                ResourceName = keyVault.Name,
                Issue = "Key Vault disaster recovery capabilities should be verified",
                Recommendation = "Ensure Key Vault has appropriate backup policies and disaster recovery procedures",
                Severity = "Low",
                BusinessImpact = "Application failures if Key Vault becomes unavailable during disasters"
            });
        }

        await Task.CompletedTask;
    }

    private decimal CalculateOverallBcdrScore(BusinessContinuityResults results)
    {
        var scoringFactors = new List<decimal>();

        // Backup coverage score (60% weight)
        var backupScore = results.BackupAnalysis.BackupCoveragePercentage;
        scoringFactors.Add(backupScore * 0.60m);

        // Disaster recovery readiness score (40% weight)
        var drScore = 100m;
        if (!results.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan)
        {
            drScore -= 50m; // Major penalty for no DR plan
        }

        if (results.DisasterRecoveryAnalysis.SinglePointsOfFailure.Any())
        {
            drScore -= results.DisasterRecoveryAnalysis.SinglePointsOfFailure.Count * 20m; // Penalty per SPOF
        }

        drScore = Math.Max(0, drScore);
        scoringFactors.Add(drScore * 0.40m);

        // Critical finding penalty
        var criticalFindings = results.Findings.Count(f => f.Severity == "High");
        var mediumFindings = results.Findings.Count(f => f.Severity == "Medium");
        var penalty = (criticalFindings * 10) + (mediumFindings * 5);

        var finalScore = Math.Max(0, scoringFactors.Sum() - penalty);

        _logger.LogInformation("BCDR Score calculated: {Score}% (Backup: {BackupScore}%, DR: {DrScore}%, Penalty: {Penalty})",
            finalScore, backupScore, drScore, penalty);

        return Math.Round(finalScore, 2);
    }
}