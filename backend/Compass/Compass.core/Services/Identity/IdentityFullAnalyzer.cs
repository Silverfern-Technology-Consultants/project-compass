using Compass.Core.Interfaces;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services.Identity;

public class IdentityFullAnalyzer : IIdentityFullAnalyzer
{
    private readonly IEnterpriseApplicationsAnalyzer _enterpriseAppsAnalyzer;
    private readonly IStaleUsersDevicesAnalyzer _staleUsersAnalyzer;
    private readonly IResourceIamRbacAnalyzer _rbacAnalyzer;
    private readonly IConditionalAccessAnalyzer _conditionalAccessAnalyzer;
    private readonly ILogger<IdentityFullAnalyzer> _logger;

    public IdentityFullAnalyzer(
        IEnterpriseApplicationsAnalyzer enterpriseAppsAnalyzer,
        IStaleUsersDevicesAnalyzer staleUsersAnalyzer,
        IResourceIamRbacAnalyzer rbacAnalyzer,
        IConditionalAccessAnalyzer conditionalAccessAnalyzer,
        ILogger<IdentityFullAnalyzer> logger)
    {
        _enterpriseAppsAnalyzer = enterpriseAppsAnalyzer;
        _staleUsersAnalyzer = staleUsersAnalyzer;
        _rbacAnalyzer = rbacAnalyzer;
        _conditionalAccessAnalyzer = conditionalAccessAnalyzer;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Full Identity Access Management analysis (orchestrating all IAM analyzers)");

        try
        {
            // Run all individual IAM analyses
            var enterpriseAppsTask = _enterpriseAppsAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);
            var staleUsersTask = _staleUsersAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);
            var rbacTask = _rbacAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);
            var conditionalAccessTask = _conditionalAccessAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);

            await Task.WhenAll(enterpriseAppsTask, staleUsersTask, rbacTask, conditionalAccessTask);

            var enterpriseAppsResults = await enterpriseAppsTask;
            var staleUsersResults = await staleUsersTask;
            var rbacResults = await rbacTask;
            var conditionalAccessResults = await conditionalAccessTask;

            // Combine results
            var combinedResults = CombineResults(enterpriseAppsResults, staleUsersResults, rbacResults, conditionalAccessResults);

            _logger.LogInformation("Full Identity Access Management analysis completed. Combined score: {Score}%, Total findings: {FindingsCount}",
                combinedResults.Score, combinedResults.SecurityFindings.Count);

            return combinedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete full identity access management analysis");
            throw;
        }
    }

    public async Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Full Identity Access Management analysis for client {ClientId}", clientId);

        try
        {
            // Run all individual IAM analyses with OAuth
            var enterpriseAppsTask = _enterpriseAppsAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);
            var staleUsersTask = _staleUsersAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);
            var rbacTask = _rbacAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);
            var conditionalAccessTask = _conditionalAccessAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);

            await Task.WhenAll(enterpriseAppsTask, staleUsersTask, rbacTask, conditionalAccessTask);

            var enterpriseAppsResults = await enterpriseAppsTask;
            var staleUsersResults = await staleUsersTask;
            var rbacResults = await rbacTask;
            var conditionalAccessResults = await conditionalAccessTask;

            // Combine results
            var combinedResults = CombineResults(enterpriseAppsResults, staleUsersResults, rbacResults, conditionalAccessResults);

            _logger.LogInformation("OAuth-enabled Full Identity Access Management analysis completed for client {ClientId}. Combined score: {Score}%, Total findings: {FindingsCount}",
                clientId, combinedResults.Score, combinedResults.SecurityFindings.Count);

            return combinedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete OAuth-enabled full identity access management analysis for client {ClientId}", clientId);
            throw;
        }
    }

    private IdentityAccessResults CombineResults(
        IdentityAccessResults enterpriseAppsResults,
        IdentityAccessResults staleUsersResults,
        IdentityAccessResults rbacResults,
        IdentityAccessResults conditionalAccessResults)
    {
        var combinedResults = new IdentityAccessResults();

        // Combine metrics (take max/sum as appropriate)
        combinedResults.TotalApplications = Math.Max(enterpriseAppsResults.TotalApplications, 0);
        combinedResults.RiskyApplications = Math.Max(enterpriseAppsResults.RiskyApplications, 0);
        combinedResults.InactiveUsers = Math.Max(staleUsersResults.InactiveUsers, 0);
        combinedResults.UnmanagedDevices = Math.Max(staleUsersResults.UnmanagedDevices, 0);
        combinedResults.OverprivilegedAssignments = Math.Max(rbacResults.OverprivilegedAssignments, 0);
        combinedResults.ConditionalAccessCoverage = conditionalAccessResults.ConditionalAccessCoverage;

        // Combine all security findings
        var allFindings = new List<IdentitySecurityFinding>();
        allFindings.AddRange(enterpriseAppsResults.SecurityFindings);
        allFindings.AddRange(staleUsersResults.SecurityFindings);
        allFindings.AddRange(rbacResults.SecurityFindings);
        allFindings.AddRange(conditionalAccessResults.SecurityFindings);

        combinedResults.SecurityFindings = allFindings;

        // Calculate overall score using the same logic as the main analyzer
        combinedResults.Score = CalculateOverallIamScore(combinedResults);

        // Combine detailed metrics
        combinedResults.DetailedMetrics = CombineDetailedMetrics(
            enterpriseAppsResults.DetailedMetrics,
            staleUsersResults.DetailedMetrics,
            rbacResults.DetailedMetrics,
            conditionalAccessResults.DetailedMetrics);

        return combinedResults;
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

        _logger.LogInformation("Full IAM Score calculated: {Score}% (App: {AppScore}%, User/Device: {UserScore}%, RBAC: {RbacScore}%, CA: {CaScore}%, Penalty: {Penalty})",
            finalScore, appSecurityScore, userDeviceScore, rbacScore, caScore, penalty);

        return Math.Round(finalScore, 2);
    }

    private Dictionary<string, object> CombineDetailedMetrics(params Dictionary<string, object>[] metrics)
    {
        var combined = new Dictionary<string, object>();

        foreach (var metric in metrics)
        {
            foreach (var kvp in metric)
            {
                if (!combined.ContainsKey(kvp.Key))
                {
                    combined[kvp.Key] = kvp.Value;
                }
                else
                {
                    // If key exists, try to combine values intelligently
                    if (kvp.Value is int intValue && combined[kvp.Key] is int existingIntValue)
                    {
                        combined[kvp.Key] = existingIntValue + intValue;
                    }
                    else if (kvp.Value is decimal decimalValue && combined[kvp.Key] is decimal existingDecimalValue)
                    {
                        combined[kvp.Key] = (existingDecimalValue + decimalValue) / 2; // Average for decimals
                    }
                    // For other types, keep the first value
                }
            }
        }

        return combined;
    }
}