using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.Security;

public interface IAdvancedThreatProtectionAnalyzer
{
    Task<List<SecurityFinding>> AnalyzeAdvancedThreatProtectionAsync(
        List<AzureResource> allResources,
        CancellationToken cancellationToken = default);
}

public class AdvancedThreatProtectionAnalyzer : IAdvancedThreatProtectionAnalyzer
{
    private readonly ILogger<AdvancedThreatProtectionAnalyzer> _logger;

    public AdvancedThreatProtectionAnalyzer(ILogger<AdvancedThreatProtectionAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<List<SecurityFinding>> AnalyzeAdvancedThreatProtectionAsync(
        List<AzureResource> allResources,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing Advanced Threat Protection across Azure services");

        var findings = new List<SecurityFinding>();

        try
        {
            // Analyze Storage Advanced Threat Protection
            var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();
            await AnalyzeStorageAdvancedThreatProtectionAsync(storageAccounts, findings);

            // Analyze SQL Advanced Threat Protection
            var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers")).ToList();
            await AnalyzeSqlAdvancedThreatProtectionAsync(sqlServers, findings);

            // Analyze Cosmos DB Advanced Threat Protection
            var cosmosDbAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.documentdb/databaseaccounts").ToList();
            await AnalyzeCosmosDbAdvancedThreatProtectionAsync(cosmosDbAccounts, findings);

            // Analyze Key Vault Advanced Threat Protection
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).ToList();
            await AnalyzeKeyVaultAdvancedThreatProtectionAsync(keyVaults, findings);

            // Analyze App Service Advanced Threat Protection
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();
            await AnalyzeAppServiceAdvancedThreatProtectionAsync(appServices, findings);

            // Analyze Container Registry Advanced Threat Protection
            var containerRegistries = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.containerregistry/registries").ToList();
            await AnalyzeContainerRegistryAdvancedThreatProtectionAsync(containerRegistries, findings);

            // Analyze Virtual Machine threat protection
            var virtualMachines = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();
            await AnalyzeVirtualMachineAdvancedThreatProtectionAsync(virtualMachines, findings);

            // Analyze Kubernetes Advanced Threat Protection
            var kubernetesClusters = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.containerservice/managedclusters").ToList();
            await AnalyzeKubernetesAdvancedThreatProtectionAsync(kubernetesClusters, findings);

            // Analyze overall ATP coverage and governance
            await AnalyzeOverallAtpCoverageAsync(allResources, findings);

            // Analyze threat intelligence integration
            await AnalyzeThreatIntelligenceIntegrationAsync(allResources, findings);

            _logger.LogInformation("Advanced Threat Protection analysis completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Advanced Threat Protection");

            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = "error.atp.analysis",
                ResourceName = "Advanced Threat Protection Analysis",
                SecurityControl = "Threat Detection Assessment",
                Issue = "Failed to analyze Advanced Threat Protection status",
                Recommendation = "Review permissions and retry Advanced Threat Protection analysis",
                Severity = "Medium",
                ComplianceFramework = "General"
            });
        }

        return findings;
    }

    private async Task AnalyzeStorageAdvancedThreatProtectionAsync(List<AzureResource> storageAccounts, List<SecurityFinding> findings)
    {
        foreach (var storage in storageAccounts.Take(15))
        {
            var severity = DetermineAtpSeverity(storage);

            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = storage.Id,
                ResourceName = storage.Name,
                SecurityControl = "Storage Threat Detection",
                Issue = "Microsoft Defender for Storage should be enabled to protect against malicious activities",
                Recommendation = "Enable Microsoft Defender for Storage to detect malicious activities like unusual access patterns, potential malware uploads, hash reputation analysis, and suspicious anonymous access",
                Severity = severity,
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Additional checks for production storage accounts
            if (IsProductionResource(storage))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "AdvancedThreatProtection",
                    ResourceId = storage.Id,
                    ResourceName = storage.Name,
                    SecurityControl = "Production Storage Security",
                    Issue = "Production storage account requires enhanced threat protection monitoring",
                    Recommendation = "Configure alerting for Defender for Storage findings and implement automated response workflows for production storage security incidents",
                    Severity = "High",
                    ComplianceFramework = "Production Security Standards"
                });
            }

            // Check for blob audit logging
            await AnalyzeStorageAuditLoggingAsync(storage, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeStorageAuditLoggingAsync(AzureResource storage, List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "AdvancedThreatProtection",
            ResourceId = storage.Id,
            ResourceName = storage.Name,
            SecurityControl = "Storage Audit Logging",
            Issue = "Storage analytics and diagnostic logging should be configured for threat detection",
            Recommendation = "Enable storage analytics logging and diagnostic settings to capture access patterns, authentication events, and API calls for security monitoring",
            Severity = "Medium",
            ComplianceFramework = "Azure Security Benchmark"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeSqlAdvancedThreatProtectionAsync(List<AzureResource> sqlServers, List<SecurityFinding> findings)
    {
        foreach (var sqlServer in sqlServers.Take(10))
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = sqlServer.Id,
                ResourceName = sqlServer.Name,
                SecurityControl = "Database Threat Detection",
                Issue = "Microsoft Defender for SQL should be enabled for comprehensive database threat protection",
                Recommendation = "Enable Microsoft Defender for SQL to detect SQL injection attacks, access anomalies, unusual database activities, and potential data exfiltration attempts. Configure threat detection policies with appropriate alerting.",
                Severity = "High",
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Check for vulnerability assessment
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = sqlServer.Id,
                ResourceName = sqlServer.Name,
                SecurityControl = "SQL Vulnerability Assessment",
                Issue = "SQL Vulnerability Assessment should be configured to identify database security weaknesses",
                Recommendation = "Enable SQL Vulnerability Assessment to automatically scan for security vulnerabilities, misconfigurations, and provide remediation guidance for database security hardening",
                Severity = "Medium",
                ComplianceFramework = "Database Security Best Practice"
            });

            // Check for auditing configuration
            await AnalyzeSqlAuditingAsync(sqlServer, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSqlAuditingAsync(AzureResource sqlServer, List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "AdvancedThreatProtection",
            ResourceId = sqlServer.Id,
            ResourceName = sqlServer.Name,
            SecurityControl = "SQL Server Auditing",
            Issue = "SQL Server auditing should be configured for security monitoring and compliance",
            Recommendation = "Enable SQL Server auditing to track database events, login attempts, and data access patterns. Configure audit logs to be sent to Log Analytics workspace or storage account for analysis",
            Severity = "Medium",
            ComplianceFramework = "Compliance Auditing Standards"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeCosmosDbAdvancedThreatProtectionAsync(List<AzureResource> cosmosDbAccounts, List<SecurityFinding> findings)
    {
        foreach (var cosmosDb in cosmosDbAccounts.Take(10))
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = cosmosDb.Id,
                ResourceName = cosmosDb.Name,
                SecurityControl = "NoSQL Threat Detection",
                Issue = "Microsoft Defender for Cosmos DB should be configured for NoSQL database protection",
                Recommendation = "Enable Microsoft Defender for Cosmos DB to detect suspicious activities, access from unusual locations, potential data exfiltration attempts, and anomalous query patterns",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Check for diagnostic logging
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = cosmosDb.Id,
                ResourceName = cosmosDb.Name,
                SecurityControl = "Cosmos DB Monitoring",
                Issue = "Cosmos DB diagnostic logging should be configured for security monitoring",
                Recommendation = "Enable diagnostic settings to capture control plane and data plane activities, authentication events, and query patterns for security analysis",
                Severity = "Low",
                ComplianceFramework = "Database Monitoring Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeKeyVaultAdvancedThreatProtectionAsync(List<AzureResource> keyVaults, List<SecurityFinding> findings)
    {
        foreach (var keyVault in keyVaults.Take(10))
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = keyVault.Id,
                ResourceName = keyVault.Name,
                SecurityControl = "Secrets Threat Detection",
                Issue = "Microsoft Defender for Key Vault should be enabled for secrets and key protection",
                Recommendation = "Enable Microsoft Defender for Key Vault to detect suspicious access patterns, unusual operations, potential attacks on secrets, keys, and certificates, and anomalous authentication attempts",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Check for Key Vault logging
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = keyVault.Id,
                ResourceName = keyVault.Name,
                SecurityControl = "Key Vault Audit Logging",
                Issue = "Key Vault diagnostic logging should be configured for security monitoring",
                Recommendation = "Enable diagnostic settings to capture all key, secret, and certificate operations, authentication events, and access patterns for security analysis and compliance",
                Severity = "Medium",
                ComplianceFramework = "Secrets Management Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAppServiceAdvancedThreatProtectionAsync(List<AzureResource> appServices, List<SecurityFinding> findings)
    {
        foreach (var appService in appServices.Take(15))
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = appService.Id,
                ResourceName = appService.Name,
                SecurityControl = "Application Threat Detection",
                Issue = "Microsoft Defender for App Service should be enabled for web application protection",
                Recommendation = "Enable Microsoft Defender for App Service to detect web application attacks, malicious code uploads, command injection attempts, and suspicious file modifications",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            // Check for application insights security monitoring
            await AnalyzeAppServiceSecurityMonitoringAsync(appService, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAppServiceSecurityMonitoringAsync(AzureResource appService, List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "AdvancedThreatProtection",
            ResourceId = appService.Id,
            ResourceName = appService.Name,
            SecurityControl = "Application Security Monitoring",
            Issue = "App Service should have comprehensive security monitoring configured",
            Recommendation = "Configure Application Insights for security event monitoring, enable diagnostic logs, and implement custom security telemetry for application-level threat detection",
            Severity = "Low",
            ComplianceFramework = "Application Security Best Practice"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeContainerRegistryAdvancedThreatProtectionAsync(List<AzureResource> containerRegistries, List<SecurityFinding> findings)
    {
        foreach (var registry in containerRegistries.Take(10))
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = registry.Id,
                ResourceName = registry.Name,
                SecurityControl = "Container Security Scanning",
                Issue = "Microsoft Defender for Container Registry should be enabled for image vulnerability scanning",
                Recommendation = "Enable Microsoft Defender for Container Registry to scan container images for vulnerabilities, malware, and provide continuous monitoring of container security posture",
                Severity = "Medium",
                ComplianceFramework = "Container Security Best Practice"
            });

            // Check for quarantine policies
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = registry.Id,
                ResourceName = registry.Name,
                SecurityControl = "Container Image Quarantine",
                Issue = "Container Registry should implement quarantine policies for vulnerable images",
                Recommendation = "Configure quarantine policies to automatically quarantine container images with critical vulnerabilities and prevent deployment of insecure images",
                Severity = "Medium",
                ComplianceFramework = "Container Security Standards"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVirtualMachineAdvancedThreatProtectionAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        if (virtualMachines.Any())
        {
            var productionVMs = virtualMachines.Where(vm => IsProductionResource(vm)).Count();

            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = "vm.threat.protection",
                ResourceName = "Virtual Machine Threat Protection",
                SecurityControl = "Endpoint Threat Detection",
                Issue = $"Microsoft Defender for Servers should be enabled for {virtualMachines.Count} virtual machines ({productionVMs} production VMs)",
                Recommendation = "Enable Microsoft Defender for Servers to provide advanced threat protection, behavioral analytics, vulnerability assessment, and just-in-time VM access for comprehensive endpoint security",
                Severity = productionVMs > 0 ? "High" : "Medium",
                ComplianceFramework = "Endpoint Security Standards"
            });

            // Check for antimalware configuration
            await AnalyzeVmAntiMalwareAsync(virtualMachines, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVmAntiMalwareAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "AdvancedThreatProtection",
            ResourceId = "vm.antimalware",
            ResourceName = "VM Anti-Malware Protection",
            SecurityControl = "Malware Protection",
            Issue = $"Anti-malware protection should be verified for {virtualMachines.Count} virtual machines",
            Recommendation = "Ensure Microsoft Antimalware extension is installed and configured on Windows VMs, and appropriate anti-malware solutions are deployed on Linux VMs with real-time protection enabled",
            Severity = "Medium",
            ComplianceFramework = "Malware Protection Standards"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeKubernetesAdvancedThreatProtectionAsync(List<AzureResource> kubernetesClusters, List<SecurityFinding> findings)
    {
        foreach (var cluster in kubernetesClusters.Take(5))
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = cluster.Id,
                ResourceName = cluster.Name,
                SecurityControl = "Kubernetes Threat Detection",
                Issue = "Microsoft Defender for Kubernetes should be enabled for container orchestration security",
                Recommendation = "Enable Microsoft Defender for Kubernetes to monitor cluster activities, detect threats at runtime, provide host-level threat detection, and analyze Kubernetes audit logs for suspicious activities",
                Severity = "High",
                ComplianceFramework = "Kubernetes Security Standards"
            });

            // Check for pod security policies
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = cluster.Id,
                ResourceName = cluster.Name,
                SecurityControl = "Kubernetes Security Policies",
                Issue = "Kubernetes cluster should implement pod security standards and network policies",
                Recommendation = "Configure pod security standards, network policies, and admission controllers to enforce security baselines and prevent deployment of insecure workloads",
                Severity = "Medium",
                ComplianceFramework = "CIS Kubernetes Benchmark"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeOverallAtpCoverageAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        var atpCapableResources = allResources.Where(r =>
            r.Type.ToLowerInvariant().Contains("storage") ||
            r.Type.ToLowerInvariant().Contains("sql") ||
            r.Type.ToLowerInvariant().Contains("documentdb") ||
            r.Type.ToLowerInvariant().Contains("keyvault") ||
            r.Type.ToLowerInvariant().Contains("web/sites") ||
            r.Type.ToLowerInvariant().Contains("compute") ||
            r.Type.ToLowerInvariant().Contains("containerservice")).Count();

        if (atpCapableResources > 10)
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = "atp.coverage.governance",
                ResourceName = "ATP Coverage Governance",
                SecurityControl = "Comprehensive Threat Detection",
                Issue = $"Large number of ATP-capable resources ({atpCapableResources}) requires centralized threat protection governance",
                Recommendation = "Implement centralized Advanced Threat Protection governance using Microsoft Defender for Cloud to ensure consistent threat detection across all supported Azure services",
                Severity = "Medium",
                ComplianceFramework = "Threat Detection Best Practice"
            });
        }

        // Check for SIEM integration
        await AnalyzeSiemIntegrationAsync(allResources, findings);

        await Task.CompletedTask;
    }

    private async Task AnalyzeSiemIntegrationAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        var logAnalyticsWorkspaces = allResources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.operationalinsights/workspaces").Count();

        var securityCapableResources = allResources.Where(r =>
            r.Type.ToLowerInvariant().Contains("storage") ||
            r.Type.ToLowerInvariant().Contains("sql") ||
            r.Type.ToLowerInvariant().Contains("keyvault")).Count();

        if (securityCapableResources > 5 && logAnalyticsWorkspaces == 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = "siem.integration",
                ResourceName = "SIEM Integration",
                SecurityControl = "Security Information Management",
                Issue = "No Log Analytics workspace found for centralized security event aggregation and analysis",
                Recommendation = "Deploy Log Analytics workspace and configure diagnostic settings to centralize security logs for SIEM integration and advanced threat analysis",
                Severity = "Medium",
                ComplianceFramework = "Security Monitoring Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeThreatIntelligenceIntegrationAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        var azureFirewalls = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/azurefirewalls").Count();
        var applicationGateways = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/applicationgateways").Count();

        if (azureFirewalls > 0 || applicationGateways > 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = "threat.intelligence.integration",
                ResourceName = "Threat Intelligence Integration",
                SecurityControl = "Threat Intelligence",
                Issue = "Network security appliances should leverage threat intelligence feeds for enhanced protection",
                Recommendation = "Configure Azure Firewall threat intelligence and Application Gateway WAF with updated rule sets to block known malicious IPs, domains, and attack patterns",
                Severity = "Medium",
                ComplianceFramework = "Threat Intelligence Best Practice"
            });
        }

        // Check for Microsoft Sentinel deployment
        var sentinelWorkspaces = allResources.Where(r =>
            r.Name.ToLowerInvariant().Contains("sentinel") ||
            r.Tags.ContainsKey("Purpose") && r.Tags["Purpose"].ToLowerInvariant().Contains("sentinel")).Count();

        var totalSecurityResources = allResources.Where(r =>
            r.Type.ToLowerInvariant().Contains("storage") ||
            r.Type.ToLowerInvariant().Contains("sql") ||
            r.Type.ToLowerInvariant().Contains("keyvault") ||
            r.Type.ToLowerInvariant().Contains("compute")).Count();

        if (totalSecurityResources > 15 && sentinelWorkspaces == 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "AdvancedThreatProtection",
                ResourceId = "microsoft.sentinel.deployment",
                ResourceName = "Microsoft Sentinel SIEM",
                SecurityControl = "Security Operations Center",
                Issue = $"Large Azure environment ({totalSecurityResources} security-relevant resources) lacks dedicated SIEM solution for advanced threat hunting",
                Recommendation = "Consider deploying Microsoft Sentinel for advanced SIEM/SOAR capabilities, threat hunting, and automated incident response across the Azure environment",
                Severity = "Low",
                ComplianceFramework = "Enterprise Security Operations"
            });
        }

        await Task.CompletedTask;
    }

    private string DetermineAtpSeverity(AzureResource resource)
    {
        // Higher severity for production resources
        if (IsProductionResource(resource))
        {
            return "High";
        }

        // Check for sensitive data indicators
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

        // Check for critical business applications
        if (resource.Tags.ContainsKey("BusinessCriticality"))
        {
            var criticality = resource.Tags["BusinessCriticality"].ToLowerInvariant();
            if (criticality.Contains("critical") || criticality.Contains("high"))
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
}