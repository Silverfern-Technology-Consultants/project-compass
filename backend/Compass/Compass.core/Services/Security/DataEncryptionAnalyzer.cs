using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.Security;

public interface IDataEncryptionAnalyzer
{
    Task<List<SecurityFinding>> AnalyzeDataEncryptionStatusAsync(
        List<AzureResource> allResources,
        CancellationToken cancellationToken = default);
}

public class DataEncryptionAnalyzer : IDataEncryptionAnalyzer
{
    private readonly ILogger<DataEncryptionAnalyzer> _logger;

    public DataEncryptionAnalyzer(ILogger<DataEncryptionAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<List<SecurityFinding>> AnalyzeDataEncryptionStatusAsync(
        List<AzureResource> allResources,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing data encryption status across Azure services");

        var findings = new List<SecurityFinding>();

        try
        {
            // Analyze storage account encryption
            var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();
            await AnalyzeStorageEncryptionAsync(storageAccounts, findings);

            // Analyze SQL Server encryption
            var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers")).ToList();
            await AnalyzeSqlEncryptionAsync(sqlServers, findings);

            // Analyze Cosmos DB encryption
            var cosmosDbAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.documentdb/databaseaccounts").ToList();
            await AnalyzeCosmosDbEncryptionAsync(cosmosDbAccounts, findings);

            // Analyze virtual machine disk encryption
            var virtualMachines = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();
            await AnalyzeVmDiskEncryptionAsync(virtualMachines, findings);

            // Analyze Key Vault encryption and key management
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).ToList();
            await AnalyzeKeyVaultEncryptionAsync(keyVaults, findings);

            // Analyze managed disk encryption
            var managedDisks = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/disks").ToList();
            await AnalyzeManagedDiskEncryptionAsync(managedDisks, findings);

            // Analyze Azure SQL Database encryption
            var sqlDatabases = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.sql/servers/databases").ToList();
            await AnalyzeSqlDatabaseEncryptionAsync(sqlDatabases, findings);

            // Analyze App Service encryption
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();
            await AnalyzeAppServiceEncryptionAsync(appServices, findings);

            // Overall encryption posture assessment
            await AnalyzeOverallEncryptionPostureAsync(allResources, findings);

            _logger.LogInformation("Data encryption analysis completed for {ResourceCount} resources", allResources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze data encryption status");

            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = "error.encryption.analysis",
                ResourceName = "Data Encryption Analysis",
                SecurityControl = "Encryption Assessment",
                Issue = "Failed to analyze data encryption status across Azure services",
                Recommendation = "Review permissions and retry data encryption analysis",
                Severity = "Medium",
                ComplianceFramework = "General"
            });
        }

