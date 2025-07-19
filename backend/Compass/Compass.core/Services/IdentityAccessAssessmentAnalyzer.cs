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
    private readonly IMicrosoftGraphService _graphService;
    private readonly ILogger<IdentityAccessAssessmentAnalyzer> _logger;

    public IdentityAccessAssessmentAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        IMicrosoftGraphService graphService,
        ILogger<IdentityAccessAssessmentAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeIdentityAccessAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting PURE Identity Access Management analysis for assessment type: {AssessmentType}", assessmentType);

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

            _logger.LogInformation("PURE Identity Access Management analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
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
        _logger.LogInformation("Starting OAuth-enabled PURE Identity Access Management analysis for client {ClientId}", clientId);

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

        try
        {
            // First check if Graph credentials are available
            var hasGraphAccess = await _graphService.TestGraphConnectionAsync(clientId, organizationId);

            if (!hasGraphAccess)
            {
                _logger.LogWarning("Microsoft Graph access not available for client {ClientId}, falling back to limited analysis", clientId);
                return await AnalyzeIdentityAccessAsync(subscriptionIds, assessmentType, cancellationToken);
            }

            _logger.LogInformation("Microsoft Graph access confirmed for client {ClientId}, performing enhanced IAM analysis", clientId);

            // Perform Graph-enhanced analysis
            switch (assessmentType)
            {
                case AssessmentType.EnterpriseApplications:
                    await AnalyzeEnterpriseApplicationsWithGraphAsync(clientId, organizationId, results, findings, cancellationToken);
                    break;

                case AssessmentType.StaleUsersDevices:
                    await AnalyzeStaleUsersAndDevicesWithGraphAsync(clientId, organizationId, results, findings, cancellationToken);
                    break;

                case AssessmentType.ResourceIamRbac:
                    await AnalyzeResourceIamRbacWithGraphAsync(subscriptionIds, clientId, organizationId, results, findings, cancellationToken);
                    break;

                case AssessmentType.ConditionalAccess:
                    await AnalyzeConditionalAccessWithGraphAsync(clientId, organizationId, results, findings, cancellationToken);
                    break;

                case AssessmentType.IdentityFull:
                    await AnalyzeEnterpriseApplicationsWithGraphAsync(clientId, organizationId, results, findings, cancellationToken);
                    await AnalyzeStaleUsersAndDevicesWithGraphAsync(clientId, organizationId, results, findings, cancellationToken);
                    await AnalyzeResourceIamRbacWithGraphAsync(subscriptionIds, clientId, organizationId, results, findings, cancellationToken);
                    await AnalyzeConditionalAccessWithGraphAsync(clientId, organizationId, results, findings, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported IAM assessment type: {assessmentType}");
            }

            results.SecurityFindings = findings;
            results.Score = CalculateOverallIamScore(results);

            _logger.LogInformation("OAuth-enabled PURE Identity Access Management analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Identity Access Management with OAuth for client {ClientId}", clientId);

            // Fall back to standard analysis
            _logger.LogInformation("Falling back to standard IAM analysis for client {ClientId}", clientId);
            return await AnalyzeIdentityAccessAsync(subscriptionIds, assessmentType, cancellationToken);
        }
    }

    #region Graph-Enhanced Analysis Methods

    private async Task AnalyzeEnterpriseApplicationsWithGraphAsync(
        Guid clientId,
        Guid organizationId,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Enterprise Applications with Microsoft Graph (IAM-focused)...");

        try
        {
            // Get applications and service principals
            var applications = await _graphService.GetApplicationsAsync(clientId, organizationId);
            var servicePrincipals = await _graphService.GetServicePrincipalsAsync(clientId, organizationId);
            var expiredApps = await _graphService.GetApplicationsWithExpiredCredentialsAsync(clientId, organizationId);
            var overprivilegedSPs = await _graphService.GetOverprivilegedServicePrincipalsAsync(clientId, organizationId);

            results.TotalApplications = applications.Count;
            results.RiskyApplications = expiredApps.Count + overprivilegedSPs.Count;

            // Analyze expired credentials
            foreach (var app in expiredApps)
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "ApplicationExpiredCredentials",
                    ResourceId = app.Id ?? string.Empty,
                    ResourceName = app.DisplayName ?? "Unknown Application",
                    Severity = "High",
                    Description = "Application has expired credentials which may cause service outages",
                    Recommendation = "Renew expired credentials and implement automated credential rotation",
                    BusinessImpact = "Applications with expired credentials will fail authentication and cause service disruptions"
                });
            }

            // Analyze overprivileged service principals
            foreach (var sp in overprivilegedSPs)
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "OverprivilegedServicePrincipal",
                    ResourceId = sp.Id ?? string.Empty,
                    ResourceName = sp.DisplayName ?? "Unknown Service Principal",
                    Severity = "High",
                    Description = "Service principal has excessive permissions that exceed its operational requirements",
                    Recommendation = "Review and reduce permissions to follow principle of least privilege",
                    BusinessImpact = "Overprivileged service principals increase attack surface and security risk"
                });
            }

            // Analyze applications without credentials (potential security risk)
            var appsWithoutCreds = applications.Where(app =>
                (app.PasswordCredentials == null || !app.PasswordCredentials.Any()) &&
                (app.KeyCredentials == null || !app.KeyCredentials.Any())).ToList();

            foreach (var app in appsWithoutCreds)
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "ApplicationWithoutCredentials",
                    ResourceId = app.Id ?? string.Empty,
                    ResourceName = app.DisplayName ?? "Unknown Application",
                    Severity = "Medium",
                    Description = "Application registration exists without any configured credentials",
                    Recommendation = "Review if application is still needed or configure appropriate credentials",
                    BusinessImpact = "Unused application registrations create unnecessary attack surface"
                });
            }

            _logger.LogInformation("Enterprise Applications IAM analysis completed with Graph. Total: {Total}, Risky: {Risky}",
                results.TotalApplications, results.RiskyApplications);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze enterprise applications with Graph for client {ClientId}", clientId);

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "EnterpriseApplicationAnalysisError",
                ResourceId = "graph.error",
                ResourceName = "Enterprise Application Analysis",
                Severity = "Medium",
                Description = "Failed to analyze enterprise applications using Microsoft Graph",
                Recommendation = "Review Microsoft Graph permissions and retry analysis",
                BusinessImpact = "Cannot assess application security risks without Graph access"
            });
        }
    }

    private async Task AnalyzeStaleUsersAndDevicesWithGraphAsync(
        Guid clientId,
        Guid organizationId,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Stale Users and Devices with Microsoft Graph (IAM-focused)...");

        try
        {
            // Get inactive users (haven't signed in for 90+ days)
            var inactiveUsers = await _graphService.GetInactiveUsersAsync(clientId, organizationId, 90);
            var allDevices = await _graphService.GetDevicesAsync(clientId, organizationId);
            var nonCompliantDevices = await _graphService.GetNonCompliantDevicesAsync(clientId, organizationId);

            results.InactiveUsers = inactiveUsers.Count;
            results.UnmanagedDevices = nonCompliantDevices.Count;

            // Analyze inactive users
            foreach (var user in inactiveUsers.Take(10)) // Limit to top 10 for findings
            {
                var daysSinceSignIn = user.SignInActivity?.LastSignInDateTime.HasValue == true
                    ? (DateTime.UtcNow - user.SignInActivity.LastSignInDateTime.Value.DateTime).Days
                    : 999;

                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "InactiveUser",
                    ResourceId = user.Id ?? string.Empty,
                    ResourceName = user.DisplayName ?? user.UserPrincipalName ?? "Unknown User",
                    Severity = daysSinceSignIn > 180 ? "High" : "Medium",
                    Description = $"User has not signed in for {daysSinceSignIn} days",
                    Recommendation = "Review if user account is still needed or disable/remove inactive accounts",
                    BusinessImpact = "Inactive user accounts increase attack surface and compliance risks"
                });
            }

            // Analyze non-compliant devices
            foreach (var device in nonCompliantDevices.Take(10)) // Limit to top 10 for findings
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "NonCompliantDevice",
                    ResourceId = device.Id ?? string.Empty,
                    ResourceName = device.DisplayName ?? "Unknown Device",
                    Severity = "Medium",
                    Description = "Device does not meet compliance policies",
                    Recommendation = "Update device to meet compliance requirements or restrict access",
                    BusinessImpact = "Non-compliant devices may lack security controls and pose security risks"
                });
            }

            // Add summary finding if there are many inactive users
            if (inactiveUsers.Count > 10)
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "HighInactiveUserCount",
                    ResourceId = "users.inactive.summary",
                    ResourceName = "Inactive User Summary",
                    Severity = "Medium",
                    Description = $"Found {inactiveUsers.Count} inactive users (90+ days without sign-in)",
                    Recommendation = "Implement regular user lifecycle review process and automated cleanup policies",
                    BusinessImpact = "Large number of inactive users increases administrative overhead and security risks"
                });
            }

            _logger.LogInformation("Users and Devices IAM analysis completed with Graph. Inactive Users: {InactiveUsers}, Non-compliant Devices: {NonCompliantDevices}",
                results.InactiveUsers, results.UnmanagedDevices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze users and devices with Graph for client {ClientId}", clientId);

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "UserDeviceAnalysisError",
                ResourceId = "graph.userdevice.error",
                ResourceName = "User Device Analysis",
                Severity = "Medium",
                Description = "Failed to analyze users and devices using Microsoft Graph",
                Recommendation = "Review Microsoft Graph permissions and retry analysis",
                BusinessImpact = "Cannot assess user lifecycle and device security risks without Graph access"
            });
        }
    }

    private async Task AnalyzeResourceIamRbacWithGraphAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Resource IAM/RBAC with Microsoft Graph enhancement (IAM-focused)...");

        try
        {
            // Get privileged users from Graph
            var privilegedUsers = await _graphService.GetPrivilegedUsersAsync(clientId, organizationId);
            var directoryRoles = await _graphService.GetDirectoryRolesAsync(clientId, organizationId);
            var roleAnalysis = await _graphService.AnalyzeRoleAssignmentsAsync(clientId, organizationId);

            // ALSO run Azure Resource Graph analysis for Azure RBAC
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Analyze role complexity and potential over-permissioning
            var resourceGroups = allResources.GroupBy(r => r.ResourceGroup ?? "Unknown").ToList();
            foreach (var rgGroup in resourceGroups)
            {
                var resourceGroupName = rgGroup.Key;
                var resourcesInRG = rgGroup.ToList();
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
            }

            // Add Graph-specific findings only if we got results
            if (privilegedUsers.Any())
            {
                results.OverprivilegedAssignments = roleAnalysis.OverprivilegedUsers.Count;

                // Analyze privileged users (limit to avoid too many findings)
                foreach (var user in privilegedUsers.Take(5))
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "PrivilegedUserReview",
                        ResourceId = user.Id ?? string.Empty,
                        ResourceName = user.DisplayName ?? user.UserPrincipalName ?? "Unknown User",
                        Severity = "Medium",
                        Description = "User has privileged directory roles that require regular review",
                        Recommendation = "Implement regular access reviews for privileged users and consider using PIM",
                        BusinessImpact = "Privileged users have elevated access that could cause significant damage if compromised"
                    });
                }

                // Add summary for role assignments
                if (roleAnalysis.TotalRoleAssignments > 0)
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "RoleAssignmentSummary",
                        ResourceId = "roles.summary",
                        ResourceName = "Role Assignment Summary",
                        Severity = "Low",
                        Description = $"Found {roleAnalysis.TotalRoleAssignments} role assignments with {roleAnalysis.PrivilegedRoleAssignments} privileged assignments",
                        Recommendation = "Regularly review role assignments and implement principle of least privilege",
                        BusinessImpact = "Excessive role assignments increase security risk and compliance complexity"
                    });
                }
            }
            else
            {
                // Only show limited finding if Graph analysis actually failed
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "RbacAnalysisLimited",
                    ResourceId = "azure.rbac",
                    ResourceName = "RBAC Analysis",
                    Severity = "Medium",
                    Description = "Azure AD role analysis requires additional Microsoft Graph permissions",
                    Recommendation = "Grant Microsoft Graph permissions to analyze directory roles and privileged access",
                    BusinessImpact = "Cannot assess Azure AD role assignment security without Graph API access"
                });
            }

            _logger.LogInformation("RBAC IAM analysis completed with Graph enhancement. Privileged users: {PrivilegedUsers}, Azure resources: {ResourceCount}",
                privilegedUsers.Count, allResources.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze RBAC with Graph for client {ClientId}", clientId);

            // Fall back to standard RBAC analysis on error
            await AnalyzeResourceIamRbacAsync(subscriptionIds, results, findings, cancellationToken);
        }
    }

    private async Task AnalyzeConditionalAccessWithGraphAsync(
        Guid clientId,
        Guid organizationId,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Conditional Access with Microsoft Graph (IAM-focused)...");

        try
        {
            // Get conditional access policies
            var caPolicies = await _graphService.GetConditionalAccessPoliciesAsync(clientId, organizationId);
            var usersNotCoveredByMfa = await _graphService.GetUsersNotCoveredByMfaAsync(clientId, organizationId);
            var caCoverage = await _graphService.AnalyzeConditionalAccessCoverageAsync(clientId, organizationId);

            // Only proceed if we actually got policy data
            if (caPolicies.Any() || caCoverage.TotalUsers > 0)
            {
                results.ConditionalAccessCoverage = new ConditionalAccessCoverage
                {
                    TotalPolicies = caPolicies.Count,
                    EnabledPolicies = caPolicies.Count(p => p.State == Microsoft.Graph.Models.ConditionalAccessPolicyState.Enabled),
                    CoveragePercentage = caCoverage.TotalUsers > 0 ?
                        (decimal)caCoverage.UsersCoveredByMfa / caCoverage.TotalUsers * 100 : 0
                };

                // Analyze disabled CA policies
                var disabledPolicies = caPolicies.Where(p => p.State == Microsoft.Graph.Models.ConditionalAccessPolicyState.Disabled).ToList();
                foreach (var policy in disabledPolicies)
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "DisabledConditionalAccessPolicy",
                        ResourceId = policy.Id ?? string.Empty,
                        ResourceName = policy.DisplayName ?? "Unknown Policy",
                        Severity = "Medium",
                        Description = "Conditional Access policy is disabled and not providing protection",
                        Recommendation = "Review and enable policy or remove if no longer needed",
                        BusinessImpact = "Disabled policies do not provide intended security protections"
                    });
                }

                // Analyze users not covered by MFA
                if (usersNotCoveredByMfa.Count > 0)
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "UsersWithoutMfaCoverage",
                        ResourceId = "ca.mfa.coverage",
                        ResourceName = "MFA Coverage Gap",
                        Severity = "High",
                        Description = $"{usersNotCoveredByMfa.Count} users are not covered by MFA requirements",
                        Recommendation = "Implement Conditional Access policies to require MFA for all users",
                        BusinessImpact = "Users without MFA requirements are vulnerable to credential-based attacks"
                    });
                }

                // Check for policy gaps
                foreach (var gap in caCoverage.PolicyGaps)
                {
                    findings.Add(new IdentitySecurityFinding
                    {
                        FindingType = "ConditionalAccessGap",
                        ResourceId = "ca.gap",
                        ResourceName = "Conditional Access Gap",
                        Severity = "Medium",
                        Description = gap,
                        Recommendation = "Review and address Conditional Access policy gaps",
                        BusinessImpact = "Policy gaps may leave critical scenarios unprotected"
                    });
                }

                _logger.LogInformation("Conditional Access IAM analysis completed with Graph. Policies: {TotalPolicies}, Coverage: {Coverage}%",
                    caPolicies.Count, results.ConditionalAccessCoverage.CoveragePercentage);
            }
            else
            {
                // Only show limited finding if we couldn't get any policy data
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "ConditionalAccessAnalysisLimited",
                    ResourceId = "conditional.access",
                    ResourceName = "Conditional Access Analysis",
                    Severity = "Medium",
                    Description = "Conditional Access analysis requires Microsoft Graph API permissions for comprehensive review",
                    Recommendation = "Grant Microsoft Graph permissions to analyze Conditional Access policies, compliance, and coverage",
                    BusinessImpact = "Cannot assess conditional access security posture without Graph API access"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Conditional Access with Graph for client {ClientId}", clientId);

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "ConditionalAccessAnalysisError",
                ResourceId = "graph.ca.error",
                ResourceName = "Conditional Access Analysis",
                Severity = "Medium",
                Description = "Failed to analyze Conditional Access using Microsoft Graph",
                Recommendation = "Review Microsoft Graph permissions and retry analysis",
                BusinessImpact = "Cannot assess conditional access security posture without Graph access"
            });
        }
    }

    #endregion

    #region Original Analysis Methods (IAM-focused fallback)

    private async Task AnalyzeEnterpriseApplicationsAsync(
        string[] subscriptionIds,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Enterprise Applications (limited IAM analysis without Graph)...");

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Focus on identity-related resources only
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.web/sites")).ToList();
            var functionApps = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.web/sites") &&
                                                      r.Kind?.ToLowerInvariant().Contains("functionapp") == true).ToList();

            results.TotalApplications = appServices.Count + functionApps.Count;

            foreach (var appService in appServices)
            {
                await AnalyzeAppServiceIdentityAsync(appService, findings);
            }

            results.RiskyApplications = findings.Count(f => f.FindingType.Contains("Application"));

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

            _logger.LogInformation("Enterprise Applications IAM analysis completed (limited). Analyzed {ResourceCount} application-related resources",
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

    private async Task AnalyzeAppServiceIdentityAsync(AzureResource appService, List<IdentitySecurityFinding> findings)
    {
        string appServiceName = appService.Name;
        string appServiceId = appService.Id;

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

        await Task.CompletedTask;
    }

    private async Task AnalyzeStaleUsersAndDevicesAsync(
        string[] subscriptionIds,
        IdentityAccessResults results,
        List<IdentitySecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Stale Users and Devices (limited IAM analysis without Graph)...");

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Focus on identity-related resources
            var virtualMachines = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

            foreach (var vm in virtualMachines)
            {
                await AnalyzeVirtualMachineIdentityAsync(vm, findings);
            }

            results.InactiveUsers = findings.Count(f => f.FindingType.Contains("User") || f.FindingType.Contains("Identity"));

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

            _logger.LogInformation("Stale Users and Devices IAM analysis completed (limited). Identity issues: {IdentityIssues}",
                results.InactiveUsers);
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
        _logger.LogInformation("Analyzing Resource IAM/RBAC (limited IAM analysis without Graph)...");

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            var resourceGroups = allResources.GroupBy(r => r.ResourceGroup ?? "Unknown").ToList();

            foreach (var rgGroup in resourceGroups)
            {
                var resourceGroupName = rgGroup.Key;
                var resourcesInRG = rgGroup.ToList();

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
            }

            results.OverprivilegedAssignments = findings.Count(f => f.FindingType.Contains("Overprivileged") || f.FindingType.Contains("Complexity"));

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

            _logger.LogInformation("Resource IAM/RBAC analysis completed (limited). Analyzed {ResourceGroupCount} resource groups",
                resourceGroups.Count);
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
        _logger.LogInformation("Analyzing Conditional Access Policies (limited IAM analysis without Graph)...");

        try
        {
            results.ConditionalAccessCoverage = new ConditionalAccessCoverage
            {
                TotalPolicies = 0,
                EnabledPolicies = 0,
                CoveragePercentage = 0
            };

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

            _logger.LogInformation("Conditional Access IAM analysis completed (limited).");
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

    #endregion

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
}