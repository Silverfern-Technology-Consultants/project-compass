using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;

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
    private readonly IEnterpriseApplicationsAnalyzer _enterpriseAppsAnalyzer;
    private readonly IStaleUsersDevicesAnalyzer _staleUsersAnalyzer;
    private readonly IResourceIamRbacAnalyzer _rbacAnalyzer;
    private readonly IConditionalAccessAnalyzer _conditionalAccessAnalyzer;
    private readonly IIdentityFullAnalyzer _fullAnalyzer;
    private readonly ILogger<IdentityAccessAssessmentAnalyzer> _logger;

    public IdentityAccessAssessmentAnalyzer(
        IEnterpriseApplicationsAnalyzer enterpriseAppsAnalyzer,
        IStaleUsersDevicesAnalyzer staleUsersAnalyzer,
        IResourceIamRbacAnalyzer rbacAnalyzer,
        IConditionalAccessAnalyzer conditionalAccessAnalyzer,
        IIdentityFullAnalyzer fullAnalyzer,
        ILogger<IdentityAccessAssessmentAnalyzer> logger)
    {
        _enterpriseAppsAnalyzer = enterpriseAppsAnalyzer;
        _staleUsersAnalyzer = staleUsersAnalyzer;
        _rbacAnalyzer = rbacAnalyzer;
        _conditionalAccessAnalyzer = conditionalAccessAnalyzer;
        _fullAnalyzer = fullAnalyzer;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeIdentityAccessAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting modular Identity Access Management analysis for assessment type: {AssessmentType}", assessmentType);

        try
        {
            var results = assessmentType switch
            {
                AssessmentType.EnterpriseApplications => await _enterpriseAppsAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.StaleUsersDevices => await _staleUsersAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.ResourceIamRbac => await _rbacAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.ConditionalAccess => await _conditionalAccessAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.IdentityFull => await _fullAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                _ => throw new ArgumentException($"Unsupported IAM assessment type: {assessmentType}")
            };

            _logger.LogInformation("Modular Identity Access Management analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, results.SecurityFindings.Count);

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
        _logger.LogInformation("Starting OAuth-enabled modular Identity Access Management analysis for client {ClientId}, assessment type: {AssessmentType}",
            clientId, assessmentType);

        try
        {
            var results = assessmentType switch
            {
                AssessmentType.EnterpriseApplications => await _enterpriseAppsAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.StaleUsersDevices => await _staleUsersAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.ResourceIamRbac => await _rbacAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.ConditionalAccess => await _conditionalAccessAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.IdentityFull => await _fullAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                _ => throw new ArgumentException($"Unsupported IAM assessment type: {assessmentType}")
            };

            _logger.LogInformation("OAuth-enabled modular Identity Access Management analysis completed for client {ClientId}. Score: {Score}%, Issues found: {IssuesCount}",
                clientId, results.Score, results.SecurityFindings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Identity Access Management with OAuth for client {ClientId}, assessment type: {AssessmentType}",
                clientId, assessmentType);

            // Fall back to standard analysis
            _logger.LogInformation("Falling back to standard IAM analysis for client {ClientId}", clientId);
            return await AnalyzeIdentityAccessAsync(subscriptionIds, assessmentType, cancellationToken);
        }
    }
}