        return findings;
    }

    private async Task AnalyzeStorageEncryptionAsync(List<AzureResource> storageAccounts, List<SecurityFinding> findings)
    {
        foreach (var storageAccount in storageAccounts.Take(20))
        {
            if (!string.IsNullOrEmpty(storageAccount.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(storageAccount.Properties);

                    // Check for customer-managed keys
                    var hasCustomerManagedKeys = false;
                    if (properties.RootElement.TryGetProperty("encryption", out var encryption))
                    {
                        if (encryption.TryGetProperty("keySource", out var keySource))
                        {
                            hasCustomerManagedKeys = keySource.GetString()?.ToLowerInvariant() == "microsoft.keyvault";
                        }
                    }

                    if (!hasCustomerManagedKeys)
                    {
                        var severity = DetermineEncryptionSeverity(storageAccount);

                        findings.Add(new SecurityFinding
                        {
                            Category = "DataEncryption",
                            ResourceId = storageAccount.Id,
                            ResourceName = storageAccount.Name,
                            SecurityControl = "Encryption Key Management",
                            Issue = "Storage account is using Microsoft-managed keys instead of customer-managed keys",
                            Recommendation = "Configure customer-managed keys (CMK) stored in Azure Key Vault for enhanced control over encryption keys, especially for production environments",
                            Severity = severity,
                            ComplianceFramework = "Azure Security Benchmark"
                        });
                    }

                    // Check for infrastructure encryption (double encryption)
                    if (properties.RootElement.TryGetProperty("encryption", out var encryptionSettings))
                    {
                        if (!encryptionSettings.TryGetProperty("requireInfrastructureEncryption", out var infraEncryption) ||
                            !infraEncryption.GetBoolean())
                        {
                            var isProduction = IsProductionResource(storageAccount);
                            if (isProduction)
                            {
                                findings.Add(new SecurityFinding
                                {
                                    Category = "DataEncryption",
                                    ResourceId = storageAccount.Id,
                                    ResourceName = storageAccount.Name,
                                    SecurityControl = "Infrastructure Encryption",
                                    Issue = "Production storage account does not have infrastructure encryption (double encryption) enabled",
                                    Recommendation = "Enable infrastructure encryption for additional security layer for production data",
                                    Severity = "Medium",
                                    ComplianceFramework = "High Security Environments"
                                });
                            }
                        }
                    }

                    // Check for secure transfer requirement
                    if (properties.RootElement.TryGetProperty("supportsHttpsTrafficOnly", out var httpsOnly))
                    {
                        if (!httpsOnly.GetBoolean())
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "DataEncryption",
                                ResourceId = storageAccount.Id,
                                ResourceName = storageAccount.Name,
                                SecurityControl = "Encryption in Transit",
                                Issue = "Storage account allows HTTP traffic, potentially exposing data in transit",
                                Recommendation = "Enable 'Secure transfer required' to enforce HTTPS/TLS for all storage account communications",
                                Severity = "High",
                                ComplianceFramework = "Azure Security Benchmark"
                            });
                        }
                    }

                    // Check for minimum TLS version
                    if (properties.RootElement.TryGetProperty("minimumTlsVersion", out var tlsVersion))
                    {
                        var minTlsVersion = tlsVersion.GetString();
                        if (minTlsVersion != "TLS1_2")
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "DataEncryption",
                                ResourceId = storageAccount.Id,
                                ResourceName = storageAccount.Name,
                                SecurityControl = "Encryption in Transit",
                                Issue = $"Storage account minimum TLS version is set to {minTlsVersion} instead of TLS 1.2",
                                Recommendation = "Set minimum TLS version to 1.2 or higher to ensure secure communication protocols",
                                Severity = "Medium",
                                ComplianceFramework = "Azure Security Benchmark"
                            });
                        }
                    }

                    // Check for blob versioning and soft delete (data protection features)
                    await AnalyzeStorageDataProtectionAsync(storageAccount, properties, findings);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse storage account properties for {StorageName}", storageAccount.Name);
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeStorageDataProtectionAsync(AzureResource storageAccount, JsonDocument properties, List<SecurityFinding> findings)
    {
        // Check for blob versioning
        if (properties.RootElement.TryGetProperty("blobRestorePolicy", out var blobRestore))
        {
            if (!blobRestore.TryGetProperty("enabled", out var enabled) || !enabled.GetBoolean())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DataEncryption",
                    ResourceId = storageAccount.Id,
                    ResourceName = storageAccount.Name,
                    SecurityControl = "Data Protection",
                    Issue = "Storage account does not have point-in-time restore enabled for blob data",
                    Recommendation = "Enable point-in-time restore for blob containers to protect against accidental deletion or corruption",
                    Severity = "Low",
                    ComplianceFramework = "Data Protection Best Practice"
                });
            }
        }

        // Check for blob soft delete
        if (properties.RootElement.TryGetProperty("deleteRetentionPolicy", out var deleteRetention))
        {
            if (!deleteRetention.TryGetProperty("enabled", out var softDeleteEnabled) || !softDeleteEnabled.GetBoolean())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DataEncryption",
                    ResourceId = storageAccount.Id,
                    ResourceName = storageAccount.Name,
                    SecurityControl = "Data Protection",
                    Issue = "Storage account does not have blob soft delete enabled",
                    Recommendation = "Enable blob soft delete to protect against accidental deletion of blob data",
                    Severity = "Medium",
                    ComplianceFramework = "Data Protection Best Practice"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSqlEncryptionAsync(List<AzureResource> sqlServers, List<SecurityFinding> findings)
    {
        foreach (var sqlServer in sqlServers.Take(15))
        {
            // Check for TDE (Transparent Data Encryption) configuration
            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = sqlServer.Id,
                ResourceName = sqlServer.Name,
                SecurityControl = "Database Encryption",
                Issue = "SQL Server TDE (Transparent Data Encryption) configuration should be verified with customer-managed keys",
                Recommendation = "Ensure TDE is enabled with customer-managed keys for all SQL databases. Verify encryption status for all databases on this server",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Check for Always Encrypted configuration
            if (IsProductionResource(sqlServer))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DataEncryption",
                    ResourceId = sqlServer.Id,
                    ResourceName = sqlServer.Name,
                    SecurityControl = "Column-Level Encryption",
                    Issue = "Production SQL Server should implement Always Encrypted for sensitive data columns",
                    Recommendation = "Implement Always Encrypted for sensitive data columns (PII, financial data) to provide client-side encryption with separation of data and key management",
                    Severity = "Medium",
                    ComplianceFramework = "Data Protection Standards"
                });
            }

            // Check for backup encryption
            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = sqlServer.Id,
                ResourceName = sqlServer.Name,
                SecurityControl = "Backup Encryption",
                Issue = "SQL Server backup encryption should be verified",
                Recommendation = "Ensure automated backups are encrypted with customer-managed keys and verify backup encryption policies",
                Severity = "Low",
                ComplianceFramework = "Backup Security Best Practice"
            });

            // Check for SQL Server auditing encryption
            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = sqlServer.Id,
                ResourceName = sqlServer.Name,
                SecurityControl = "Audit Log Encryption",
                Issue = "SQL Server audit logs should be encrypted and stored securely",
                Recommendation = "Configure audit log encryption and secure storage for SQL Server audit trails to maintain data integrity and confidentiality",
                Severity = "Low",
                ComplianceFramework = "Compliance Auditing Standards"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSqlDatabaseEncryptionAsync(List<AzureResource> sqlDatabases, List<SecurityFinding> findings)
    {
        foreach (var database in sqlDatabases.Take(20))
        {
            // Database-level encryption analysis
            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = database.Id,
                ResourceName = database.Name,
                SecurityControl = "Database Encryption",
                Issue = "SQL Database encryption status should be verified at the database level",
                Recommendation = "Verify TDE is enabled for this specific database and consider implementing Always Encrypted for sensitive columns",
                Severity = "Medium",
                ComplianceFramework = "Database Security Standards"
            });

            // Check for production database additional requirements
            if (IsProductionResource(database))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DataEncryption",
                    ResourceId = database.Id,
                    ResourceName = database.Name,
                    SecurityControl = "Production Database Security",
                    Issue = "Production database requires enhanced encryption and monitoring",
                    Recommendation = "Implement comprehensive encryption strategy including TDE with CMK, Always Encrypted for sensitive data, and encrypted backups",
                    Severity = "High",
                    ComplianceFramework = "Production Security Standards"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeCosmosDbEncryptionAsync(List<AzureResource> cosmosDbAccounts, List<SecurityFinding> findings)
    {
        foreach (var cosmosDb in cosmosDbAccounts.Take(15))
        {
            if (!string.IsNullOrEmpty(cosmosDb.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(cosmosDb.Properties);

                    // Check for customer-managed keys
                    var hasCustomerManagedKeys = false;
                    if (properties.RootElement.TryGetProperty("keyVaultKeyUri", out var keyVaultUri))
                    {
                        hasCustomerManagedKeys = !string.IsNullOrEmpty(keyVaultUri.GetString());
                    }

                    if (!hasCustomerManagedKeys)
                    {
                        var severity = DetermineEncryptionSeverity(cosmosDb);

                        findings.Add(new SecurityFinding
                        {
                            Category = "DataEncryption",
                            ResourceId = cosmosDb.Id,
                            ResourceName = cosmosDb.Name,
                            SecurityControl = "Database Encryption",
                            Issue = "Cosmos DB account is using service-managed keys instead of customer-managed keys",
                            Recommendation = "Configure customer-managed keys for Cosmos DB encryption to maintain control over encryption keys and meet compliance requirements",
                            Severity = severity,
                            ComplianceFramework = "Azure Security Benchmark"
                        });
                    }

                    // Check for backup encryption
                    if (properties.RootElement.TryGetProperty("backupPolicy", out var backupPolicy))
                    {
                        if (backupPolicy.TryGetProperty("type", out var backupType))
                        {
                            var backupTypeValue = backupType.GetString()?.ToLowerInvariant();
                            if (backupTypeValue == "continuous")
                            {
                                findings.Add(new SecurityFinding
                                {
                                    Category = "DataEncryption",
                                    ResourceId = cosmosDb.Id,
                                    ResourceName = cosmosDb.Name,
                                    SecurityControl = "Backup Encryption",
                                    Issue = "Cosmos DB continuous backup encryption should be verified",
                                    Recommendation = "Verify that continuous backup data is properly encrypted and consider customer-managed keys for backup encryption",
                                    Severity = "Low",
                                    ComplianceFramework = "Backup Security Best Practice"
                                });
                            }
                        }
                    }

                    // Check for network encryption in transit
                    if (properties.RootElement.TryGetProperty("disableKeyBasedMetadataWriteAccess", out var keyBasedAccess))
                    {
                        if (!keyBasedAccess.GetBoolean())
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "DataEncryption",
                                ResourceId = cosmosDb.Id,
                                ResourceName = cosmosDb.Name,
                                SecurityControl = "Access Control Encryption",
                                Issue = "Cosmos DB allows key-based metadata write access",
                                Recommendation = "Disable key-based metadata write access to enforce Azure AD authentication and enhance security",
                                Severity = "Medium",
                                ComplianceFramework = "Azure Security Benchmark"
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Cosmos DB properties for {CosmosDbName}", cosmosDb.Name);
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVmDiskEncryptionAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        if (virtualMachines.Any())
        {
            var productionVMs = virtualMachines.Where(vm => IsProductionResource(vm)).Count();
            var totalVMs = virtualMachines.Count;

            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = "vm.disk.encryption",
                ResourceName = "Virtual Machine Disk Encryption",
                SecurityControl = "Disk Encryption",
                Issue = $"Disk encryption status should be verified for {totalVMs} virtual machines ({productionVMs} production VMs)",
                Recommendation = "Ensure Azure Disk Encryption (ADE) is enabled for all VM OS and data disks. Use customer-managed keys stored in Azure Key Vault for enhanced security, especially for production VMs",
                Severity = productionVMs > 0 ? "High" : "Medium",
                ComplianceFramework = "CIS Azure Foundations"
            });

            // Check for BitLocker/DM-Crypt recommendations
            if (productionVMs > 0)
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DataEncryption",
                    ResourceId = "vm.encryption.compliance",
                    ResourceName = "VM Encryption Compliance",
                    SecurityControl = "Encryption Standards",
                    Issue = $"Production VMs ({productionVMs}) require compliance-grade encryption verification",
                    Recommendation = "Verify BitLocker (Windows) or dm-crypt (Linux) encryption is properly configured with appropriate cipher suites and key management practices",
                    Severity = "Medium",
                    ComplianceFramework = "Compliance Encryption Standards"
                });
            }

            // VM-specific encryption analysis
            foreach (var vm in virtualMachines.Take(10))
            {
                await AnalyzeIndividualVmEncryptionAsync(vm, findings);
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeIndividualVmEncryptionAsync(AzureResource vm, List<SecurityFinding> findings)
    {
        if (!string.IsNullOrEmpty(vm.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(vm.Properties);

                // Check for encryption at host
                if (properties.RootElement.TryGetProperty("securityProfile", out var securityProfile))
                {
                    if (!securityProfile.TryGetProperty("encryptionAtHost", out var encryptionAtHost) ||
                        !encryptionAtHost.GetBoolean())
                    {
                        var severity = IsProductionResource(vm) ? "High" : "Medium";

                        findings.Add(new SecurityFinding
                        {
                            Category = "DataEncryption",
                            ResourceId = vm.Id,
                            ResourceName = vm.Name,
                            SecurityControl = "Host-Level Encryption",
                            Issue = "Virtual machine does not have encryption at host enabled",
                            Recommendation = "Enable encryption at host to encrypt temporary disks and disk caches for comprehensive VM encryption",
                            Severity = severity,
                            ComplianceFramework = "Azure Security Benchmark"
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse VM properties for {VmName}", vm.Name);
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeManagedDiskEncryptionAsync(List<AzureResource> managedDisks, List<SecurityFinding> findings)
    {
        var unencryptedDisks = 0;
        var totalDisks = managedDisks.Count;

        foreach (var disk in managedDisks.Take(25))
        {
            if (!string.IsNullOrEmpty(disk.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(disk.Properties);

                    // Check for encryption settings
                    var isEncrypted = false;
                    var hasCustomerManagedKey = false;

                    if (properties.RootElement.TryGetProperty("encryptionSettingsCollection", out var encryptionSettings))
                    {
                        if (encryptionSettings.TryGetProperty("enabled", out var enabled))
                        {
                            isEncrypted = enabled.GetBoolean();
                        }
                    }

                    // Check for server-side encryption with customer-managed keys
                    if (properties.RootElement.TryGetProperty("encryption", out var encryption))
                    {
                        if (encryption.TryGetProperty("type", out var encryptionType))
                        {
                            hasCustomerManagedKey = encryptionType.GetString()?.ToLowerInvariant() == "customerkey";
                        }
                    }

                    if (!isEncrypted)
                    {
                        unencryptedDisks++;

                        findings.Add(new SecurityFinding
                        {
                            Category = "DataEncryption",
                            ResourceId = disk.Id,
                            ResourceName = disk.Name,
                            SecurityControl = "Disk Encryption",
                            Issue = "Managed disk does not have encryption enabled",
                            Recommendation = "Enable Azure Disk Encryption or server-side encryption for this managed disk",
                            Severity = "High",
                            ComplianceFramework = "Azure Security Benchmark"
                        });
                    }
                    else if (!hasCustomerManagedKey && IsHighValueDisk(disk))
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category = "DataEncryption",
                            ResourceId = disk.Id,
                            ResourceName = disk.Name,
                            SecurityControl = "Encryption Key Management",
                            Issue = "High-value managed disk is using platform-managed keys instead of customer-managed keys",
                            Recommendation = "Configure customer-managed keys for sensitive or production disk encryption",
                            Severity = "Medium",
                            ComplianceFramework = "Azure Security Benchmark"
                        });
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse managed disk properties for {DiskName}", disk.Name);
                    unencryptedDisks++; // Assume unencrypted if we can't parse
                }
            }
            else
            {
                unencryptedDisks++; // Assume unencrypted if no properties
            }
        }

        if (unencryptedDisks > 0)
        {
            var severity = (decimal)unencryptedDisks / totalDisks > 0.5m ? "High" : "Medium";

            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = "managed.disk.encryption.summary",
                ResourceName = "Managed Disk Encryption Summary",
                SecurityControl = "Disk Encryption",
                Issue = $"Found {unencryptedDisks} potentially unencrypted managed disks out of {totalDisks} total disks",
                Recommendation = "Enable encryption at rest for all managed disks using Azure Disk Encryption or server-side encryption with customer-managed keys",
                Severity = severity,
                ComplianceFramework = "Azure Security Benchmark"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeKeyVaultEncryptionAsync(List<AzureResource> keyVaults, List<SecurityFinding> findings)
    {
        foreach (var keyVault in keyVaults.Take(15))
        {
            if (!string.IsNullOrEmpty(keyVault.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(keyVault.Properties);

                    // Check for HSM-protected keys capability
                    if (properties.RootElement.TryGetProperty("sku", out var sku))
                    {
                        if (sku.TryGetProperty("name", out var skuName))
                        {
                            var skuValue = skuName.GetString()?.ToLowerInvariant();
                            if (skuValue == "standard" && IsProductionResource(keyVault))
                            {
                                findings.Add(new SecurityFinding
                                {
                                    Category = "DataEncryption",
                                    ResourceId = keyVault.Id,
                                    ResourceName = keyVault.Name,
                                    SecurityControl = "Key Protection",
                                    Issue = "Production Key Vault is using Standard SKU which doesn't support HSM-protected keys",
                                    Recommendation = "Consider upgrading to Premium SKU for HSM-protected keys to meet high-security and compliance requirements for production environments",
                                    Severity = "Medium",
                                    ComplianceFramework = "High Security Environments"
                                });
                            }
                        }
                    }

                    // Check for purge protection
                    if (properties.RootElement.TryGetProperty("enablePurgeProtection", out var purgeProtection))
                    {
                        if (!purgeProtection.GetBoolean())
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "DataEncryption",
                                ResourceId = keyVault.Id,
                                ResourceName = keyVault.Name,
                                SecurityControl = "Key Protection",
                                Issue = "Key Vault does not have purge protection enabled",
                                Recommendation = "Enable purge protection to prevent permanent deletion of keys, secrets, and certificates during the retention period",
                                Severity = "Medium",
                                ComplianceFramework = "Azure Security Benchmark"
                            });
                        }
                    }

                    // Check for soft delete
                    if (properties.RootElement.TryGetProperty("enableSoftDelete", out var softDelete))
                    {
                        if (!softDelete.GetBoolean())
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "DataEncryption",
                                ResourceId = keyVault.Id,
                                ResourceName = keyVault.Name,
                                SecurityControl = "Key Protection",
                                Issue = "Key Vault does not have soft delete enabled",
                                Recommendation = "Enable soft delete to protect against accidental deletion of keys, secrets, and certificates",
                                Severity = "High",
                                ComplianceFramework = "Azure Security Benchmark"
                            });
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Key Vault properties for {KeyVaultName}", keyVault.Name);
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAppServiceEncryptionAsync(List<AzureResource> appServices, List<SecurityFinding> findings)
    {
        foreach (var appService in appServices.Take(15))
        {
            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = appService.Id,
                ResourceName = appService.Name,
                SecurityControl = "Application Encryption",
                Issue = "App Service encryption and HTTPS configuration should be verified",
                Recommendation = "Ensure HTTPS-only is enabled, minimum TLS version is set to 1.2, and consider using customer-managed certificates stored in Key Vault",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Check for production app services
            if (IsProductionResource(appService))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DataEncryption",
                    ResourceId = appService.Id,
                    ResourceName = appService.Name,
                    SecurityControl = "Production Application Security",
                    Issue = "Production App Service requires enhanced encryption and certificate management",
                    Recommendation = "Implement comprehensive security including HTTPS-only, TLS 1.2+, custom domain certificates from Key Vault, and connection string encryption",
                    Severity = "High",
                    ComplianceFramework = "Production Security Standards"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeOverallEncryptionPostureAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        // Analyze overall encryption posture across the environment
        var encryptionCapableResources = allResources.Where(r =>
            r.Type.ToLowerInvariant().Contains("storage") ||
            r.Type.ToLowerInvariant().Contains("sql") ||
            r.Type.ToLowerInvariant().Contains("documentdb") ||
            r.Type.ToLowerInvariant().Contains("keyvault") ||
            r.Type.ToLowerInvariant().Contains("compute") ||
            r.Type.ToLowerInvariant().Contains("web/sites")).Count();

        var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).Count();
        var productionResources = allResources.Where(r => IsProductionResource(r)).Count();

        // Check for centralized key management
        if (encryptionCapableResources > 15 && keyVaults == 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "DataEncryption",
                ResourceId = "encryption.key.management",
                ResourceName = "Centralized Key Management",
                SecurityControl = "Key Management Strategy",
                Issue = $"Large number of encryption-capable resources ({encryptionCapableResources}) without centralized key management",
                Recommendation = "Deploy Azure Key Vault for centralized key management and implement customer-managed keys across Azure services",
                Severity = "High",
                ComplianceFramework = "Encryption Governance Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private string DetermineEncryptionSeverity(AzureResource resource)
    {
        // Higher severity for production resources
        if (IsProductionResource(resource))
        {
            return "High";
        }

        // Check for sensitive data indicators in tags or names
        var hasDataClassification = resource.Tags.ContainsKey("DataClassification") ||
                                   resource.Tags.ContainsKey("Sensitivity");

        if (hasDataClassification)
        {
            var classification = resource.Tags.GetValueOrDefault("DataClassification", "")?.ToLowerInvariant() ??
                               resource.Tags.GetValueOrDefault("Sensitivity", "")?.ToLowerInvariant() ?? "";

            if (classification.Contains("confidential") || classification.Contains("restricted") || classification.Contains("sensitive"))
            {
                return "High";
            }
        }

        // Check for compliance requirements
        if (resource.Tags.ContainsKey("ComplianceFramework"))
        {
            var compliance = resource.Tags["ComplianceFramework"].ToLowerInvariant();
            if (compliance.Contains("pci") || compliance.Contains("hipaa") || compliance.Contains("sox"))
            {
                return "High";
            }
        }

        return "Medium";
    }

    private bool IsProductionResource(AzureResource resource)
    {
        return resource.Environment?.ToLowerInvariant().Contains("prod") == true ||
               resource.Tags.ContainsKey("Environment") &&
               resource.Tags["Environment"].ToLowerInvariant().Contains("prod");
    }

    private bool IsHighValueDisk(AzureResource disk)
    {
        // Check if disk is associated with production resources or contains sensitive data
        return IsProductionResource(disk) ||
               disk.Name.ToLowerInvariant().Contains("data") ||
               disk.Name.ToLowerInvariant().Contains("db") ||
               disk.Tags.ContainsKey("DataClassification");
    }
}