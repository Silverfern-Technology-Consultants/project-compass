using Compass.Core.Models;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface IAssessmentOrchestrator
{
    Task<Guid> StartAssessmentAsync(AssessmentRequest request, CancellationToken cancellationToken = default);
    Task<AssessmentResult?> GetAssessmentResultAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task ProcessPendingAssessmentsAsync(CancellationToken cancellationToken = default);
}

public class AssessmentOrchestrator : IAssessmentOrchestrator
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly INamingConventionAnalyzer _namingAnalyzer;
    private readonly ITaggingAnalyzer _taggingAnalyzer;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ILogger<AssessmentOrchestrator> _logger;

    public AssessmentOrchestrator(
        IAzureResourceGraphService resourceGraphService,
        INamingConventionAnalyzer namingAnalyzer,
        ITaggingAnalyzer taggingAnalyzer,
        IAssessmentRepository assessmentRepository,
        ILogger<AssessmentOrchestrator> logger)
    {
        _resourceGraphService = resourceGraphService;
        _namingAnalyzer = namingAnalyzer;
        _taggingAnalyzer = taggingAnalyzer;
        _assessmentRepository = assessmentRepository;
        _logger = logger;
    }

    public async Task<Guid> StartAssessmentAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting assessment for environment {EnvironmentId}", request.EnvironmentId);

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            EnvironmentId = request.EnvironmentId,
            AssessmentType = request.Type.ToString(),
            Status = AssessmentStatus.Pending.ToString(),
            StartedDate = DateTime.UtcNow,
            CustomerId = Guid.NewGuid(), // In real implementation, get from context
            CustomerName = "Sample Customer" // In real implementation, get from context
        };

        await _assessmentRepository.CreateAsync(assessment);

        // Start processing in background (in a real implementation, you'd use a background service)
        _ = Task.Run(() => ProcessAssessmentAsync(assessment.Id, request, cancellationToken), cancellationToken);

        return assessment.Id;
    }

    public async Task<AssessmentResult?> GetAssessmentResultAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
        if (assessment == null) return null;

        var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);

        return MapToAssessmentResult(assessment, findings);
    }

    public async Task ProcessPendingAssessmentsAsync(CancellationToken cancellationToken = default)
    {
        var pendingAssessments = await _assessmentRepository.GetPendingAssessmentsAsync();

        foreach (var assessment in pendingAssessments)
        {
            try
            {
                // In a real implementation, you'd reconstruct the original request
                var request = new AssessmentRequest
                {
                    EnvironmentId = assessment.EnvironmentId,
                    Type = Enum.Parse<AssessmentType>(assessment.AssessmentType),
                    SubscriptionIds = new[] { "dummy" } // This would come from environment configuration
                };

                await ProcessAssessmentAsync(assessment.Id, request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process assessment {AssessmentId}", assessment.Id);
                await MarkAssessmentAsFailed(assessment.Id, ex.Message);
            }
        }
    }

    private async Task ProcessAssessmentAsync(Guid assessmentId, AssessmentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing assessment {AssessmentId}", assessmentId);

            // Update status to InProgress
            await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.InProgress.ToString());

            // 1. Collect Azure resources
            var resources = await _resourceGraphService.GetResourcesAsync(request.SubscriptionIds, cancellationToken);

            if (!resources.Any())
            {
                _logger.LogWarning("No resources found for assessment {AssessmentId}", assessmentId);
                await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Completed.ToString());
                return;
            }

            // 2. Run analyses based on assessment type
            NamingConventionResults? namingResults = null;
            TaggingResults? taggingResults = null;

            if (request.Type == AssessmentType.NamingConvention || request.Type == AssessmentType.Full)
            {
                namingResults = await _namingAnalyzer.AnalyzeNamingConventionsAsync(resources, cancellationToken);
            }

            if (request.Type == AssessmentType.Tagging || request.Type == AssessmentType.Full)
            {
                taggingResults = await _taggingAnalyzer.AnalyzeTaggingAsync(resources, cancellationToken);
            }

            // 3. Calculate overall score
            var overallScore = CalculateOverallScore(namingResults, taggingResults, request.Type);

            // 4. Save findings
            await SaveAssessmentFindings(assessmentId, namingResults, taggingResults);

            // 5. Complete assessment
            await _assessmentRepository.UpdateAssessmentAsync(assessmentId, overallScore, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

            _logger.LogInformation("Assessment {AssessmentId} completed successfully with score {Score}", assessmentId, overallScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process assessment {AssessmentId}", assessmentId);
            await MarkAssessmentAsFailed(assessmentId, ex.Message);
        }
    }

    private async Task SaveAssessmentFindings(Guid assessmentId, NamingConventionResults? namingResults, TaggingResults? taggingResults)
    {
        var findings = new List<AssessmentFinding>();

        // Save naming convention findings
        if (namingResults?.Violations != null)
        {
            foreach (var violation in namingResults.Violations)
            {
                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "NamingConvention",
                    ResourceId = violation.ResourceId,
                    ResourceName = violation.ResourceName,
                    ResourceType = violation.ResourceType,
                    Severity = violation.Severity,
                    Issue = violation.Issue,
                    Recommendation = $"Suggested name: {violation.SuggestedName}",
                    EstimatedEffort = "Low"
                });
            }
        }

        // Save tagging findings
        if (taggingResults?.Violations != null)
        {
            foreach (var violation in taggingResults.Violations)
            {
                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "Tagging",
                    ResourceId = violation.ResourceId,
                    ResourceName = violation.ResourceName,
                    ResourceType = violation.ResourceType,
                    Severity = violation.Severity,
                    Issue = violation.Issue,
                    Recommendation = violation.MissingTags.Any()
                        ? $"Add missing tags: {string.Join(", ", violation.MissingTags)}"
                        : "Review and fix tag values",
                    EstimatedEffort = violation.MissingTags.Count > 3 ? "High" : "Medium"
                });
            }
        }

        if (findings.Any())
        {
            await _assessmentRepository.CreateFindingsAsync(findings);
        }
    }

    private decimal CalculateOverallScore(NamingConventionResults? namingResults, TaggingResults? taggingResults, AssessmentType type)
    {
        var scores = new List<decimal>();

        if (type == AssessmentType.NamingConvention && namingResults != null)
        {
            return namingResults.Score;
        }

        if (type == AssessmentType.Tagging && taggingResults != null)
        {
            return taggingResults.Score;
        }

        if (type == AssessmentType.Full)
        {
            if (namingResults != null) scores.Add(namingResults.Score);
            if (taggingResults != null) scores.Add(taggingResults.Score);
        }

        return scores.Any() ? Math.Round(scores.Average(), 2) : 0m;
    }

    private async Task MarkAssessmentAsFailed(Guid assessmentId, string errorMessage)
    {
        await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Failed.ToString());
        // In a real implementation, you'd also save the error message
    }

    private AssessmentResult MapToAssessmentResult(Assessment assessment, List<AssessmentFinding> findings)
    {
        return new AssessmentResult
        {
            AssessmentId = assessment.Id,
            EnvironmentId = assessment.EnvironmentId,
            Type = Enum.Parse<AssessmentType>(assessment.AssessmentType),
            Status = Enum.Parse<AssessmentStatus>(assessment.Status),
            OverallScore = assessment.OverallScore ?? 0,
            StartedDate = assessment.StartedDate,
            CompletedDate = assessment.CompletedDate,
            TotalResourcesAnalyzed = findings.Select(f => f.ResourceId).Distinct().Count(),
            IssuesFound = findings.Count,
            Recommendations = GenerateRecommendations(findings)
        };
    }

    private List<AssessmentRecommendation> GenerateRecommendations(List<AssessmentFinding> findings)
    {
        var recommendations = new List<AssessmentRecommendation>();

        // Group findings by category and severity
        var namingIssues = findings.Where(f => f.Category == "NamingConvention").ToList();
        var taggingIssues = findings.Where(f => f.Category == "Tagging").ToList();

        if (namingIssues.Any())
        {
            recommendations.Add(new AssessmentRecommendation
            {
                Category = "NamingConvention",
                Title = "Establish Consistent Naming Conventions",
                Description = $"Found {namingIssues.Count} naming convention violations. Implementing consistent naming standards will improve resource organization and management.",
                Priority = namingIssues.Any(i => i.Severity == "High") ? "High" : "Medium",
                EstimatedEffort = namingIssues.Count > 20 ? "High" : "Medium",
                AffectedResources = namingIssues.Select(i => i.ResourceName).Distinct().ToList(),
                ActionPlan = "1. Define naming convention standards\n2. Create naming templates\n3. Rename non-compliant resources\n4. Implement governance policies"
            });
        }

        if (taggingIssues.Any())
        {
            recommendations.Add(new AssessmentRecommendation
            {
                Category = "Tagging",
                Title = "Implement Comprehensive Tagging Strategy",
                Description = $"Found {taggingIssues.Count} tagging violations. Proper resource tagging enables better cost management, automation, and governance.",
                Priority = taggingIssues.Any(i => i.Severity == "High") ? "High" : "Medium",
                EstimatedEffort = taggingIssues.Count > 50 ? "High" : "Medium",
                AffectedResources = taggingIssues.Select(i => i.ResourceName).Distinct().ToList(),
                ActionPlan = "1. Define required tag taxonomy\n2. Apply missing tags to resources\n3. Set up tagging policies\n4. Implement automated tagging"
            });
        }

        return recommendations;
    }
}