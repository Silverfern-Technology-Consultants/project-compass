using Compass.Core.Models;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface ITaggingAnalyzer : IPreferenceAwareTaggingAnalyzer
{
    // Keep existing method for backward compatibility
    Task<TaggingResults> AnalyzeTaggingAsync(List<AzureResource> resources, CancellationToken cancellationToken = default);
}

public class TaggingAnalyzer : ITaggingAnalyzer
{
    private readonly ILogger<TaggingAnalyzer> _logger;

    // Default required tags based on best practices
    private readonly string[] _commonRequiredTags =
    {
        "Environment",
        "Owner",
        "Project",
        "CostCenter",
        "Department"
    };

    // Tags that should have consistent values across resources
    private readonly string[] _consistencyTags =
    {
        "Environment",
        "Project",
        "Department",
        "Owner"
    };

    // Resource types that should always be tagged
    private readonly string[] _tagRequiredResourceTypes =
    {
        "microsoft.compute/virtualmachines",
        "microsoft.storage/storageaccounts",
        "microsoft.sql/servers",
        "microsoft.web/sites",
        "microsoft.keyvault/vaults",
        "microsoft.network/virtualnetworks"
    };

    public TaggingAnalyzer(ILogger<TaggingAnalyzer> logger)
    {
        _logger = logger;
    }

    // Standard analysis method (backward compatibility)
    public Task<TaggingResults> AnalyzeTaggingAsync(List<AzureResource> resources, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting standard tagging analysis for {ResourceCount} resources", resources.Count);
        var results = AnalyzeTaggingSync(resources, null);
        return Task.FromResult(results);
    }

    // Preference-aware analysis method (implements IPreferenceAwareTaggingAnalyzer)
    public Task<TaggingResults> AnalyzeTaggingAsync(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (clientConfig != null && clientConfig.HasTaggingPreferences)
        {
            _logger.LogInformation("Starting client-preference-aware tagging analysis for {ResourceCount} resources with client {ClientName}",
                resources.Count, clientConfig.ClientName);
            var results = AnalyzeWithClientPreferences(resources, clientConfig);
            return Task.FromResult(results);
        }

        _logger.LogInformation("Starting standard tagging analysis for {ResourceCount} resources", resources.Count);
        var standardResults = AnalyzeTaggingSync(resources, null);
        return Task.FromResult(standardResults);
    }

    // Client preference-aware analysis
    private TaggingResults AnalyzeWithClientPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        _logger.LogInformation("Running client-preference-aware tagging analysis for {ResourceCount} resources", resources.Count);

        var results = new TaggingResults
        {
            TotalResources = resources.Count,
            TaggedResources = resources.Count(r => r.HasTags)
        };

        results.TagCoveragePercentage = results.TotalResources > 0
            ? Math.Round((decimal)results.TaggedResources / results.TotalResources * 100, 2)
            : 100m;

        // Use client-specific required tags
        var clientRequiredTags = clientConfig.GetEffectiveRequiredTags();

        // Analyze tag usage frequency
        results.TagUsageFrequency = AnalyzeTagUsageFrequency(resources);

        // Find missing required tags based on client preferences
        results.MissingRequiredTags = FindMissingRequiredTags(resources, clientRequiredTags);

        // Find violations with client preference context
        results.Violations = FindTaggingViolationsWithPreferences(resources, clientConfig).ToList();

        // Calculate score based on client preferences
        results.Score = CalculatePreferenceBasedTaggingScore(results, clientConfig);

        _logger.LogInformation("Client-preference-aware tagging analysis completed. Score: {Score}%, Coverage: {Coverage}%, Client tags: {ClientTags}",
            results.Score, results.TagCoveragePercentage, string.Join(", ", clientRequiredTags));

