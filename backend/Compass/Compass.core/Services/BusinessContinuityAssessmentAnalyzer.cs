using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Compass.Core.Services.BusinessContinuity;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface IBusinessContinuityAssessmentAnalyzer
{
    Task<BusinessContinuityResults> AnalyzeBusinessContinuityAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);

    Task<BusinessContinuityResults> AnalyzeBusinessContinuityWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);
}

public class BusinessContinuityAssessmentAnalyzer : IBusinessContinuityAssessmentAnalyzer
{
    private readonly IBackupCoverageAnalyzer _backupCoverageAnalyzer;
    private readonly IRecoveryConfigurationAnalyzer _recoveryConfigurationAnalyzer;
    private readonly IBusinessContinuityFullAnalyzer _businessContinuityFullAnalyzer;
    private readonly ILogger<BusinessContinuityAssessmentAnalyzer> _logger;

    public BusinessContinuityAssessmentAnalyzer(
        IBackupCoverageAnalyzer backupCoverageAnalyzer,
        IRecoveryConfigurationAnalyzer recoveryConfigurationAnalyzer,
        IBusinessContinuityFullAnalyzer businessContinuityFullAnalyzer,
        ILogger<BusinessContinuityAssessmentAnalyzer> logger)
    {
        _backupCoverageAnalyzer = backupCoverageAnalyzer;
        _recoveryConfigurationAnalyzer = recoveryConfigurationAnalyzer;
        _businessContinuityFullAnalyzer = businessContinuityFullAnalyzer;
        _logger = logger;
    }

    public async Task<BusinessContinuityResults> AnalyzeBusinessContinuityAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting modular Business Continuity analysis for assessment type: {AssessmentType}", assessmentType);

        try
        {
            var results = assessmentType switch
            {
                AssessmentType.BackupCoverage => await _backupCoverageAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.RecoveryConfiguration => await _recoveryConfigurationAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                AssessmentType.BusinessContinuityFull => await _businessContinuityFullAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken),
                _ => throw new ArgumentException($"Unsupported BCDR assessment type: {assessmentType}")
            };

            _logger.LogInformation("Modular Business Continuity analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, results.Findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Business Continuity for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<BusinessContinuityResults> AnalyzeBusinessContinuityWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled modular Business Continuity analysis for client {ClientId}, assessment type: {AssessmentType}",
            clientId, assessmentType);

        try
        {
            var results = assessmentType switch
            {
                AssessmentType.BackupCoverage => await _backupCoverageAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.RecoveryConfiguration => await _recoveryConfigurationAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                AssessmentType.BusinessContinuityFull => await _businessContinuityFullAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken),
                _ => throw new ArgumentException($"Unsupported BCDR assessment type: {assessmentType}")
            };

            _logger.LogInformation("OAuth-enabled modular Business Continuity analysis completed for client {ClientId}. Score: {Score}%, Issues found: {IssuesCount}",
                clientId, results.Score, results.Findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Business Continuity with OAuth for client {ClientId}, assessment type: {AssessmentType}",
                clientId, assessmentType);

            // Fall back to standard analysis
            _logger.LogInformation("Falling back to standard BCDR analysis for client {ClientId}", clientId);
            return await AnalyzeBusinessContinuityAsync(subscriptionIds, assessmentType, cancellationToken);
        }
    }
}