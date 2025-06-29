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
    private readonly IDependencyAnalyzer _dependencyAnalyzer;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly ILogger<AssessmentOrchestrator> _logger;

    public AssessmentOrchestrator(
        IAzureResourceGraphService resourceGraphService,
        INamingConventionAnalyzer namingAnalyzer,
        ITaggingAnalyzer taggingAnalyzer,
        IDependencyAnalyzer dependencyAnalyzer,
        IAssessmentRepository assessmentRepository,
        ILogger<AssessmentOrchestrator> logger)
    {
        _resourceGraphService = resourceGraphService;
        _namingAnalyzer = namingAnalyzer;
        _taggingAnalyzer = taggingAnalyzer;
        _dependencyAnalyzer = dependencyAnalyzer;
        _assessmentRepository = assessmentRepository;
        _logger = logger;
    }

    public async Task<Guid> StartAssessmentAsync(AssessmentRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting enhanced assessment for organization {OrganizationId}, environment {EnvironmentId} with {SubscriptionCount} subscriptions",
            request.OrganizationId, request.EnvironmentId, request.SubscriptionIds.Length);

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            EnvironmentId = request.EnvironmentId,
            Name = request.Name,
            AssessmentType = request.Type.ToString(),
            Status = AssessmentStatus.Pending.ToString(),
            StartedDate = DateTime.UtcNow,
            CustomerId = request.CustomerId,
            OrganizationId = request.OrganizationId, // ✅ NEW: Set organization ID
            CustomerName = "Organization Member" // Will be updated with actual customer name from context
        };

        await _assessmentRepository.CreateAsync(assessment);

        // Process the assessment synchronously within the HTTP request scope
        try
        {
            await ProcessAssessmentAsync(assessment.Id, request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process assessment {AssessmentId} for organization {OrganizationId}",
                assessment.Id, request.OrganizationId);
            await MarkAssessmentAsFailed(assessment.Id, ex.Message);
        }

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
        // Background processing is disabled for now to avoid DbContext scope issues
        _logger.LogDebug("Background processing is disabled - assessments are processed synchronously");
        await Task.CompletedTask;
    }

    private async Task ProcessAssessmentAsync(Guid assessmentId, AssessmentRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing enhanced assessment {AssessmentId} with subscriptions: {Subscriptions}",
            assessmentId, string.Join(",", request.SubscriptionIds));

        // Update status to InProgress
        await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.InProgress.ToString());

        // 1. Collect Azure resources using the REAL subscription IDs from the request
        var resources = await _resourceGraphService.GetResourcesAsync(request.SubscriptionIds, cancellationToken);

        if (!resources.Any())
        {
            _logger.LogWarning("No resources found for assessment {AssessmentId}", assessmentId);
            await _assessmentRepository.UpdateAssessmentAsync(assessmentId, 100m, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);
            return;
        }

        _logger.LogInformation("Found {ResourceCount} resources for enhanced assessment {AssessmentId}", resources.Count, assessmentId);

        // NEW: Save resources to database for historical data
        await SaveAssessmentResourcesAsync(assessmentId, resources);

        // 2. Run enhanced analyses based on assessment type
        NamingConventionResults? namingResults = null;
        TaggingResults? taggingResults = null;
        DependencyAnalysisResults? dependencyResults = null;

        if (request.Type == AssessmentType.NamingConvention || request.Type == AssessmentType.Full)
        {
            _logger.LogInformation("Running enhanced naming convention analysis...");
            namingResults = await _namingAnalyzer.AnalyzeNamingConventionsAsync(resources, cancellationToken);
            _logger.LogInformation("Enhanced naming analysis completed. Score: {Score}%, Pattern Distribution: {PatternCount} patterns detected",
                namingResults.Score, namingResults.PatternDistribution.Count);
        }

        if (request.Type == AssessmentType.Tagging || request.Type == AssessmentType.Full)
        {
            _logger.LogInformation("Running enhanced tagging analysis...");
            taggingResults = await _taggingAnalyzer.AnalyzeTaggingAsync(resources, cancellationToken);
            _logger.LogInformation("Enhanced tagging analysis completed. Score: {Score}%, Coverage: {Coverage}%",
                taggingResults.Score, taggingResults.TagCoveragePercentage);
        }

        // 3. Always run dependency analysis for comprehensive insights
        _logger.LogInformation("Running dependency analysis...");
        dependencyResults = await _dependencyAnalyzer.AnalyzeDependenciesAsync(resources, cancellationToken);
        _logger.LogInformation("Dependency analysis completed. Found {VMDeps} VM dependencies, {NetworkDeps} network dependencies, {RGCount} resource groups",
            dependencyResults.VirtualMachineDependencies.Count,
            dependencyResults.NetworkDependencies.Count,
            dependencyResults.ResourceGroupAnalysis.TotalResourceGroups);

        // 4. Calculate overall score
        var overallScore = CalculateOverallScore(namingResults, taggingResults, request.Type);

        // 5. Save enhanced findings
        await SaveEnhancedAssessmentFindings(assessmentId, namingResults, taggingResults, dependencyResults);

        // 6. Complete assessment
        await _assessmentRepository.UpdateAssessmentAsync(assessmentId, overallScore, AssessmentStatus.Completed.ToString(), DateTime.UtcNow);

        _logger.LogInformation("Enhanced assessment {AssessmentId} completed successfully with score {Score}%. Analyzed {ResourceCount} resources with detailed dependency mapping",
            assessmentId, overallScore, resources.Count);
    }

    private async Task SaveEnhancedAssessmentFindings(
        Guid assessmentId,
        NamingConventionResults? namingResults,
        TaggingResults? taggingResults,
        DependencyAnalysisResults? dependencyResults)
    {
        var findings = new List<AssessmentFinding>();

        // Save enhanced naming convention findings
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
                    EstimatedEffort = GetEffortEstimate(violation.ViolationType)
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

        // Save dependency analysis findings
        if (dependencyResults != null)
        {
            // Environment separation issues
            foreach (var issue in dependencyResults.EnvironmentSeparation.MixedEnvironmentNetworks)
            {
                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "EnvironmentSeparation",
                    ResourceId = issue.NetworkName,
                    ResourceName = issue.NetworkName,
                    ResourceType = "microsoft.network/virtualnetworks",
                    Severity = issue.RiskLevel,
                    Issue = $"Network contains mixed environments: {string.Join(", ", issue.DetectedEnvironments)}. This poses security and isolation risks.",
                    Recommendation = "Separate development and production resources into different virtual networks to improve security isolation.",
                    EstimatedEffort = "High"
                });
            }

            // Resource group organization issues
            var inconsistentRGs = dependencyResults.ResourceGroupAnalysis.ResourceGroups
                .Where(rg => rg.NamingPatterns.Count > 2) // More than 2 different naming patterns
                .ToList();

            foreach (var rg in inconsistentRGs)
            {
                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "ResourceGroupOrganization",
                    ResourceId = rg.ResourceGroupName,
                    ResourceName = rg.ResourceGroupName,
                    ResourceType = "microsoft.resources/resourcegroups",
                    Severity = "Medium",
                    Issue = $"Resource group has inconsistent naming patterns: {string.Join(", ", rg.NamingPatterns.Keys)}",
                    Recommendation = "Standardize naming conventions within resource groups for better organization.",
                    EstimatedEffort = "Medium"
                });
            }

            // Complex dependency chains (VMs with many dependencies)
            var complexVMs = dependencyResults.VirtualMachineDependencies
                .Where(vm => vm.NetworkInterfaces.Count + vm.PublicIPs.Count + vm.NetworkSecurityGroups.Count > 5)
                .ToList();

            foreach (var vm in complexVMs)
            {
                findings.Add(new AssessmentFinding
                {
                    Id = Guid.NewGuid(),
                    AssessmentId = assessmentId,
                    Category = "ComplexDependencies",
                    ResourceId = vm.VirtualMachine.Id,
                    ResourceName = vm.VirtualMachine.Name,
                    ResourceType = vm.VirtualMachine.Type,
                    Severity = "Low",
                    Issue = "Virtual machine has complex dependency chain that may complicate management and migration.",
                    Recommendation = "Review VM dependencies and consider simplifying the network configuration.",
                    EstimatedEffort = "Medium"
                });
            }
        }

        if (findings.Any())
        {
            _logger.LogInformation("Saving {FindingCount} enhanced findings for assessment {AssessmentId}", findings.Count, assessmentId);
            await _assessmentRepository.CreateFindingsAsync(findings);
        }
    }
    private async Task SaveAssessmentResourcesAsync(Guid assessmentId, List<AzureResource> resources)
    {
        _logger.LogInformation("Saving {ResourceCount} resources for assessment {AssessmentId}", resources.Count, assessmentId);

        var assessmentResources = resources.Select(r => new AssessmentResource
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            ResourceId = r.Id,
            Name = r.Name,
            Type = r.Type,
            ResourceTypeName = r.ResourceTypeName,
            ResourceGroup = r.ResourceGroup,
            Location = r.Location,
            SubscriptionId = r.SubscriptionId,
            Kind = r.Kind,
            Sku = r.Sku,
            Tags = System.Text.Json.JsonSerializer.Serialize(r.Tags),
            TagCount = r.TagCount,
            Environment = r.Environment,
            Properties = r.Properties,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _assessmentRepository.CreateResourcesAsync(assessmentResources);

        _logger.LogInformation("Successfully saved {ResourceCount} resources for assessment {AssessmentId}",
            assessmentResources.Count, assessmentId);
    }
    private string GetEffortEstimate(string violationType)
    {
        return violationType switch
        {
            "InvalidCharacters" => "High",
            "MissingResourceTypePrefix" => "Medium",
            "NameTooLong" => "Medium",
            "InconsistentPattern" => "Low",
            _ => "Medium"
        };
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
        try
        {
            await _assessmentRepository.UpdateStatusAsync(assessmentId, AssessmentStatus.Failed.ToString());
            _logger.LogInformation("Marked assessment {AssessmentId} as failed: {Error}", assessmentId, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark assessment {AssessmentId} as failed", assessmentId);
        }
    }

    private AssessmentResult MapToAssessmentResult(Assessment assessment, List<AssessmentFinding> findings)
    {
        var result = new AssessmentResult
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
            Recommendations = GenerateEnhancedRecommendations(findings)
        };

        // Add detailed metrics
        result.DetailedMetrics = GenerateDetailedMetrics(findings);

        return result;
    }

    private List<AssessmentRecommendation> GenerateEnhancedRecommendations(List<AssessmentFinding> findings)
    {
        var recommendations = new List<AssessmentRecommendation>();

        // Group findings by category
        var findingsByCategory = findings.GroupBy(f => f.Category).ToList();

        foreach (var categoryGroup in findingsByCategory)
        {
            var category = categoryGroup.Key;
            var categoryFindings = categoryGroup.ToList();
            var highSeverityCount = categoryFindings.Count(f => f.Severity == "High");
            var totalCount = categoryFindings.Count;

            var recommendation = category switch
            {
                "NamingConvention" => new AssessmentRecommendation
                {
                    Category = "NamingConvention",
                    Title = "Standardize Naming Conventions",
                    Description = $"Found {totalCount} naming convention violations across {categoryFindings.Select(f => f.ResourceType).Distinct().Count()} resource types. Consistent naming improves resource discoverability and management automation.",
                    Priority = highSeverityCount > 0 ? "High" : totalCount > 10 ? "Medium" : "Low",
                    EstimatedEffort = totalCount > 20 ? "High" : totalCount > 5 ? "Medium" : "Low",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList(),
                    ActionPlan = GenerateNamingActionPlan(categoryFindings)
                },

                "Tagging" => new AssessmentRecommendation
                {
                    Category = "Tagging",
                    Title = "Implement Comprehensive Tagging Strategy",
                    Description = $"Found {totalCount} tagging violations. Proper resource tagging enables cost allocation, automation, and governance.",
                    Priority = highSeverityCount > 0 ? "High" : "Medium",
                    EstimatedEffort = totalCount > 50 ? "High" : totalCount > 20 ? "Medium" : "Low",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList(),
                    ActionPlan = GenerateTaggingActionPlan(categoryFindings)
                },

                "EnvironmentSeparation" => new AssessmentRecommendation
                {
                    Category = "EnvironmentSeparation",
                    Title = "Improve Environment Isolation",
                    Description = $"Found {totalCount} environment separation issues. Proper isolation reduces security risks and prevents accidental cross-environment access.",
                    Priority = "High",
                    EstimatedEffort = "High",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().ToList(),
                    ActionPlan = "1. Separate development and production resources\n2. Implement network isolation\n3. Review access controls\n4. Establish clear environment boundaries"
                },

                "ResourceGroupOrganization" => new AssessmentRecommendation
                {
                    Category = "ResourceGroupOrganization",
                    Title = "Reorganize Resource Groups",
                    Description = $"Found {totalCount} resource group organization issues. Well-organized resource groups improve management and governance.",
                    Priority = "Medium",
                    EstimatedEffort = "Medium",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().ToList(),
                    ActionPlan = "1. Define resource group strategy\n2. Group related resources\n3. Apply consistent naming\n4. Implement governance policies"
                },

                "ComplexDependencies" => new AssessmentRecommendation
                {
                    Category = "ComplexDependencies",
                    Title = "Simplify Resource Dependencies",
                    Description = $"Found {totalCount} resources with complex dependency chains. Simplified dependencies improve reliability and reduce management overhead.",
                    Priority = "Low",
                    EstimatedEffort = "Medium",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().ToList(),
                    ActionPlan = "1. Review dependency chains\n2. Consolidate where possible\n3. Document critical dependencies\n4. Plan for disaster recovery"
                },

                _ => new AssessmentRecommendation
                {
                    Category = category,
                    Title = $"Address {category} Issues",
                    Description = $"Found {totalCount} issues in category {category}.",
                    Priority = highSeverityCount > 0 ? "High" : "Medium",
                    EstimatedEffort = "Medium",
                    AffectedResources = categoryFindings.Select(f => f.ResourceName).Distinct().Take(10).ToList(),
                    ActionPlan = "Review findings and implement appropriate remediation steps."
                }
            };

            recommendations.Add(recommendation);
        }

        return recommendations;
    }

    private string GenerateNamingActionPlan(List<AssessmentFinding> namingFindings)
    {
        var violationTypes = namingFindings.GroupBy(f => GetViolationTypeFromIssue(f.Issue)).ToList();
        var actionPlan = "1. Establish naming convention standards\n";

        if (violationTypes.Any(vt => vt.Key.Contains("prefix")))
            actionPlan += "2. Implement resource type prefixes (vm-, st-, vnet-, etc.)\n";

        if (violationTypes.Any(vt => vt.Key.Contains("pattern")))
            actionPlan += "3. Standardize on consistent naming pattern (kebab-case recommended)\n";

        if (violationTypes.Any(vt => vt.Key.Contains("character")))
            actionPlan += "4. Remove invalid characters from resource names\n";

        actionPlan += "5. Create naming templates for each resource type\n";
        actionPlan += "6. Implement Azure Policy to enforce standards\n";
        actionPlan += "7. Plan phased rename of non-compliant resources";

        return actionPlan;
    }

    private string GenerateTaggingActionPlan(List<AssessmentFinding> taggingFindings)
    {
        var actionPlan = "1. Define mandatory tag taxonomy (Environment, Owner, Project, CostCenter)\n";
        actionPlan += "2. Apply missing tags to existing resources\n";
        actionPlan += "3. Implement Azure Policy for tag enforcement\n";
        actionPlan += "4. Set up automated tagging for new resources\n";
        actionPlan += "5. Create cost allocation reports using tags\n";
        actionPlan += "6. Establish tag governance process";

        return actionPlan;
    }

    private string GetViolationTypeFromIssue(string issue)
    {
        return issue.ToLowerInvariant() switch
        {
            var i when i.Contains("prefix") => "prefix",
            var i when i.Contains("pattern") => "pattern",
            var i when i.Contains("character") => "character",
            var i when i.Contains("length") => "length",
            _ => "other"
        };
    }

    private Dictionary<string, object> GenerateDetailedMetrics(List<AssessmentFinding> findings)
    {
        var metrics = new Dictionary<string, object>();

        // Category distribution
        var categoryStats = findings.GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["CategoryDistribution"] = categoryStats;

        // Severity distribution
        var severityStats = findings.GroupBy(f => f.Severity)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["SeverityDistribution"] = severityStats;

        // Resource type distribution
        var resourceTypeStats = findings.GroupBy(f => f.ResourceType)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["ResourceTypeDistribution"] = resourceTypeStats;

        // Top issues
        var topIssues = findings.GroupBy(f => GetViolationTypeFromIssue(f.Issue))
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToDictionary(g => g.Key, g => g.Count());
        metrics["TopIssueTypes"] = topIssues;

        return metrics;
    }
}