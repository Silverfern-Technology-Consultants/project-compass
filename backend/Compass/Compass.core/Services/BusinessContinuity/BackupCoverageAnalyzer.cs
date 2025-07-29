using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.BusinessContinuity;

public interface IBackupCoverageAnalyzer
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

public class BackupCoverageAnalyzer : IBackupCoverageAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<BackupCoverageAnalyzer> _logger;

    public BackupCoverageAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        ILogger<BackupCoverageAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<BusinessContinuityResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Backup Coverage analysis for {SubscriptionCount} subscriptions", subscriptionIds.Length);

        var results = new BusinessContinuityResults();
        var findings = new List<BusinessContinuityFinding>();

        try
        {
            await AnalyzeBackupCoverageAsync(subscriptionIds, results, findings, cancellationToken);

            results.Findings = findings;
            results.Score = CalculateBackupCoverageScore(results.BackupAnalysis, findings);

            _logger.LogInformation("Backup Coverage analysis completed. Score: {Score}%, Coverage: {Coverage}%",
                results.Score, results.BackupAnalysis.BackupCoveragePercentage);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze backup coverage for subscriptions: {Subscriptions}",
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
        _logger.LogInformation("Starting OAuth-enabled Backup Coverage analysis for client {ClientId}", clientId);

        try
        {
            // Test OAuth credentials first
            var hasOAuthCredentials = await _oauthService.TestCredentialsAsync(clientId, organizationId);
            if (hasOAuthCredentials)
            {
                _logger.LogInformation("OAuth credentials available for enhanced backup analysis");
                // For now, use standard analysis - OAuth enhancement can be added later for Azure Backup APIs
                return await AnalyzeAsync(subscriptionIds, cancellationToken);
            }
            else
            {
                _logger.LogInformation("OAuth not available, using standard backup analysis");
                return await AnalyzeAsync(subscriptionIds, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OAuth backup analysis failed, falling back to standard analysis");
            return await AnalyzeAsync(subscriptionIds, cancellationToken);
        }
    }

    private async Task AnalyzeBackupCoverageAsync(
        string[] subscriptionIds,
        BusinessContinuityResults results,
        List<BusinessContinuityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Backup Coverage across {SubscriptionCount} subscriptions...", subscriptionIds.Length);

        try
        {
            // Get all Azure resources that should have backup protection
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Identify backup-critical resources
            var virtualMachines = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();
            var databases = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers/databases")).ToList();
            var fileShares = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.storage/storageaccounts") &&
                                                     r.Kind?.ToLowerInvariant().Contains("filestorage") == true).ToList();
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.keyvault/vaults").ToList();

            // Look for Azure Backup resources
            var recoveryVaults = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.recoveryservices/vaults").ToList();
            var backupPolicies = allResources.Where(r => r.Type.ToLowerInvariant().Contains("backuppolicies")).ToList();

            // Calculate backup coverage
            var totalCriticalResources = virtualMachines.Count + databases.Count + fileShares.Count + keyVaults.Count;
            var protectedResources = EstimateProtectedResources(recoveryVaults, totalCriticalResources);

            // Initialize backup analysis
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
                ["FileShares"] = fileShares.Count,
                ["KeyVaults"] = keyVaults.Count,
                ["RecoveryVaults"] = recoveryVaults.Count
            };

            // Check for Recovery Services Vaults
            if (recoveryVaults.Count == 0 && totalCriticalResources > 0)
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

                // Add critical resources as unprotected
                results.BackupAnalysis.UnbackedUpCriticalResources.AddRange(
                    virtualMachines.Concat(databases).Concat(fileShares).Select(r => r.Name).Take(10)
                );
            }
            else
            {
                // Analyze individual Recovery Vaults
                foreach (var vault in recoveryVaults)
                {
                    await AnalyzeRecoveryVaultAsync(vault, findings);
                }
            }

            // Analyze VM backup status
            foreach (var vm in virtualMachines.Take(15)) // Limit for performance
            {
                await AnalyzeVMBackupStatusAsync(vm, findings, recoveryVaults.Any());
            }

            // Analyze database backup configuration
            foreach (var db in databases.Take(15)) // Limit for performance
            {
                await AnalyzeDatabaseBackupAsync(db, findings);
            }

            // Analyze Key Vault backup and protection
            foreach (var kv in keyVaults.Take(10))
            {
                await AnalyzeKeyVaultBackupAsync(kv, findings);
            }

            // Analyze storage account protection features
            await AnalyzeStorageAccountProtectionAsync(allResources, findings);

            // Analyze VM backup policies and configuration
            await AnalyzeVMBackupPoliciesAsync(virtualMachines, recoveryVaults, findings);

            // Analyze SQL advanced backup features
            await AnalyzeSQLAdvancedBackupAsync(databases, findings);

            // Check for backup dependencies and storage redundancy
            await AnalyzeBackupDependenciesAsync(allResources, findings);

            _logger.LogInformation("Backup Coverage analysis completed. Coverage: {Coverage}%, Protected: {Protected}/{Total}",
                results.BackupAnalysis.BackupCoveragePercentage, results.BackupAnalysis.BackedUpResources, results.BackupAnalysis.TotalResources);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze backup coverage");
            throw;
        }
    }

    private int EstimateProtectedResources(List<AzureResource> recoveryVaults, int totalResources)
    {
        if (!recoveryVaults.Any()) return 0;

        // Rough estimation: each Recovery Vault protects up to 10 resources
        // In real implementation, would query vault contents via Azure Backup APIs
        var estimatedProtection = recoveryVaults.Count * 10;
        return Math.Min(estimatedProtection, totalResources);
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

        // Check if VM appears to be production-critical
        bool isProductionCritical = vm.Tags.ContainsKey("Environment") &&
                                   (vm.Tags["Environment"].ToLowerInvariant().Contains("prod") ||
                                    vm.Tags["Environment"].ToLowerInvariant().Contains("production"));

        if (!hasRecoveryVaults)
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = vmId,
                ResourceName = vmName,
                Issue = "Virtual machine is not protected by Azure Backup",
                Recommendation = "Configure Azure Backup protection for this virtual machine",
                Severity = isProductionCritical ? "High" : "Medium",
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
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = vmId,
                                ResourceName = vmName,
                                Issue = $"Production-sized VM ({size}) requires backup protection verification",
                                Recommendation = "Verify backup configuration and ensure appropriate retention policies",
                                Severity = "High",
                                BusinessImpact = "Production workloads vulnerable to data loss without proper backup"
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

        // Check if database is production-critical
        bool isCritical = database.Tags.ContainsKey("Environment") &&
                         (database.Tags["Environment"].ToLowerInvariant().Contains("prod") ||
                          database.Tags["Environment"].ToLowerInvariant().Contains("production"));

        // SQL databases have automated backup separate from Recovery Vaults
        if (database.Type.ToLowerInvariant().Contains("microsoft.sql"))
        {
            if (!string.IsNullOrEmpty(database.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(database.Properties);

                    // Check service tier for backup capabilities
                    if (properties.RootElement.TryGetProperty("currentServiceObjectiveName", out var serviceObjective))
                    {
                        var tier = serviceObjective.GetString()?.ToLowerInvariant();

                        if (tier == "basic")
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "SQL Database using Basic tier has limited backup retention (7 days PITR only)",
                                Recommendation = "Upgrade to Standard or Premium tier for extended backup retention and LTR options",
                                Severity = isCritical ? "High" : "Medium",
                                BusinessImpact = "Basic tier provides insufficient backup retention for business requirements"
                            });
                        }
                    }

                    // Check backup storage redundancy
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
                                Issue = "Production SQL Database using locally redundant backup storage",
                                Recommendation = "Configure geo-redundant backup storage to protect against regional disasters",
                                Severity = "High",
                                BusinessImpact = "Database backups vulnerable to regional outages"
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed
                }
            }

            // General SQL backup recommendations
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = dbId,
                ResourceName = dbName,
                Issue = "SQL Database backup configuration requires verification",
                Recommendation = "Verify PITR settings, LTR policies, and geo-redundant storage configuration",
                Severity = isCritical ? "Medium" : "Low",
                BusinessImpact = "Inadequate backup configuration may result in data loss"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeKeyVaultBackupAsync(AzureResource keyVault, List<BusinessContinuityFinding> findings)
    {
        string kvName = keyVault.Name;
        string kvId = keyVault.Id;

        // Check Key Vault properties for advanced protection features
        if (!string.IsNullOrEmpty(keyVault.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(keyVault.Properties);

                // Check for soft delete
                bool softDeleteEnabled = false;
                if (properties.RootElement.TryGetProperty("enableSoftDelete", out var softDelete))
                {
                    softDeleteEnabled = softDelete.GetBoolean();
                }

                if (!softDeleteEnabled)
                {
                    findings.Add(new BusinessContinuityFinding
                    {
                        Category = "Backup",
                        ResourceId = kvId,
                        ResourceName = kvName,
                        Issue = "Key Vault does not have soft delete enabled",
                        Recommendation = "Enable soft delete to protect against accidental deletion of keys, secrets, and certificates",
                        Severity = "High",
                        BusinessImpact = "Keys and secrets vulnerable to permanent accidental deletion without soft delete"
                    });
                }

                // Check for purge protection
                bool purgeProtectionEnabled = false;
                if (properties.RootElement.TryGetProperty("enablePurgeProtection", out var purgeProtection))
                {
                    purgeProtectionEnabled = purgeProtection.GetBoolean();
                }

                if (softDeleteEnabled && !purgeProtectionEnabled)
                {
                    findings.Add(new BusinessContinuityFinding
                    {
                        Category = "Backup",
                        ResourceId = kvId,
                        ResourceName = kvName,
                        Issue = "Key Vault has soft delete but lacks purge protection",
                        Recommendation = "Enable purge protection to prevent permanent deletion during soft delete retention period",
                        Severity = "Medium",
                        BusinessImpact = "Soft deleted keys can still be permanently purged, reducing recovery options"
                    });
                }

                // Check for HSM protection level
                if (properties.RootElement.TryGetProperty("sku", out var sku))
                {
                    if (sku.TryGetProperty("name", out var skuName))
                    {
                        var skuValue = skuName.GetString()?.ToLowerInvariant();
                        if (skuValue == "standard")
                        {
                            // Check if this is a production Key Vault
                            bool isProduction = keyVault.Tags.ContainsKey("Environment") &&
                                               keyVault.Tags["Environment"].ToLowerInvariant().Contains("prod");

                            if (isProduction)
                            {
                                findings.Add(new BusinessContinuityFinding
                                {
                                    Category = "Backup",
                                    ResourceId = kvId,
                                    ResourceName = kvName,
                                    Issue = "Production Key Vault using Standard tier without HSM protection",
                                    Recommendation = "Consider Premium tier with HSM protection for production cryptographic keys",
                                    Severity = "Medium",
                                    BusinessImpact = "Standard tier keys less resilient against sophisticated attacks"
                                });
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        // General Key Vault backup recommendations
        findings.Add(new BusinessContinuityFinding
        {
            Category = "Backup",
            ResourceId = kvId,
            ResourceName = kvName,
            Issue = "Key Vault backup policies should be configured for keys, secrets, and certificates",
            Recommendation = "Implement automated backup procedures using Azure Key Vault backup APIs and consider Azure Backup Vault integration",
            Severity = "Medium",
            BusinessImpact = "Loss of cryptographic keys and secrets could impact application availability and data encryption"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeStorageAccountProtectionAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();

        foreach (var storage in storageAccounts.Take(15)) // Analyze more storage accounts
        {
            string storageName = storage.Name;
            string storageId = storage.Id;

            if (!string.IsNullOrEmpty(storage.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(storage.Properties);

                    // Check replication type (already partially implemented, enhancing)
                    if (properties.RootElement.TryGetProperty("replication", out var replication))
                    {
                        var replicationType = replication.GetString()?.ToLowerInvariant();
                        if (replicationType?.Contains("lrs") == true)
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = storageId,
                                ResourceName = storageName,
                                Issue = "Storage account uses locally redundant storage, vulnerable to regional outages",
                                Recommendation = "Upgrade to geo-redundant storage (GRS) or read-access geo-redundant storage (RA-GRS) for better disaster recovery",
                                Severity = "Medium",
                                BusinessImpact = "Data loss risk during regional disasters with LRS configuration"
                            });
                        }
                        else if (replicationType?.Contains("zrs") == true)
                        {
                            // ZRS is good for availability but check if GRS is needed for DR
                            bool isProductionZrs = storage.Tags.ContainsKey("Environment") &&
                                                  storage.Tags["Environment"].ToLowerInvariant().Contains("prod");

                            if (isProductionZrs)
                            {
                                findings.Add(new BusinessContinuityFinding
                                {
                                    Category = "Backup",
                                    ResourceId = storageId,
                                    ResourceName = storageName,
                                    Issue = "Production storage account using ZRS - consider GRS for cross-region disaster recovery",
                                    Recommendation = "Evaluate if GZRS (Geo-zone-redundant storage) is appropriate for production workloads requiring both high availability and disaster recovery",
                                    Severity = "Low",
                                    BusinessImpact = "ZRS provides high availability but limited disaster recovery across regions"
                                });
                            }
                        }
                    }

                    // Check for blob service properties (soft delete, versioning, etc.)
                    // Note: These would typically require additional API calls to get blob service properties
                    // For now, we'll recommend verification
                    bool isProduction = storage.Tags.ContainsKey("Environment") &&
                                       storage.Tags["Environment"].ToLowerInvariant().Contains("prod");

                    if (isProduction || !storage.HasTags)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "Backup",
                            ResourceId = storageId,
                            ResourceName = storageName,
                            Issue = "Storage account blob protection features should be verified",
                            Recommendation = "Enable blob soft delete, versioning, and point-in-time restore for blob data protection. Consider immutable storage for compliance requirements.",
                            Severity = "Medium",
                            BusinessImpact = "Blob data vulnerable to accidental deletion or modification without proper protection features"
                        });
                    }

                    // Check for access tier optimization
                    if (properties.RootElement.TryGetProperty("accessTier", out var accessTier))
                    {
                        var tier = accessTier.GetString()?.ToLowerInvariant();
                        if (tier == "hot" && storageName.ToLowerInvariant().Contains("backup"))
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = storageId,
                                ResourceName = storageName,
                                Issue = "Backup storage account using Hot tier may be inefficient for long-term retention",
                                Recommendation = "Consider Cool or Archive tier for backup data to optimize costs while maintaining durability",
                                Severity = "Low",
                                BusinessImpact = "Higher storage costs for backup data without impact on data protection"
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed, provide general recommendation
                    findings.Add(new BusinessContinuityFinding
                    {
                        Category = "Backup",
                        ResourceId = storageId,
                        ResourceName = storageName,
                        Issue = "Storage account backup and protection configuration should be reviewed",
                        Recommendation = "Verify replication settings, blob soft delete, versioning, and backup policies",
                        Severity = "Low",
                        BusinessImpact = "Incomplete storage protection may result in data loss during failures"
                    });
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVMBackupPoliciesAsync(List<AzureResource> virtualMachines, List<AzureResource> recoveryVaults, List<BusinessContinuityFinding> findings)
    {
        if (!recoveryVaults.Any())
        {
            // Already handled in main analysis
            return;
        }

        // Analyze VM backup configuration more deeply
        var productionVMs = virtualMachines.Where(vm =>
            vm.Tags.ContainsKey("Environment") &&
            vm.Tags["Environment"].ToLowerInvariant().Contains("prod")).ToList();

        var developmentVMs = virtualMachines.Where(vm =>
            vm.Tags.ContainsKey("Environment") &&
            (vm.Tags["Environment"].ToLowerInvariant().Contains("dev") ||
             vm.Tags["Environment"].ToLowerInvariant().Contains("test"))).ToList();

        // Production VMs should have more stringent backup requirements
        if (productionVMs.Any())
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = "vm.backup.policies.production",
                ResourceName = "Production VM Backup Policies",
                Issue = $"Found {productionVMs.Count} production VMs requiring backup policy verification",
                Recommendation = "Verify production VMs have daily backup policies with appropriate retention (minimum 30 days daily, 12 weeks weekly, 12 months monthly)",
                Severity = "High",
                BusinessImpact = "Production VMs require stringent backup policies to meet RTO/RPO requirements"
            });
        }

        // Check for cross-region restore capability
        if (recoveryVaults.Count > 0)
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = "vm.backup.cross.region",
                ResourceName = "VM Cross-Region Restore",
                Issue = "VM backup cross-region restore capability should be verified",
                Recommendation = "Enable cross-region restore for Recovery Services Vaults to support disaster recovery scenarios",
                Severity = "Medium",
                BusinessImpact = "Limited recovery options if primary region becomes unavailable"
            });
        }

        // Check for instant restore and backup frequency
        foreach (var vm in productionVMs.Take(10))
        {
            await AnalyzeVMBackupStatusAsync(vm, findings, true); // Already implemented, but ensuring it's called
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSQLAdvancedBackupAsync(List<AzureResource> databases, List<BusinessContinuityFinding> findings)
    {
        var sqlDatabases = databases.Where(db => db.Type.ToLowerInvariant().Contains("microsoft.sql")).ToList();
        var sqlServers = new HashSet<string>();

        foreach (var db in sqlDatabases.Take(15))
        {
            string dbName = db.Name;
            string dbId = db.Id;

            // Extract server name from resource ID
            var serverName = ExtractSqlServerFromResourceId(dbId);
            if (!string.IsNullOrEmpty(serverName))
            {
                sqlServers.Add(serverName);
            }

            bool isProduction = db.Tags.ContainsKey("Environment") &&
                               db.Tags["Environment"].ToLowerInvariant().Contains("prod");

            // Enhanced SQL backup analysis
            if (!string.IsNullOrEmpty(db.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(db.Properties);

                    // Check service tier for advanced backup capabilities
                    if (properties.RootElement.TryGetProperty("currentServiceObjectiveName", out var serviceObjective))
                    {
                        var tier = serviceObjective.GetString()?.ToLowerInvariant();

                        // Check for Hyperscale tier specific backup considerations
                        if (tier?.Contains("hs_") == true) // Hyperscale tier
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "Hyperscale database backup strategy should be reviewed for unique characteristics",
                                Recommendation = "Verify Hyperscale backup policies, snapshot frequency, and understand restore capabilities specific to Hyperscale architecture",
                                Severity = "Medium",
                                BusinessImpact = "Hyperscale databases have different backup and restore characteristics requiring specific planning"
                            });
                        }

                        // Business Critical tier should have read replicas consideration
                        if (tier?.Contains("bc_") == true && isProduction) // Business Critical tier
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "Business Critical database should consider read replicas for disaster recovery",
                                Recommendation = "Implement geo-replicated read replicas or auto-failover groups for Business Critical production databases",
                                Severity = "Medium",
                                BusinessImpact = "Business Critical databases require enhanced DR capabilities for zero-downtime scenarios"
                            });
                        }
                    }

                    // Check for geo-replication and failover groups
                    if (isProduction)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "Backup",
                            ResourceId = dbId,
                            ResourceName = dbName,
                            Issue = "Production SQL Database geo-replication and auto-failover configuration should be verified",
                            Recommendation = "Implement auto-failover groups with read-write and read-only replicas in secondary regions for production databases",
                            Severity = "High",
                            BusinessImpact = "Production databases without geo-replication vulnerable to extended downtime during regional outages"
                        });
                    }

                    // Check backup retention and LTR policies
                    if (properties.RootElement.TryGetProperty("requestedBackupStorageRedundancy", out var backupRedundancy))
                    {
                        var redundancy = backupRedundancy.GetString()?.ToLowerInvariant();
                        if (redundancy == "local" && isProduction)
                        {
                            findings.Add(new BusinessContinuityFinding
                            {
                                Category = "Backup",
                                ResourceId = dbId,
                                ResourceName = dbName,
                                Issue = "Production SQL Database using locally redundant backup storage",
                                Recommendation = "Configure geo-redundant backup storage and implement Long-Term Retention (LTR) policies for compliance and disaster recovery",
                                Severity = "High",
                                BusinessImpact = "Database backups vulnerable to regional outages with locally redundant storage"
                            });
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed
                }
            }

            // Always recommend LTR verification for production databases
            if (isProduction && !dbName.ToLowerInvariant().Contains("master"))
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "Backup",
                    ResourceId = dbId,
                    ResourceName = dbName,
                    Issue = "Production database Long-Term Retention (LTR) policies should be configured",
                    Recommendation = "Configure LTR policies for weekly, monthly, and yearly backups beyond standard 35-day retention for compliance and long-term recovery needs",
                    Severity = "Medium",
                    BusinessImpact = "Limited long-term recovery options without LTR policies for compliance and historical data recovery"
                });
            }
        }

        // Server-level recommendations for unique SQL servers
        foreach (var serverName in sqlServers.Take(5))
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "Backup",
                ResourceId = $"sql.server.{serverName}",
                ResourceName = $"SQL Server: {serverName}",
                Issue = "SQL Server should have automatic tuning and backup monitoring enabled",
                Recommendation = "Enable automatic tuning, backup monitoring alerts, and consider Managed Instance for enhanced backup capabilities",
                Severity = "Low",
                BusinessImpact = "Manual backup monitoring may miss failures or performance issues"
            });
        }

        await Task.CompletedTask;
    }

    private string ExtractSqlServerFromResourceId(string resourceId)
    {
        try
        {
            // Extract server name from resource ID pattern: /subscriptions/.../servers/servername/databases/dbname
            var parts = resourceId.Split('/');
            var serverIndex = Array.IndexOf(parts, "servers");
            if (serverIndex >= 0 && serverIndex + 1 < parts.Length)
            {
                return parts[serverIndex + 1];
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return string.Empty;
    }

    private async Task AnalyzeBackupDependenciesAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        // Check storage accounts for backup dependencies
        var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();

        foreach (var storage in storageAccounts.Take(10)) // Limit for performance
        {
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
                                Category = "Backup",
                                ResourceId = storage.Id,
                                ResourceName = storage.Name,
                                Issue = "Storage account uses locally redundant storage, vulnerable to regional outages",
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

        await Task.CompletedTask;
    }

    private decimal CalculateBackupCoverageScore(BackupAnalysis backupAnalysis, List<BusinessContinuityFinding> findings)
    {
        var baseScore = backupAnalysis.BackupCoveragePercentage;

        // Apply penalties for critical findings
        var criticalFindings = findings.Count(f => f.Severity == "High");
        var mediumFindings = findings.Count(f => f.Severity == "Medium");

        var penalty = (criticalFindings * 15) + (mediumFindings * 5);
        var finalScore = Math.Max(0, baseScore - penalty);

        _logger.LogInformation("Backup Coverage Score: Base {BaseScore}%, Penalty {Penalty}, Final {FinalScore}%",
            baseScore, penalty, finalScore);

        return Math.Round(finalScore, 2);
    }
}