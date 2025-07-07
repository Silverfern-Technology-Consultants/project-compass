using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services;

public interface IIdentityAccessAssessmentAnalyzer
{
    Task<IdentityAccessResults> AnalyzeIdentityAccessAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);

    Task<IdentityAccessResults> AnalyzeIdentityAccessWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);
}

public class IdentityAccessAssessmentAnalyzer : IIdentityAccessAssessmentAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<IdentityAccessAssessmentAnalyzer> _logger;

    public IdentityAccessAssessmentAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        ILogger<IdentityAccessAssessmentAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeIdentityAccessAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Identity Access Management analysis for assessment type: {AssessmentType}", assessmentType);

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

        try
        {
            switch (assessmentType)
            {
                case AssessmentType.EnterpriseApplications:
                    await AnalyzeEnterpriseApplicationsAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.StaleUsersDevices:
                    await AnalyzeStaleUsersAndDevicesAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.ResourceIamRbac:
                    await AnalyzeResourceIamRbacAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.ConditionalAccess:
                    await AnalyzeConditionalAccessAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.IdentityFull:
                    await AnalyzeEnterpriseApplicationsAsync(subscriptionIds, results, findings, cancellationToken);
                    await AnalyzeStaleUsersAndDevicesAsync(subscriptionIds, results, findings, cancellationToken);
                    await AnalyzeResourceIamRbacAsync(subscriptionIds, results, findings, cancellationToken);
                    await AnalyzeConditionalAccessAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported IAM assessment type: {assessmentType}");
            }

            results.SecurityFindings = findings;
            results.Score = CalculateOverallIamScore(results);

            _logger.LogInformation("Identity Access Management analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Identity Access Management for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<IdentityAccessResults> AnalyzeIdentityAccessWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Identity Access Management analysis for client {ClientId}", clientId);

        // For now, fall back to standard analysis since we don't have OAuth-specific IAM queries yet
        // In the future, this could use OAuth to access Microsoft Graph for enhanced analysis
        return await AnalyzeIdentityAccessAsync(subscriptionIds, assessmentType, cancellationToken);
    }

    private async Task AnalyzeEnterpriseApplicationsAsync(
        string[] subscriptionIds,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Enterprise Applications and App Registrations...");

        try
        {
            // Note: Enterprise applications and app registrations require Microsoft Graph API access
            // Since we're using Azure Resource Graph, we have limited visibility into AAD resources
            // We'll analyze what we can from available Azure resources

            // Get all Azure resources that might relate to applications/identity
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for Key Vaults (which often store application secrets)
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).ToList();

            // Look for App Service resources (which might have managed identities)
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.web/sites")).ToList();

            // Look for Function Apps
            var functionApps = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.web/sites") &&
                                                      r.Kind?.ToLowerInvariant().Contains("functionapp") == true).ToList();

            results.TotalApplications = keyVaults.Count + appServices.Count + functionApps.Count;

            // Analyze Key Vault access policies (potential security issues)
            foreach (var keyVault in keyVaults)
            {
                await AnalyzeKeyVaultSecurityAsync(keyVault, findings);
            }

            // Analyze App Services for managed identity usage
            foreach (var appService in appServices)
            {
                await AnalyzeAppServiceIdentityAsync(appService, findings);
            }

            results.RiskyApplications = findings.Count(f => f.FindingType.Contains("Application"));

            // Add general recommendation about enterprise application analysis
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "EnterpriseApplicationAnalysisLimited",
                ResourceId = "microsoft.graph.applications",
                ResourceName = "Enterprise Applications",
                Severity = "Medium",
                Description = "Complete enterprise application analysis requires Microsoft Graph API permissions",
                Recommendation = "Configure Microsoft Graph permissions to analyze app registrations, service principals, and OAuth consent grants",
                BusinessImpact = "Cannot assess application-level security risks without Graph API access",
                AdditionalData = new Dictionary<string, string>
                {
                    ["RequiredPermissions"] = "Application.Read.All, Directory.Read.All, Policy.Read.All"
                }
            });

            _logger.LogInformation("Enterprise Applications analysis completed. Analyzed {ResourceCount} application-related resources",
                results.TotalApplications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze enterprise applications");

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "EnterpriseApplicationAnalysisError",
                ResourceId = "error.analysis",
                ResourceName = "Enterprise Application Analysis",
                Severity = "High",
                Description = "Failed to analyze enterprise applications due to an error",
                Recommendation = "Review Azure permissions and retry the analysis",
                BusinessImpact = "Cannot assess application security risks"
            });
        }
    }

    private async Task AnalyzeKeyVaultSecurityAsync(AzureResource keyVault, List<IdentitySecurityFinding> findings)
    {
        // Analyze Key Vault for potential security issues
        string keyVaultName = keyVault.Name;
        string keyVaultId = keyVault.Id;

        // Check if Key Vault has proper tagging
        if (!keyVault.HasTags || !keyVault.Tags.ContainsKey("Environment"))
        {
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "KeyVaultMissingTags",
                ResourceId = keyVaultId,
                ResourceName = keyVaultName,
                Severity = "Medium",
                Description = "Key Vault lacks proper tagging, making it difficult to identify purpose and ownership",
                Recommendation = "Add Environment, Owner, and Purpose tags to Key Vault",
                BusinessImpact = "Untagged key vaults complicate security auditing and access management"
            });
        }

        // Check for public network access (if this info is available in properties)
        if (!string.IsNullOrEmpty(keyVault.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(keyVault.Properties);
                if (properties.RootElement.TryGetProperty("publicNetworkAccess", out var networkAccess))
                {
                    if (networkAccess.GetString()?.ToLowerInvariant() == "enabled")
                    {
                        findings.Add(new IdentitySecurityFinding
                        {
                            FindingType = "KeyVaultPublicAccess",
                            ResourceId = keyVaultId,
                            ResourceName = keyVaultName,
                            Severity = "High",
                            Description = "Key Vault allows public network access, increasing attack surface",
                            Recommendation = "Configure private endpoints and disable public network access",
                            BusinessImpact = "Public Key Vaults are accessible from the internet, increasing security risks"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed, continue without this check
            }
        }

        await Task.CompletedTask; // Make method properly async
    }

    private async Task AnalyzeAppServiceIdentityAsync(AzureResource appService, List<IdentitySecurityFinding> findings)
    {
        string appServiceName = appService.Name;
        string appServiceId = appService.Id;

        // Check if App Service has managed identity configured (if available in properties)
        bool hasManagedIdentity = false;

        if (!string.IsNullOrEmpty(appService.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(appService.Properties);
                if (properties.RootElement.TryGetProperty("identity", out var identity))
                {
                    hasManagedIdentity = identity.ValueKind != JsonValueKind.Null;
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed, continue
            }
        }

        if (!hasManagedIdentity)
        {
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "AppServiceMissingManagedIdentity",
                ResourceId = appServiceId,
                ResourceName = appServiceName,
                Severity = "Medium",
                Description = "App Service does not appear to use managed identity for authentication",
                Recommendation = "Enable system-assigned or user-assigned managed identity to improve security",
                BusinessImpact = "Applications without managed identity may use stored credentials, increasing security risks"
            });
        }

        await Task.CompletedTask; // Make method properly async
    }

    private async Task AnalyzeStaleUsersAndDevicesAsync(
        string[] subscriptionIds,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Stale Users and Devices...");

        try
        {
            // Get all resources to analyze for identity-related issues
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for Intune/device management resources
            var deviceResources = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("microsoft.intune") ||
                r.Type.ToLowerInvariant().Contains("device") ||
                r.Type.ToLowerInvariant().Contains("microsoft.devicemanagement")).ToList();

            // Look for virtual machines (which should have proper identity management)
            var virtualMachines = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

            foreach (var vm in virtualMachines)
            {
                await AnalyzeVirtualMachineIdentityAsync(vm, findings);
            }

            results.UnmanagedDevices = deviceResources.Count;
            results.InactiveUsers = findings.Count(f => f.FindingType.Contains("User") || f.FindingType.Contains("Identity"));

            // Add general recommendation about user/device analysis
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "UserDeviceAnalysisLimited",
                ResourceId = "microsoft.graph.users",
                ResourceName = "User and Device Analysis",
                Severity = "Medium",
                Description = "Comprehensive user and device analysis requires Microsoft Graph and Intune permissions",
                Recommendation = "Configure Microsoft Graph permissions to analyze user accounts, device compliance, and lifecycle management",
                BusinessImpact = "Cannot fully assess identity lifecycle and device security risks",
                AdditionalData = new Dictionary<string, string>
                {
                    ["RequiredPermissions"] = "User.Read.All, Device.Read.All, DeviceManagementManagedDevices.Read.All"
                }
            });

            _logger.LogInformation("Stale Users and Devices analysis completed. Device resources: {DeviceCount}, Identity issues: {IdentityIssues}",
                results.UnmanagedDevices, results.InactiveUsers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze stale users and devices");

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "UserDeviceAnalysisError",
                ResourceId = "error.userdevice",
                ResourceName = "User Device Analysis",
                Severity = "Medium",
                Description = "Failed to analyze user and device security",
                Recommendation = "Review permissions and retry analysis",
                BusinessImpact = "Cannot assess identity lifecycle security risks"
            });
        }
    }

    private async Task AnalyzeVirtualMachineIdentityAsync(AzureResource vm, List<IdentitySecurityFinding> findings)
    {
        string vmName = vm.Name;
        string vmId = vm.Id;

        // Check if VM has managed identity
        bool hasManagedIdentity = false;

        if (!string.IsNullOrEmpty(vm.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(vm.Properties);
                if (properties.RootElement.TryGetProperty("identity", out var identity))
                {
                    hasManagedIdentity = identity.ValueKind != JsonValueKind.Null;
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        if (!hasManagedIdentity)
        {
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "VirtualMachineMissingManagedIdentity",
                ResourceId = vmId,
                ResourceName = vmName,
                Severity = "Medium",
                Description = "Virtual machine does not appear to use managed identity",
                Recommendation = "Enable system-assigned managed identity to eliminate credential management",
                BusinessImpact = "VMs without managed identity may require stored credentials, increasing security risks"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeResourceIamRbacAsync(
        string[] subscriptionIds,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Resource IAM/RBAC Assignments...");

        try
        {
            // Use existing resource graph service to get all resources
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // For IAM/RBAC analysis, we need to look at what we can determine from the resources themselves
            // Full RBAC analysis would require Azure Resource Manager APIs with authorization read permissions

            // Analyze resource groups for potential organization issues
            var resourceGroups = allResources.GroupBy(r => r.ResourceGroup ?? "Unknown").ToList();

            foreach (var rgGroup in resourceGroups)
            {
                var resourceGroupName = rgGroup.Key;
                var resourcesInRG = rgGroup.ToList();

                // Check for resource groups with too many different resource types (potential over-permissioning)
                var uniqueResourceTypes = resourcesInRG.Select(r => r.Type).Distinct().Count();

                if (uniqueResourceTypes > 10)
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "ResourceGroupComplexity",
                        ResourceId = resourceGroupName,
                        ResourceName = resourceGroupName,
                        Severity = "Medium",
                        Description = $"Resource group '{resourceGroupName}' contains {uniqueResourceTypes} different resource types, which may indicate broad permissions",
                        Recommendation = "Review resource group organization and consider separating resources by access requirements",
                        BusinessImpact = "Complex resource groups may lead to over-permissioned access"
                    });
                }

                // Check for resource groups without proper tagging (impacts access management)
                var untaggedResources = resourcesInRG.Where(r => !r.HasTags).Count();
                if (untaggedResources > resourcesInRG.Count * 0.5)
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "ResourceGroupMissingTags",
                        ResourceId = resourceGroupName,
                        ResourceName = resourceGroupName,
                        Severity = "Low",
                        Description = $"Resource group '{resourceGroupName}' has {untaggedResources} untagged resources out of {resourcesInRG.Count}",
                        Recommendation = "Implement consistent tagging to support proper access management and governance",
                        BusinessImpact = "Untagged resources complicate access management and auditing"
                    });
                }
            }

            // Check for high-value resources that should have strict access controls
            var highValueResources = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("keyvault") ||
                r.Type.ToLowerInvariant().Contains("sql") ||
                r.Type.ToLowerInvariant().Contains("storage")).ToList();

            foreach (var hvResource in highValueResources)
            {
                // High-value resources should have proper tagging for access management
                if (!hvResource.HasTags || !hvResource.Tags.ContainsKey("Environment"))
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "HighValueResourceMissingTags",
                        ResourceId = hvResource.Id,
                        ResourceName = hvResource.Name,
                        Severity = "Medium",
                        Description = $"High-value resource '{hvResource.Name}' lacks proper tagging for access management",
                        Recommendation = "Add Environment, Owner, and Classification tags to support proper access controls",
                        BusinessImpact = "Untagged high-value resources may have inappropriate access permissions"
                    });
                }
            }

            results.OverprivilegedAssignments = findings.Count(f => f.FindingType.Contains("Overprivileged") || f.FindingType.Contains("Complexity"));

            // Add general recommendation about RBAC analysis
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "RbacAnalysisLimited",
                ResourceId = "azure.rbac",
                ResourceName = "RBAC Analysis",
                Severity = "Medium",
                Description = "Comprehensive RBAC analysis requires Azure Resource Manager permissions for authorization data",
                Recommendation = "Grant authorization read permissions to analyze role assignments and access patterns",
                BusinessImpact = "Cannot fully assess role assignment security and access patterns",
                AdditionalData = new Dictionary<string, string>
                {
                    ["RequiredPermissions"] = "Microsoft.Authorization/roleAssignments/read, Microsoft.Authorization/roleDefinitions/read"
                }
            });

            _logger.LogInformation("Resource IAM/RBAC analysis completed. Analyzed {ResourceGroupCount} resource groups, {HighValueCount} high-value resources",
                resourceGroups.Count, highValueResources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Resource IAM/RBAC");

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "RbacAnalysisError",
                ResourceId = "error.rbac",
                ResourceName = "RBAC Analysis",
                Severity = "High",
                Description = "Failed to analyze RBAC and access management",
                Recommendation = "Review permissions and retry analysis",
                BusinessImpact = "Cannot assess access management security risks"
            });
        }
    }

    private async Task AnalyzeConditionalAccessAsync(
        string[] subscriptionIds,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Conditional Access Policies...");

        try
        {
            // Conditional Access policies are primarily Microsoft Graph/Entra ID resources
            // We can analyze what's available through Azure Resource Graph

            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for AAD-related resources
            var aadResources = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("microsoft.aad") ||
                r.Type.ToLowerInvariant().Contains("activedirectory") ||
                r.Type.ToLowerInvariant().Contains("conditional")).ToList();

            results.ConditionalAccessCoverage = new ConditionalAccessCoverage
            {
                TotalPolicies = aadResources.Count,
                EnabledPolicies = 0, // Would need Graph API to determine
                CoveragePercentage = 0
            };

            // Add finding about limited analysis capability
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "ConditionalAccessAnalysisLimited",
                ResourceId = "conditional.access",
                ResourceName = "Conditional Access Analysis",
                Severity = "Medium",
                Description = "Conditional Access analysis requires Microsoft Graph API permissions for comprehensive review",
                Recommendation = "Grant Microsoft Graph permissions to analyze Conditional Access policies, compliance, and coverage",
                BusinessImpact = "Cannot assess conditional access security posture without proper Graph API permissions",
                AdditionalData = new Dictionary<string, string>
                {
                    ["RequiredPermissions"] = "Policy.Read.All, Application.Read.All, Directory.Read.All"
                }
            });

            // General conditional access security recommendations
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "ConditionalAccessBestPractices",
                ResourceId = "ca.bestpractices",
                ResourceName = "Conditional Access Best Practices",
                Severity = "Medium",
                Description = "Ensure Conditional Access policies are configured for all critical applications and privileged users",
                Recommendation = "Implement MFA requirements, device compliance checks, location-based controls, and risk-based policies",
                BusinessImpact = "Lack of comprehensive Conditional Access policies increases risk of unauthorized access and data breaches"
            });

            // Check for high-value resources that should be protected by CA
            var highValueResources = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("keyvault") ||
                r.Type.ToLowerInvariant().Contains("sql") ||
                r.Type.ToLowerInvariant().Contains("storage")).ToList();

            if (highValueResources.Any())
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "HighValueResourceAccessReview",
                    ResourceId = "high.value.resources",
                    ResourceName = "High-Value Resource Access",
                    Severity = "High",
                    Description = $"Found {highValueResources.Count} high-value resources that should have strict Conditional Access controls",
                    Recommendation = "Ensure all access to key vaults, databases, and storage accounts is protected by Conditional Access policies",
                    BusinessImpact = "Unprotected high-value resources are vulnerable to unauthorized access"
                });
            }

            _logger.LogInformation("Conditional Access analysis completed with limited scope. Found {AADResourceCount} AAD-related resources",
                aadResources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Conditional Access policies");

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "ConditionalAccessError",
                ResourceId = "ca.error",
                ResourceName = "Conditional Access Analysis Error",
                Severity = "High",
                Description = "Failed to analyze Conditional Access configuration due to an error",
                Recommendation = "Review Conditional Access policies manually or grant appropriate Graph API permissions",
                BusinessImpact = "Cannot verify conditional access security controls are in place"
            });
        }
    }

    private decimal CalculateOverallIamScore(IdentityAccessResults results)
    {
        var scoringFactors = new List<decimal>();

        // Application security score (25% weight)
        var appSecurityScore = 100m;
        if (results.TotalApplications > 0)
        {
            var appRiskPercentage = (decimal)results.RiskyApplications / results.TotalApplications * 100;
            appSecurityScore = Math.Max(0, 100 - (appRiskPercentage * 2)); // Double penalty for risky apps
        }
        scoringFactors.Add(appSecurityScore * 0.25m);

        // User/device management score (25% weight)
        var userDeviceScore = 100m;
        var userDeviceIssues = results.InactiveUsers + results.UnmanagedDevices;
        if (userDeviceIssues > 0)
        {
            // Deduct points based on number of issues
            userDeviceScore = Math.Max(0, 100 - (userDeviceIssues * 5));
        }
        scoringFactors.Add(userDeviceScore * 0.25m);

        // RBAC score (30% weight)
        var rbacScore = 100m;
        if (results.OverprivilegedAssignments > 0)
        {
            // Heavily penalize overprivileged assignments
            rbacScore = Math.Max(0, 100 - (results.OverprivilegedAssignments * 10));
        }
        scoringFactors.Add(rbacScore * 0.30m);

        // Conditional Access score (20% weight)
        var caScore = results.ConditionalAccessCoverage.CoveragePercentage;
        // If no CA analysis was possible, give partial credit to avoid penalizing too heavily
        if (caScore == 0 && results.ConditionalAccessCoverage.TotalPolicies == 0)
        {
            caScore = 50m; // Neutral score when analysis isn't possible
        }
        scoringFactors.Add(caScore * 0.20m);

        // Critical finding penalty
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var penalty = (criticalFindings * 15) + (highFindings * 8);

        var finalScore = Math.Max(0, scoringFactors.Sum() - penalty);

        _logger.LogInformation("IAM Score calculated: {Score}% (App: {AppScore}%, User/Device: {UserScore}%, RBAC: {RbacScore}%, CA: {CaScore}%, Penalty: {Penalty})",
            finalScore, appSecurityScore, userDeviceScore, rbacScore, caScore, penalty);

        return Math.Round(finalScore, 2);
    }

    // Helper methods for parsing (simplified since we're not using Graph API data)
    private List<string> ParseRequiredResourceAccess(string resourceAccessJson)
    {
        var permissions = new List<string>();

        // This is a simplified approach since we don't have actual Graph API data
        // In a real implementation with Graph API, this would parse actual permission objects

        if (string.IsNullOrEmpty(resourceAccessJson))
            return permissions;

        try
        {
            // Look for common high-risk permission patterns in JSON strings
            var commonHighRiskPatterns = new[]
            {
                "Directory.ReadWrite.All",
                "Application.ReadWrite.All",
                "User.ReadWrite.All",
                "RoleManagement.ReadWrite.Directory",
                "Mail.ReadWrite",
                "Files.ReadWrite.All",
                "Sites.FullControl.All"
            };

            foreach (var pattern in commonHighRiskPatterns)
            {
                if (resourceAccessJson.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    permissions.Add(pattern);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse required resource access");
        }

        return permissions;
    }

    private bool IsHighRiskPermission(string permission)
    {
        var highRiskPermissions = new[]
        {
            "Directory.ReadWrite.All",
            "Application.ReadWrite.All",
            "User.ReadWrite.All",
            "RoleManagement.ReadWrite.Directory",
            "Mail.ReadWrite",
            "Files.ReadWrite.All",
            "Sites.FullControl.All",
            "Group.ReadWrite.All",
            "Member.Read.Hidden"
        };

        return highRiskPermissions.Any(hrp => permission.Contains(hrp, StringComparison.OrdinalIgnoreCase));
    }

    private List<string> CheckCredentialExpiration(object passwordCredentials, object keyCredentials)
    {
        var expiredCredentials = new List<string>();

        // Since we don't have actual Graph API data, provide general recommendations
        expiredCredentials.Add("Review application credential expiration dates");
        expiredCredentials.Add("Implement automated credential rotation where possible");

        return expiredCredentials;
    }
}