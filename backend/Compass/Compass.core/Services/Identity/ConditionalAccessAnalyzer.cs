using Compass.Core.Interfaces;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services.Identity;

public class ConditionalAccessAnalyzer : IConditionalAccessAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly IMicrosoftGraphService _graphService;
    private readonly ILogger<ConditionalAccessAnalyzer> _logger;

    public ConditionalAccessAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        IMicrosoftGraphService graphService,
        ILogger<ConditionalAccessAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Conditional Access analysis (limited - no Graph access)");

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

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

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("Conditional Access analysis completed (limited).");

            return results;
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

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
        }
    }

    public async Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Conditional Access analysis with Microsoft Graph enhancement");

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

            _logger.LogInformation("Microsoft Graph access confirmed for client {ClientId}, performing enhanced Conditional Access analysis", clientId);

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

                _logger.LogInformation("Conditional Access analysis completed with Graph. Policies: {TotalPolicies}, Coverage: {Coverage}%",
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

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            return results;
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

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
        }
    }

    private decimal CalculateScore(IdentityAccessResults results)
    {
        // Conditional Access score based on coverage percentage
        var caScore = results.ConditionalAccessCoverage.CoveragePercentage;

        // If no CA analysis was possible, give partial credit to avoid penalizing too heavily
        if (caScore == 0 && results.ConditionalAccessCoverage.TotalPolicies == 0)
        {
            caScore = 50m; // Neutral score when analysis isn't possible
        }

        // Penalty for critical and high findings
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var penalty = (criticalFindings * 15) + (highFindings * 8);

        var finalScore = Math.Max(0, caScore - penalty);

        return Math.Round(finalScore, 2);
    }
}