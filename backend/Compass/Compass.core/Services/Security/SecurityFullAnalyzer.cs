using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services.Security;

public interface ISecurityFullAnalyzer
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

public class SecurityFullAnalyzer : ISecurityFullAnalyzer
{
    private readonly INetworkSecurityAnalyzer _networkSecurityAnalyzer;
    private readonly IDefenderForCloudAnalyzer _defenderAnalyzer;
    private readonly ILogger<SecurityFullAnalyzer> _logger;

    public SecurityFullAnalyzer(
        INetworkSecurityAnalyzer networkSecurityAnalyzer,
        IDefenderForCloudAnalyzer defenderAnalyzer,
        ILogger<SecurityFullAnalyzer> logger)
    {
        _networkSecurityAnalyzer = networkSecurityAnalyzer;
        _defenderAnalyzer = defenderAnalyzer;
        _logger = logger;
    }

    public async Task<SecurityPostureResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting comprehensive Security Posture analysis (SecurityFull) for subscriptions: {Subscriptions}",
            string.Join(",", subscriptionIds));

        try
        {
            // Run network security and Defender analyses in parallel
            var networkTask = _networkSecurityAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);
            var defenderTask = _defenderAnalyzer.AnalyzeAsync(subscriptionIds, cancellationToken);

            await Task.WhenAll(networkTask, defenderTask);

            var networkResults = await networkTask;
            var defenderResults = await defenderTask;

            // Combine results using weighted scoring methodology
            var combinedResults = CombineSecurityResults(networkResults, defenderResults);

            // Perform cross-domain security analysis
            await PerformCrossDomainSecurityAnalysis(combinedResults, subscriptionIds);

            _logger.LogInformation("Comprehensive Security Posture analysis completed. Combined Score: {Score}%, Total Issues: {IssuesCount}",
                combinedResults.Score, combinedResults.SecurityFindings.Count);

            return combinedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform comprehensive Security Posture analysis for subscriptions: {Subscriptions}",
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
        _logger.LogInformation("Starting OAuth-enabled comprehensive Security Posture analysis for client {ClientId}", clientId);

        try
        {
            // Run OAuth-enabled analyses in parallel
            var networkTask = _networkSecurityAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);
            var defenderTask = _defenderAnalyzer.AnalyzeWithOAuthAsync(subscriptionIds, clientId, organizationId, cancellationToken);

            await Task.WhenAll(networkTask, defenderTask);

            var networkResults = await networkTask;
            var defenderResults = await defenderTask;

            // Combine results with enhanced OAuth-based insights
            var combinedResults = CombineSecurityResults(networkResults, defenderResults);

            // Perform enhanced cross-domain security analysis with OAuth context
            await PerformEnhancedCrossDomainAnalysisAsync(combinedResults, subscriptionIds, clientId, organizationId);

            _logger.LogInformation("OAuth-enabled comprehensive Security Posture analysis completed for client {ClientId}. Combined Score: {Score}%, Total Issues: {IssuesCount}",
                clientId, combinedResults.Score, combinedResults.SecurityFindings.Count);

            return combinedResults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth-enabled comprehensive Security Posture analysis failed for client {ClientId}, falling back to standard analysis", clientId);

            // Fall back to standard analysis
            return await AnalyzeAsync(subscriptionIds, cancellationToken);
        }
    }

    private SecurityPostureResults CombineSecurityResults(
        SecurityPostureResults networkResults,
        SecurityPostureResults defenderResults)
    {
        _logger.LogInformation("Combining security analysis results with weighted scoring (40% network, 60% defender)");

        var combinedResults = new SecurityPostureResults();

        // Combine network security analysis (preserve existing analysis)
        combinedResults.NetworkSecurity = networkResults.NetworkSecurity ?? new NetworkSecurityAnalysis();

        // Combine Defender for Cloud analysis (preserve existing analysis)
        combinedResults.DefenderAnalysis = defenderResults.DefenderAnalysis ?? new DefenderForCloudAnalysis();

        // Merge security findings from both analyses
        var allFindings = new List<SecurityFinding>();
        allFindings.AddRange(networkResults.SecurityFindings ?? new List<SecurityFinding>());
        allFindings.AddRange(defenderResults.SecurityFindings ?? new List<SecurityFinding>());

        // Remove any potential duplicates based on ResourceId and Issue
        combinedResults.SecurityFindings = allFindings
            .GroupBy(f => new { f.ResourceId, f.Issue })
            .Select(g => g.First())
            .ToList();

        // Calculate weighted combined score (40% network, 60% defender)
        var networkWeight = 0.40m;
        var defenderWeight = 0.60m;

        var networkScore = networkResults.Score;
        var defenderScore = defenderResults.Score;

        combinedResults.Score = Math.Round(
            (networkScore * networkWeight) + (defenderScore * defenderWeight),
            2);

        // Apply additional penalties for cross-domain security gaps
        var crossDomainPenalty = CalculateCrossDomainSecurityPenalty(combinedResults);
        combinedResults.Score = Math.Max(0, combinedResults.Score - crossDomainPenalty);

        _logger.LogInformation("Security results combined. Network Score: {NetworkScore}% (weight: {NetworkWeight}%), " +
                             "Defender Score: {DefenderScore}% (weight: {DefenderWeight}%), " +
                             "Combined Score: {CombinedScore}%, Cross-domain penalty: {Penalty}%",
            networkScore, networkWeight * 100,
            defenderScore, defenderWeight * 100,
            combinedResults.Score, crossDomainPenalty);

        return combinedResults;
    }

    private async Task PerformCrossDomainSecurityAnalysis(
        SecurityPostureResults combinedResults,
        string[] subscriptionIds)
    {
        _logger.LogInformation("Performing cross-domain security analysis for comprehensive insights");

        // Analyze security governance alignment
        await AnalyzeSecurityGovernanceAlignment(combinedResults);

        // Analyze security monitoring coverage
        await AnalyzeSecurityMonitoringCoverage(combinedResults);

        // Analyze threat detection capabilities
        await AnalyzeThreatDetectionCapabilities(combinedResults);

        // Analyze incident response readiness
        await AnalyzeIncidentResponseReadiness(combinedResults);

        // Analyze security cost optimization opportunities
        await AnalyzeSecurityCostOptimization(combinedResults);
    }

    private async Task PerformEnhancedCrossDomainAnalysisAsync(
        SecurityPostureResults combinedResults,
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId)
    {
        _logger.LogInformation("Performing enhanced cross-domain security analysis with OAuth context for client {ClientId}", clientId);

        // Perform standard cross-domain analysis first
        await PerformCrossDomainSecurityAnalysis(combinedResults, subscriptionIds);

        // Add OAuth-enhanced insights
        await AnalyzeOAuthSecurityContext(combinedResults, clientId, organizationId);

        // Analyze client-specific security patterns
        await AnalyzeClientSecurityPatterns(combinedResults, clientId);
    }

    private async Task AnalyzeSecurityGovernanceAlignment(SecurityPostureResults results)
    {
        // Check alignment between network security and Defender configurations
        var networkFindings = results.SecurityFindings.Where(f => f.Category == "Network").ToList();
        var defenderFindings = results.SecurityFindings.Where(f => f.Category == "DefenderForCloud").ToList();

        if (networkFindings.Any(f => f.Severity == "High") &&
            !results.DefenderAnalysis.IsEnabled)
        {
            results.SecurityFindings.Add(new SecurityFinding
            {
                Category = "SecurityGovernance",
                ResourceId = "governance.alignment",
                ResourceName = "Security Governance Alignment",
                SecurityControl = "Holistic Security Management",
                Issue = "High-severity network security issues detected but Microsoft Defender for Cloud is not fully enabled",
                Recommendation = "Enable comprehensive Defender for Cloud protection to complement network security controls and provide unified security monitoring",
                Severity = "High",
                ComplianceFramework = "Security Integration Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSecurityMonitoringCoverage(SecurityPostureResults results)
    {
        // Analyze gaps in security monitoring coverage
        var hasNetworkMonitoring = results.NetworkSecurity.NetworkSecurityGroups > 0;
        var hasDefenderMonitoring = results.DefenderAnalysis.IsEnabled;

        if (!hasNetworkMonitoring && !hasDefenderMonitoring)
        {
            results.SecurityFindings.Add(new SecurityFinding
            {
                Category = "SecurityMonitoring",
                ResourceId = "monitoring.coverage",
                ResourceName = "Security Monitoring Coverage",
                SecurityControl = "Security Observability",
                Issue = "Insufficient security monitoring coverage across network and endpoint protection",
                Recommendation = "Implement comprehensive security monitoring including Network Watcher, NSG flow logs, and Microsoft Defender for Cloud",
                Severity = "Critical",
                ComplianceFramework = "Security Monitoring Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeThreatDetectionCapabilities(SecurityPostureResults results)
    {
        // Analyze threat detection capabilities across domains
        var networkThreatDetection = results.SecurityFindings.Any(f =>
            f.Category == "Network" && f.SecurityControl.Contains("Threat"));

        var defenderThreatDetection = results.DefenderAnalysis.DefenderPlansStatus.Any();

        if (!networkThreatDetection && !defenderThreatDetection)
        {
            results.SecurityFindings.Add(new SecurityFinding
            {
                Category = "ThreatDetection",
                ResourceId = "threat.detection",
                ResourceName = "Threat Detection Capabilities",
                SecurityControl = "Advanced Threat Protection",
                Issue = "Limited threat detection capabilities across network and endpoint layers",
                Recommendation = "Implement Azure Sentinel for SIEM capabilities, enable Defender plans for advanced threat detection, and configure network-based threat detection",
                Severity = "High",
                ComplianceFramework = "NIST Cybersecurity Framework"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeIncidentResponseReadiness(SecurityPostureResults results)
    {
        // Analyze incident response readiness
        var hasSecurityContacts = results.SecurityFindings.Any(f =>
            f.ResourceId == "security.contacts" && f.Category == "DefenderForCloud");

        var hasNetworkSegmentation = results.NetworkSecurity.NetworkSecurityGroups > 0;

        if (hasSecurityContacts && !hasNetworkSegmentation)
        {
            results.SecurityFindings.Add(new SecurityFinding
            {
                Category = "IncidentResponse",
                ResourceId = "incident.response.readiness",
                ResourceName = "Incident Response Readiness",
                SecurityControl = "Incident Containment",
                Issue = "Security contacts configured but network segmentation capabilities are limited for incident containment",
                Recommendation = "Implement network segmentation with NSGs and prepare incident response playbooks for network isolation procedures",
                Severity = "Medium",
                ComplianceFramework = "Incident Response Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSecurityCostOptimization(SecurityPostureResults results)
    {
        // Analyze security cost optimization opportunities
        var defenderPlansCount = results.DefenderAnalysis.DefenderPlansStatus.Count;
        var networkSecurityCount = results.NetworkSecurity.NetworkSecurityGroups;

        if (defenderPlansCount > 5 && networkSecurityCount < 3)
        {
            results.SecurityFindings.Add(new SecurityFinding
            {
                Category = "SecurityCostOptimization",
                ResourceId = "cost.optimization",
                ResourceName = "Security Cost Optimization",
                SecurityControl = "Cost-Effective Security",
                Issue = "Multiple Defender plans may be enabled but basic network security controls are limited",
                Recommendation = "Review Defender plan necessity and ensure foundational network security controls are in place before advanced paid protections",
                Severity = "Low",
                ComplianceFramework = "Cost Optimization Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeOAuthSecurityContext(SecurityPostureResults results, Guid clientId, Guid organizationId)
    {
        // OAuth-specific security analysis
        results.SecurityFindings.Add(new SecurityFinding
        {
            Category = "OAuthSecurity",
            ResourceId = $"oauth.security.{clientId}",
            ResourceName = "OAuth Security Context",
            SecurityControl = "Delegated Access Security",
            Issue = "OAuth-delegated security analysis provides enhanced visibility but requires ongoing token security monitoring",
            Recommendation = "Implement OAuth token lifecycle management, monitor for suspicious OAuth usage patterns, and ensure proper scope limitations",
            Severity = "Medium",
            ComplianceFramework = "OAuth Security Best Practice"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeClientSecurityPatterns(SecurityPostureResults results, Guid clientId)
    {
        // Client-specific security pattern analysis
        // This would typically analyze historical patterns for this client
        _logger.LogInformation("Analyzing security patterns for client {ClientId}", clientId);

        await Task.CompletedTask;
    }

    private decimal CalculateCrossDomainSecurityPenalty(SecurityPostureResults results)
    {
        decimal penalty = 0m;

        // Penalty for misaligned security controls
        var criticalNetworkFindings = results.SecurityFindings.Count(f =>
            f.Category == "Network" && f.Severity == "Critical");

        var criticalDefenderFindings = results.SecurityFindings.Count(f =>
            f.Category == "DefenderForCloud" && f.Severity == "Critical");

        // If there are critical findings in one domain but not proper coverage in the other
        if (criticalNetworkFindings > 0 && !results.DefenderAnalysis.IsEnabled)
        {
            penalty += 10m; // 10% penalty for lack of comprehensive protection
        }

        if (criticalDefenderFindings > 0 && results.NetworkSecurity.NetworkSecurityGroups == 0)
        {
            penalty += 8m; // 8% penalty for lack of network-level protection
        }

        // Penalty for excessive security gaps across domains
        var totalCriticalFindings = criticalNetworkFindings + criticalDefenderFindings;
        if (totalCriticalFindings > 5)
        {
            penalty += (totalCriticalFindings - 5) * 2m; // 2% per additional critical finding
        }

        return Math.Min(penalty, 25m); // Cap penalty at 25%
    }
}