        return results;
    }

    // Standard analysis method (synchronous)
    private TaggingResults AnalyzeTaggingSync(List<AzureResource> resources, ClientAssessmentConfiguration? clientConfig)
    {
        var results = new TaggingResults
        {
            TotalResources = resources.Count,
            TaggedResources = resources.Count(r => r.HasTags)
        };

        results.TagCoveragePercentage = results.TotalResources > 0
            ? Math.Round((decimal)results.TaggedResources / results.TotalResources * 100, 2)
            : 100m;

        // Analyze tag usage frequency
        results.TagUsageFrequency = AnalyzeTagUsageFrequency(resources);

        // Find missing required tags
        results.MissingRequiredTags = FindMissingRequiredTags(resources, _commonRequiredTags);

        // Find violations
        results.Violations = FindTaggingViolations(resources).ToList();

        // Calculate overall score
        results.Score = CalculateTaggingScore(results);

        _logger.LogInformation("Standard tagging analysis completed. Score: {Score}%, Coverage: {Coverage}%",
            results.Score, results.TagCoveragePercentage);

        return results;
    }

    private Dictionary<string, int> AnalyzeTagUsageFrequency(List<AzureResource> resources)
    {
        var tagFrequency = new Dictionary<string, int>();

        foreach (var resource in resources)
        {
            foreach (var tag in resource.Tags.Keys)
            {
                if (tagFrequency.ContainsKey(tag))
                {
                    tagFrequency[tag]++;
                }
                else
                {
                    tagFrequency[tag] = 1;
                }
            }
        }

        return tagFrequency.OrderByDescending(kvp => kvp.Value)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private List<string> FindMissingRequiredTags(List<AzureResource> resources, IEnumerable<string> requiredTags)
    {
        var missingTags = new List<string>();

        foreach (var requiredTag in requiredTags)
        {
            var resourcesWithTag = resources.Count(r =>
                r.Tags.Keys.Any(k => k.Equals(requiredTag, StringComparison.OrdinalIgnoreCase)));

            // If less than 30% of resources have this tag, consider it "missing"
            if (resourcesWithTag < resources.Count * 0.3)
            {
                missingTags.Add(requiredTag);
            }
        }

        return missingTags;
    }

    private IEnumerable<TaggingViolation> FindTaggingViolationsWithPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var clientRequiredTags = clientConfig.GetEffectiveRequiredTags();
        var enforcementStrict = clientConfig.EnforceTagCompliance;

        foreach (var resource in resources)
        {
            // Check if critical resource types are missing tags entirely
            if (ShouldRequireTags(resource.Type) && !resource.HasTags)
            {
                var severity = enforcementStrict ? "High" : "Medium";
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "NoTags",
                    Issue = "Critical resource type has no tags",
                    MissingTags = clientRequiredTags.ToList(),
                    Severity = severity
                };
                continue;
            }

            // Check for missing client-required tags
            var missingClientTags = new List<string>();
            foreach (var requiredTag in clientRequiredTags)
            {
                if (!resource.Tags.Keys.Any(k => k.Equals(requiredTag, StringComparison.OrdinalIgnoreCase)))
                {
                    missingClientTags.Add(requiredTag);
                }
            }

            if (missingClientTags.Any())
            {
                var severity = enforcementStrict ? "High" :
                              missingClientTags.Count > 2 ? "High" : "Medium";
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "MissingClientRequiredTags",
                    Issue = $"Missing client-required tags: {string.Join(", ", missingClientTags)}",
                    MissingTags = missingClientTags,
                    Severity = severity
                };
            }

            // Check for empty tag values
            var emptyTags = resource.Tags.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value)).ToList();
            if (emptyTags.Any())
            {
                var severity = enforcementStrict ? "High" : "Medium";
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "EmptyTagValues",
                    Issue = $"Tags with empty values: {string.Join(", ", emptyTags.Select(t => t.Key))}",
                    MissingTags = emptyTags.Select(t => t.Key).ToList(),
                    Severity = severity
                };
            }

            // Check for inconsistent tag naming (case sensitivity)
            var inconsistentTags = FindInconsistentTagNames(resource.Tags.Keys);
            if (inconsistentTags.Any())
            {
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "InconsistentTagNaming",
                    Issue = $"Inconsistent tag naming: {string.Join(", ", inconsistentTags)}",
                    MissingTags = inconsistentTags,
                    Severity = "Low"
                };
            }
        }
    }

    private IEnumerable<TaggingViolation> FindTaggingViolations(List<AzureResource> resources)
    {
        foreach (var resource in resources)
        {
            // Check if critical resource types are missing tags entirely
            if (ShouldRequireTags(resource.Type) && !resource.HasTags)
            {
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "NoTags",
                    Issue = "Critical resource type has no tags",
                    MissingTags = _commonRequiredTags.ToList(),
                    Severity = "High"
                };
                continue;
            }

            // Check for missing required tags
            var missingRequiredTags = new List<string>();
            foreach (var requiredTag in _commonRequiredTags)
            {
                if (!resource.Tags.Keys.Any(k => k.Equals(requiredTag, StringComparison.OrdinalIgnoreCase)))
                {
                    missingRequiredTags.Add(requiredTag);
                }
            }

            if (missingRequiredTags.Any())
            {
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "MissingRequiredTags",
                    Issue = $"Missing required tags: {string.Join(", ", missingRequiredTags)}",
                    MissingTags = missingRequiredTags,
                    Severity = missingRequiredTags.Count > 2 ? "High" : "Medium"
                };
            }

            // Check for empty tag values
            var emptyTags = resource.Tags.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value)).ToList();
            if (emptyTags.Any())
            {
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "EmptyTagValues",
                    Issue = $"Tags with empty values: {string.Join(", ", emptyTags.Select(t => t.Key))}",
                    MissingTags = emptyTags.Select(t => t.Key).ToList(),
                    Severity = "Medium"
                };
            }

            // Check for inconsistent tag naming (case sensitivity)
            var inconsistentTags = FindInconsistentTagNames(resource.Tags.Keys);
            if (inconsistentTags.Any())
            {
                yield return new TaggingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "InconsistentTagNaming",
                    Issue = $"Inconsistent tag naming: {string.Join(", ", inconsistentTags)}",
                    MissingTags = inconsistentTags,
                    Severity = "Low"
                };
            }
        }
    }

    private bool ShouldRequireTags(string resourceType)
    {
        return _tagRequiredResourceTypes.Contains(resourceType.ToLowerInvariant());
    }

    private List<string> FindInconsistentTagNames(IEnumerable<string> tagNames)
    {
        var inconsistent = new List<string>();
        var tagGroups = tagNames.GroupBy(t => t.ToLowerInvariant()).ToList();

        foreach (var group in tagGroups.Where(g => g.Count() > 1))
        {
            // If there are multiple variations of the same tag name (different cases)
            inconsistent.AddRange(group.Skip(1)); // Add all but the first one
        }

        return inconsistent;
    }

    private decimal CalculatePreferenceBasedTaggingScore(TaggingResults results, ClientAssessmentConfiguration clientConfig)
    {
        var scoringFactors = new List<decimal>();

        // Tag coverage (30% weight)
        scoringFactors.Add(results.TagCoveragePercentage * 0.3m);

        // Client-required tag coverage (40% weight)
        var clientRequiredTags = clientConfig.GetEffectiveRequiredTags();
        var clientTagCoverage = 100m;
        if (clientRequiredTags.Any())
        {
            clientTagCoverage = (decimal)(clientRequiredTags.Count() - results.MissingRequiredTags.Count) / clientRequiredTags.Count() * 100;
        }
        scoringFactors.Add(clientTagCoverage * 0.4m);

        // Violation penalty (30% weight)
        var violationPenalty = 0m;
        if (results.TotalResources > 0)
        {
            var highSeverityViolations = results.Violations.Count(v => v.Severity == "High");
            var mediumSeverityViolations = results.Violations.Count(v => v.Severity == "Medium");
            var lowSeverityViolations = results.Violations.Count(v => v.Severity == "Low");

            // Adjust penalty based on client enforcement level
            var penaltyMultiplier = clientConfig.EnforceTagCompliance ? 1.5m : 1.0m;
            violationPenalty = (highSeverityViolations * 10 + mediumSeverityViolations * 5 + lowSeverityViolations * 2) * penaltyMultiplier;
            violationPenalty = Math.Min(violationPenalty, 100); // Cap at 100%
        }
        scoringFactors.Add((100 - violationPenalty) * 0.3m);

        var finalScore = Math.Round(scoringFactors.Sum(), 2);
        _logger.LogInformation("Preference-based tagging score calculated: {Score}% (coverage: {Coverage}%, client compliance: {ClientCompliance}%)",
            finalScore, results.TagCoveragePercentage, clientTagCoverage);

        return finalScore;
    }

    private decimal CalculateTaggingScore(TaggingResults results)
    {
        var factors = new List<decimal>();

        // Tag coverage (40% weight)
        factors.Add(results.TagCoveragePercentage * 0.4m);

        // Required tag coverage (30% weight)
        var requiredTagCoverage = _commonRequiredTags.Length > 0
            ? (decimal)(_commonRequiredTags.Length - results.MissingRequiredTags.Count) / _commonRequiredTags.Length * 100
            : 100m;
        factors.Add(requiredTagCoverage * 0.3m);

        // Violation penalty (30% weight)
        var violationPenalty = 0m;
        if (results.TotalResources > 0)
        {
            var highSeverityViolations = results.Violations.Count(v => v.Severity == "High");
            var mediumSeverityViolations = results.Violations.Count(v => v.Severity == "Medium");
            var lowSeverityViolations = results.Violations.Count(v => v.Severity == "Low");

            violationPenalty = (highSeverityViolations * 10 + mediumSeverityViolations * 5 + lowSeverityViolations * 2);
            violationPenalty = Math.Min(violationPenalty, 100); // Cap at 100%
        }
        factors.Add((100 - violationPenalty) * 0.3m);

        return Math.Round(factors.Sum(), 2);
    }
}