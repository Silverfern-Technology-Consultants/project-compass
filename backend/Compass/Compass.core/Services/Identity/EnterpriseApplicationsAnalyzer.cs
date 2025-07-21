using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.Identity;

public class EnterpriseApplicationsAnalyzer : IEnterpriseApplicationsAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly IMicrosoftGraphService _graphService;
    private readonly ILogger<EnterpriseApplicationsAnalyzer> _logger;

    public EnterpriseApplicationsAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        IMicrosoftGraphService graphService,
        ILogger<EnterpriseApplicationsAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Enterprise Applications analysis (limited - no Graph access)");

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

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

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("Enterprise Applications analysis completed (limited). Analyzed {ResourceCount} application-related resources",
                results.TotalApplications);

            return results;
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

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
        }
    }

    public async Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Enterprise Applications analysis with Microsoft Graph enhancement");

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

            _logger.LogInformation("Microsoft Graph access confirmed for client {ClientId}, performing enhanced Enterprise Applications analysis", clientId);

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

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("Enterprise Applications analysis completed with Graph. Total: {Total}, Risky: {Risky}",
                results.TotalApplications, results.RiskyApplications);

            return results;
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

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
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

    private decimal CalculateScore(IdentityAccessResults results)
    {
        var score = 100m;

        if (results.TotalApplications > 0)
        {
            var riskPercentage = (decimal)results.RiskyApplications / results.TotalApplications * 100;
            score = Math.Max(0, 100 - (riskPercentage * 2)); // Double penalty for risky apps
        }

        // Penalty for critical and high findings
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var penalty = (criticalFindings * 15) + (highFindings * 8);

        return Math.Max(0, score - penalty);
    }
}