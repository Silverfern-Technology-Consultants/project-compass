using Compass.Core.Interfaces;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services.Identity;

public class ResourceIamRbacAnalyzer : IResourceIamRbacAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly IMicrosoftGraphService _graphService;
    private readonly ILogger<ResourceIamRbacAnalyzer> _logger;

    public ResourceIamRbacAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        IMicrosoftGraphService graphService,
        ILogger<ResourceIamRbacAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Resource IAM/RBAC analysis (limited - no Graph access)");

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

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

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("Resource IAM/RBAC analysis completed (limited). Analyzed {ResourceGroupCount} resource groups",
                resourceGroups.Count);

            return results;
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

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
        }
    }

    public async Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Resource IAM/RBAC analysis with Microsoft Graph enhancement");

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

        try
        {
            // Test Graph access first
            var hasGraphAccess = await _graphService.TestGraphConnectionAsync(clientId, organizationId);

            if (!hasGraphAccess)
            {
                _logger.LogWarning("Microsoft Graph access not available for client {ClientId}, falling back to limited analysis", clientId);
                return await AnalyzeAsync(subscriptionIds, cancellationToken);
            }

            _logger.LogInformation("Microsoft Graph access confirmed for client {ClientId}, performing enhanced RBAC analysis", clientId);

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

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("RBAC analysis completed with Graph enhancement. Privileged users: {PrivilegedUsers}, Azure resources: {ResourceCount}",
                privilegedUsers.Count, allResources.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze RBAC with Graph for client {ClientId}", clientId);

            // Fall back to standard RBAC analysis on error
            return await AnalyzeAsync(subscriptionIds, cancellationToken);
        }
    }

    private decimal CalculateScore(IdentityAccessResults results)
    {
        var score = 100m;

        // RBAC score penalty for overprivileged assignments
        if (results.OverprivilegedAssignments > 0)
        {
            // Heavily penalize overprivileged assignments
            score = Math.Max(0, 100 - (results.OverprivilegedAssignments * 10));
        }

        // Penalty for critical and high findings
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var penalty = (criticalFindings * 15) + (highFindings * 8);

        return Math.Max(0, score - penalty);
    }
}