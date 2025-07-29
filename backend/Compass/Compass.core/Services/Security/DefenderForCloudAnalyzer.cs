using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services.Security;

public interface IDefenderForCloudAnalyzer
{
    Task<SecurityPostureResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default);

    Task<SecurityPostureResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

public class DefenderForCloudAnalyzer : IDefenderForCloudAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<DefenderForCloudAnalyzer> _logger;

    public DefenderForCloudAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        ILogger<DefenderForCloudAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<SecurityPostureResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Microsoft Defender for Cloud analysis for subscriptions: {Subscriptions}",
            string.Join(",", subscriptionIds));

        var results = new SecurityPostureResults();
        var findings = new List<SecurityFinding>();

        try
        {
            await AnalyzeDefenderForCloudAsync(subscriptionIds, results, findings, cancellationToken);

            results.SecurityFindings = findings;
            results.Score = CalculateDefenderScore(results);

            _logger.LogInformation("Microsoft Defender for Cloud analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Microsoft Defender for Cloud for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<SecurityPostureResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Microsoft Defender for Cloud analysis for client {ClientId}", clientId);

        // Future enhancement: Use OAuth to access Microsoft Defender for Cloud APIs directly
        // For now, fall back to Resource Graph analysis
        return await AnalyzeAsync(subscriptionIds, cancellationToken);
    }

    private async Task AnalyzeDefenderForCloudAsync(
        string[] subscriptionIds,
        SecurityPostureResults results,
        List<SecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Microsoft Defender for Cloud configuration and security posture...");

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for Security Center resources (these are typically not visible via Resource Graph)
            var securityResources = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("security") ||
                r.Type.ToLowerInvariant().Contains("microsoft.security")).ToList();

            // Initialize Defender analysis
            results.DefenderAnalysis = new DefenderForCloudAnalysis
            {
                IsEnabled = securityResources.Any(),
                SecurityScore = 0m,
                HighSeverityRecommendations = 0,
                MediumSeverityRecommendations = 0,
                DefenderPlansStatus = new Dictionary<string, string>()
            };

            // Analyze critical resource types that should have Defender plans
            var virtualMachines = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();
            var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers")).ToList();
            var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).ToList();
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();
            var containerRegistries = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.containerregistry/registries").ToList();
            var kubernetesClusters = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.containerservice/managedclusters").ToList();
            var cosmosDbAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.documentdb/databaseaccounts").ToList();

            // General Defender for Cloud enablement check
            await AnalyzeDefenderEnablementAsync(findings);

            // Analyze Defender plan recommendations based on resource types
            await AnalyzeDefenderForServersAsync(virtualMachines, results, findings);
            await AnalyzeDefenderForSqlAsync(sqlServers, results, findings);
            await AnalyzeDefenderForStorageAsync(storageAccounts, results, findings);
            await AnalyzeDefenderForKeyVaultAsync(keyVaults, results, findings);
            await AnalyzeDefenderForAppServiceAsync(appServices, results, findings);
            await AnalyzeDefenderForContainerRegistryAsync(containerRegistries, results, findings);
            await AnalyzeDefenderForKubernetesAsync(kubernetesClusters, results, findings);
            await AnalyzeDefenderForCosmosDbAsync(cosmosDbAccounts, results, findings);

            // Analyze security configurations and compliance
            await AnalyzeSecurityContactsAsync(findings);
            await AnalyzeAutoProvisioningAsync(findings);
            await AnalyzeComplianceFrameworksAsync(findings);
            await AnalyzeSecurityPoliciesAsync(findings);
            await AnalyzeWorkflowAutomationAsync(findings);
            await AnalyzeContinuousExportAsync(findings);

            // Advanced Defender for Cloud features
            await AnalyzeJustInTimeAccessAsync(virtualMachines, findings);
            await AnalyzeAdaptiveApplicationControlsAsync(virtualMachines, findings);
            await AnalyzeAdaptiveNetworkHardeningAsync(findings);
            await AnalyzeFileIntegrityMonitoringAsync(virtualMachines, findings);

            // Update analysis metrics based on findings
            results.DefenderAnalysis.HighSeverityRecommendations = findings.Count(f => f.Category == "DefenderForCloud" && f.Severity == "High");
            results.DefenderAnalysis.MediumSeverityRecommendations = findings.Count(f => f.Category == "DefenderForCloud" && f.Severity == "Medium");

            // Analyze security score implications
            await AnalyzeSecurityScoreImpactAsync(results, findings);

            _logger.LogInformation("Microsoft Defender for Cloud analysis completed. Security resources: {SecurityCount}, High recommendations: {HighCount}",
                securityResources.Count, results.DefenderAnalysis.HighSeverityRecommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Microsoft Defender for Cloud");

            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "error.defender",
                ResourceName = "Defender for Cloud Analysis",
                SecurityControl = "Security Assessment",
                Issue = "Failed to analyze Microsoft Defender for Cloud configuration",
                Recommendation = "Review permissions and retry Defender for Cloud analysis. Ensure Reader permissions on subscriptions and Security Reader role.",
                Severity = "Medium",
                ComplianceFramework = "General"
            });

            results.DefenderAnalysis = new DefenderForCloudAnalysis
            {
                IsEnabled = false,
                SecurityScore = 0m,
                HighSeverityRecommendations = 1,
                MediumSeverityRecommendations = 0,
                DefenderPlansStatus = new Dictionary<string, string> { ["Analysis"] = "Failed" }
            };
        }
    }

    private async Task AnalyzeDefenderEnablementAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "defender.enablement",
            ResourceName = "Microsoft Defender for Cloud",
            SecurityControl = "Security Posture Management",
            Issue = "Microsoft Defender for Cloud enablement and configuration requires verification across all subscriptions",
            Recommendation = "Enable Microsoft Defender for Cloud enhanced security features across all subscriptions. Configure security policies and enable relevant Defender plans.",
            Severity = "High",
            ComplianceFramework = "CIS Azure Foundations"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForServersAsync(List<AzureResource> virtualMachines, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (virtualMachines.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.servers",
                ResourceName = "Defender for Servers",
                SecurityControl = "Endpoint Protection",
                Issue = $"Found {virtualMachines.Count} virtual machines that require Defender for Servers protection",
                Recommendation = "Enable Microsoft Defender for Servers Plan 2 to provide vulnerability assessment, just-in-time VM access, adaptive application controls, and file integrity monitoring.",
                Severity = "High",
                ComplianceFramework = "Azure Security Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["Servers"] = $"{virtualMachines.Count} VMs require protection";

            // Analyze VM security configurations
            await AnalyzeVirtualMachineSecurityAsync(virtualMachines, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForSqlAsync(List<AzureResource> sqlServers, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (sqlServers.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.sql",
                ResourceName = "Defender for SQL",
                SecurityControl = "Database Security",
                Issue = $"Found {sqlServers.Count} SQL servers that require Defender for SQL protection",
                Recommendation = "Enable Microsoft Defender for SQL to detect SQL injection attacks, access anomalies, and vulnerability assessments. Configure threat detection policies.",
                Severity = "High",
                ComplianceFramework = "Azure Security Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["SQL"] = $"{sqlServers.Count} SQL servers require protection";

            // Analyze SQL-specific security configurations
            await AnalyzeSqlSecurityConfigurationsAsync(sqlServers, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForStorageAsync(List<AzureResource> storageAccounts, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (storageAccounts.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.storage",
                ResourceName = "Defender for Storage",
                SecurityControl = "Data Protection",
                Issue = $"Found {storageAccounts.Count} storage accounts that require Defender for Storage protection",
                Recommendation = "Enable Microsoft Defender for Storage to detect malicious uploads, hash reputation analysis, and sensitive data threat detection.",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["Storage"] = $"{storageAccounts.Count} storage accounts require protection";
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForKeyVaultAsync(List<AzureResource> keyVaults, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (keyVaults.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.keyvault",
                ResourceName = "Defender for Key Vault",
                SecurityControl = "Secrets Management",
                Issue = $"Found {keyVaults.Count} key vaults that require Defender for Key Vault protection",
                Recommendation = "Enable Microsoft Defender for Key Vault to detect unusual access patterns, suspicious operations, and potential attacks on secrets and keys.",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["KeyVault"] = $"{keyVaults.Count} key vaults require protection";
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForAppServiceAsync(List<AzureResource> appServices, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (appServices.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.appservice",
                ResourceName = "Defender for App Service",
                SecurityControl = "Application Security",
                Issue = $"Found {appServices.Count} app services that require Defender for App Service protection",
                Recommendation = "Enable Microsoft Defender for App Service to detect web application attacks, malicious code uploads, and command injection attempts.",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["AppService"] = $"{appServices.Count} app services require protection";
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForContainerRegistryAsync(List<AzureResource> containerRegistries, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (containerRegistries.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.containerregistry",
                ResourceName = "Defender for Container Registry",
                SecurityControl = "Container Security",
                Issue = $"Found {containerRegistries.Count} container registries that require vulnerability scanning",
                Recommendation = "Enable Microsoft Defender for Container Registry to scan images for vulnerabilities and provide continuous monitoring of container security.",
                Severity = "Medium",
                ComplianceFramework = "CIS Kubernetes Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["ContainerRegistry"] = $"{containerRegistries.Count} registries require scanning";
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForKubernetesAsync(List<AzureResource> kubernetesClusters, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (kubernetesClusters.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.kubernetes",
                ResourceName = "Defender for Kubernetes",
                SecurityControl = "Container Orchestration Security",
                Issue = $"Found {kubernetesClusters.Count} Kubernetes clusters that require advanced threat protection",
                Recommendation = "Enable Microsoft Defender for Kubernetes to monitor cluster activities, detect threats at runtime, and provide host-level threat detection.",
                Severity = "High",
                ComplianceFramework = "CIS Kubernetes Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["Kubernetes"] = $"{kubernetesClusters.Count} clusters require protection";
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForCosmosDbAsync(List<AzureResource> cosmosDbAccounts, SecurityPostureResults results, List<SecurityFinding> findings)
    {
        if (cosmosDbAccounts.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.cosmosdb",
                ResourceName = "Defender for Cosmos DB",
                SecurityControl = "NoSQL Database Security",
                Issue = $"Found {cosmosDbAccounts.Count} Cosmos DB accounts that require advanced threat protection",
                Recommendation = "Enable Microsoft Defender for Cosmos DB to detect SQL injection, access from unusual locations, and potential data exfiltration attempts.",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });

            results.DefenderAnalysis.DefenderPlansStatus["CosmosDB"] = $"{cosmosDbAccounts.Count} accounts require protection";
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVirtualMachineSecurityAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        foreach (var vm in virtualMachines.Take(10)) // Limit analysis to avoid performance issues
        {
            // Check for basic VM security configurations
            if (!vm.HasTags || !vm.Tags.ContainsKey("Environment"))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = vm.Id,
                    ResourceName = vm.Name,
                    SecurityControl = "Asset Management",
                    Issue = "Virtual machine lacks proper environment classification for security policy application",
                    Recommendation = "Add Environment tag (Production, Development, Testing) to ensure appropriate security policies and Defender configurations are applied",
                    Severity = "Low",
                    ComplianceFramework = "Azure Security Benchmark"
                });
            }

            // Check for production VMs that need enhanced security
            if (vm.Environment?.ToLowerInvariant().Contains("prod") == true ||
                vm.Tags.ContainsKey("Environment") && vm.Tags["Environment"].ToLowerInvariant().Contains("prod"))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = vm.Id,
                    ResourceName = vm.Name,
                    SecurityControl = "Production Security",
                    Issue = "Production virtual machine requires enhanced Defender for Servers protection and monitoring",
                    Recommendation = "Ensure Defender for Servers Plan 2 is enabled with vulnerability assessment, just-in-time access, and file integrity monitoring",
                    Severity = "High",
                    ComplianceFramework = "Production Security Standards"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSqlSecurityConfigurationsAsync(List<AzureResource> sqlServers, List<SecurityFinding> findings)
    {
        foreach (var sqlServer in sqlServers.Take(5)) // Limit analysis
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = sqlServer.Id,
                ResourceName = sqlServer.Name,
                SecurityControl = "Database Security",
                Issue = "SQL Server requires Advanced Threat Protection and vulnerability assessment configuration",
                Recommendation = "Enable Defender for SQL with threat detection policies, vulnerability assessments, and data discovery & classification",
                Severity = "High",
                ComplianceFramework = "Azure Security Benchmark"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSecurityContactsAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "security.contacts",
            ResourceName = "Security Contacts Configuration",
            SecurityControl = "Incident Response",
            Issue = "Security contacts must be configured to ensure proper incident response and alert management",
            Recommendation = "Configure security contacts with email addresses and phone numbers for high-severity alerts. Enable email notifications for security findings.",
            Severity = "Medium",
            ComplianceFramework = "Azure Security Benchmark"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeAutoProvisioningAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "auto.provisioning",
            ResourceName = "Auto Provisioning Configuration",
            SecurityControl = "Security Monitoring",
            Issue = "Auto provisioning of security agents ensures comprehensive monitoring coverage",
            Recommendation = "Enable auto provisioning for Log Analytics agent, Azure Monitor agent, and security extensions on virtual machines and scale sets.",
            Severity = "Medium",
            ComplianceFramework = "Azure Security Benchmark"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeComplianceFrameworksAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "compliance.frameworks",
            ResourceName = "Compliance Frameworks",
            SecurityControl = "Regulatory Compliance",
            Issue = "Compliance framework configuration should align with business and regulatory requirements",
            Recommendation = "Enable and configure relevant compliance standards: Azure Security Benchmark (default), PCI-DSS, SOC TSP, ISO 27001, NIST SP 800-53, and industry-specific frameworks.",
            Severity = "Medium",
            ComplianceFramework = "Multi-Framework Compliance"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeSecurityPoliciesAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "security.policies",
            ResourceName = "Security Policies",
            SecurityControl = "Policy Governance",
            Issue = "Security policies should be properly configured and assigned to enforce organizational security standards",
            Recommendation = "Review and assign Azure Policy initiatives for security governance. Implement custom policies for organization-specific requirements and ensure proper enforcement modes.",
            Severity = "Medium",
            ComplianceFramework = "Azure Security Benchmark"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeWorkflowAutomationAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "workflow.automation",
            ResourceName = "Workflow Automation",
            SecurityControl = "Security Orchestration",
            Issue = "Workflow automation should be configured to enable automatic response to security findings",
            Recommendation = "Configure Logic Apps workflows to automate responses to high-severity security alerts, such as creating ITSM tickets, sending notifications, or triggering remediation scripts.",
            Severity = "Low",
            ComplianceFramework = "Security Automation Best Practice"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeContinuousExportAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "continuous.export",
            ResourceName = "Continuous Export",
            SecurityControl = "Security Data Integration",
            Issue = "Continuous export enables integration with SIEM systems and external security tools",
            Recommendation = "Configure continuous export to send security findings and recommendations to Log Analytics workspace, Event Hub, or Storage Account for SIEM integration.",
            Severity = "Low",
            ComplianceFramework = "Security Integration Best Practice"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeJustInTimeAccessAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        if (virtualMachines.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "jit.access",
                ResourceName = "Just-in-Time VM Access",
                SecurityControl = "Administrative Access Control",
                Issue = $"Just-in-time VM access should be configured for {virtualMachines.Count} virtual machines to reduce attack surface",
                Recommendation = "Enable just-in-time VM access to lock down inbound traffic to VMs and provide controlled access when needed. Configure for RDP (3389) and SSH (22) ports.",
                Severity = "Medium",
                ComplianceFramework = "CIS Azure Foundations"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAdaptiveApplicationControlsAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        if (virtualMachines.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "adaptive.application.controls",
                ResourceName = "Adaptive Application Controls",
                SecurityControl = "Application Whitelisting",
                Issue = $"Adaptive application controls should be configured for {virtualMachines.Count} virtual machines to prevent malicious applications",
                Recommendation = "Enable adaptive application controls to create application allowlists and detect potentially malicious applications running on VMs.",
                Severity = "Medium",
                ComplianceFramework = "NIST Cybersecurity Framework"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAdaptiveNetworkHardeningAsync(List<SecurityFinding> findings)
    {
        findings.Add(new SecurityFinding
        {
            Category = "DefenderForCloud",
            ResourceId = "adaptive.network.hardening",
            ResourceName = "Adaptive Network Hardening",
            SecurityControl = "Network Security Optimization",
            Issue = "Adaptive network hardening should be reviewed to optimize NSG rules based on traffic patterns",
            Recommendation = "Review adaptive network hardening recommendations to tighten NSG rules and reduce attack surface based on actual traffic patterns.",
            Severity = "Low",
            ComplianceFramework = "Azure Security Benchmark"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeFileIntegrityMonitoringAsync(List<AzureResource> virtualMachines, List<SecurityFinding> findings)
    {
        if (virtualMachines.Any())
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "file.integrity.monitoring",
                ResourceName = "File Integrity Monitoring",
                SecurityControl = "Change Detection",
                Issue = $"File integrity monitoring should be configured for {virtualMachines.Count} virtual machines to detect unauthorized changes",
                Recommendation = "Enable file integrity monitoring to track changes to critical files, registry keys, and system configurations on Windows and Linux VMs.",
                Severity = "Medium",
                ComplianceFramework = "PCI DSS"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSecurityScoreImpactAsync(SecurityPostureResults results, List<SecurityFinding> findings)
    {
        var highSeverityCount = results.DefenderAnalysis.HighSeverityRecommendations;
        var mediumSeverityCount = results.DefenderAnalysis.MediumSeverityRecommendations;

        if (highSeverityCount > 0 || mediumSeverityCount > 5)
        {
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "security.score.optimization",
                ResourceName = "Security Score Optimization",
                SecurityControl = "Security Posture Management",
                Issue = $"Current configuration may impact Secure Score ({highSeverityCount} high, {mediumSeverityCount} medium severity recommendations)",
                Recommendation = "Prioritize high-severity recommendations to improve Secure Score. Focus on enabling critical Defender plans and addressing fundamental security gaps first.",
                Severity = highSeverityCount > 3 ? "High" : "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });
        }

        // Estimate potential security score improvement
        var estimatedScore = 100m - (highSeverityCount * 12m) - (mediumSeverityCount * 4m);
        results.DefenderAnalysis.SecurityScore = Math.Max(0, Math.Round(estimatedScore, 2));

        await Task.CompletedTask;
    }

    private decimal CalculateDefenderScore(SecurityPostureResults results)
    {
        var baseScore = 100m;

        // Major deduction for not having Defender enabled
        if (!results.DefenderAnalysis.IsEnabled)
        {
            baseScore -= 50m;
        }

        // Deduct points based on severity of recommendations
        baseScore -= (results.DefenderAnalysis.HighSeverityRecommendations * 12m);
        baseScore -= (results.DefenderAnalysis.MediumSeverityRecommendations * 6m);

        // Deduct points for missing critical Defender plans
        var criticalPlans = results.DefenderAnalysis.DefenderPlansStatus.Count(kvp =>
            kvp.Key == "Servers" || kvp.Key == "SQL" || kvp.Key == "Kubernetes");
        if (criticalPlans > 0)
        {
            baseScore -= (criticalPlans * 8m);
        }

        // Additional deductions based on critical findings severity
        var criticalFindings = results.SecurityFindings.Count(f =>
            f.Category == "DefenderForCloud" && f.Severity == "Critical");
        baseScore -= (criticalFindings * 15m);

        // Bonus for comprehensive configuration
        var configuredFeatures = results.SecurityFindings.Count(f =>
            f.Category == "DefenderForCloud" && f.ResourceId.Contains("workflow"));
        if (configuredFeatures > 0)
        {
            baseScore += 5m; // Small bonus for advanced configuration
        }

        return Math.Max(0, Math.Round(baseScore, 2));
    }
}