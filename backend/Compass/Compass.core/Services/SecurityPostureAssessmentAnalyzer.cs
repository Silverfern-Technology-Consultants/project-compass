using Compass.Core.Interfaces;
using Compass.Core.Models.Assessment;
using Compass.Core.Models;
using Compass.Core.Services.Security;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface ISecurityPostureAssessmentAnalyzer
{
    Task<SecurityPostureResults> AnalyzeSecurityPostureAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);

    Task<SecurityPostureResults> AnalyzeSecurityPostureWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);
}

public class SecurityPostureAssessmentAnalyzer : ISecurityPostureAssessmentAnalyzer
{
    private readonly INetworkSecurityAnalyzer _networkSecurityAnalyzer;
    private readonly IDefenderForCloudAnalyzer _defenderAnalyzer;
    private readonly ISecurityFullAnalyzer _securityFullAnalyzer;
    private readonly ILogger<SecurityPostureAssessmentAnalyzer> _logger;

    public SecurityPostureAssessmentAnalyzer(
        INetworkSecurityAnalyzer networkSecurityAnalyzer,
        IDefenderForCloudAnalyzer defenderAnalyzer,
        ISecurityFullAnalyzer securityFullAnalyzer,
        ILogger<SecurityPostureAssessmentAnalyzer> logger)
    {
        _networkSecurityAnalyzer = networkSecurityAnalyzer;
        _defenderAnalyzer = defenderAnalyzer;
        _securityFullAnalyzer = securityFullAnalyzer;
        _logger = logger;
    }

    public async Task<SecurityPostureResults> AnalyzeSecurityPostureAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting modular Security Posture analysis for assessment type: {AssessmentType}", assessmentType);

        try
        {
            var results = assessmentType switch
            {
                AssessmentType.NetworkSecurity => await _networkSecurityAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.DefenderForCloud => await _defenderAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.SecurityFull => await _securityFullAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                _ => throw new ArgumentException($"Unsupported Security Posture assessment type: {assessmentType}")
            };

            _logger.LogInformation("Modular Security Posture analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, results.SecurityFindings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Security Posture for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<SecurityPostureResults> AnalyzeSecurityPostureWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled modular Security Posture analysis for client {ClientId}, assessment type: {AssessmentType}",
            clientId, assessmentType);

        try
        {
            var results = assessmentType switch
            {
                AssessmentType.NetworkSecurity => await _networkSecurityAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.DefenderForCloud => await _defenderAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.SecurityFull => await _securityFullAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                _ => throw new ArgumentException($"Unsupported Security Posture assessment type: {assessmentType}")
            };

            _logger.LogInformation("OAuth-enabled modular Security Posture analysis completed for client {ClientId}. Score: {Score}%, Issues found: {IssuesCount}",
                clientId, results.Score, results.SecurityFindings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Security Posture with OAuth for client {ClientId}, assessment type: {AssessmentType}",
                clientId, assessmentType);

            // Fall back to standard analysis
            _logger.LogInformation("Falling back to standard Security Posture analysis for client {ClientId}", clientId);
            return await AnalyzeSecurityPostureAsync(subscriptionIds, assessmentType, cancellationToken);
        }
    }
}
