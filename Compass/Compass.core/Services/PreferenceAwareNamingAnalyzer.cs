using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Compass.Core.Services;

public interface IPreferenceAwareNamingAnalyzer
{
    Task<NamingConventionResults> AnalyzeNamingConventionsAsync(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig = null,
        CancellationToken cancellationToken = default);
}

public class PreferenceAwareNamingAnalyzer : IPreferenceAwareNamingAnalyzer
{
    private readonly ILogger<PreferenceAwareNamingAnalyzer> _logger;

    // Default Azure resource type prefixes (fallback when client has no preferences)
    private readonly Dictionary<string, string[]> _defaultResourceTypePrefixes = new()
    {
        { "microsoft.compute/virtualmachines", new[] { "vm", "vm-" } },
        { "microsoft.storage/storageaccounts", new[] { "st", "stor", "storage" } },
        { "microsoft.network/virtualnetworks", new[] { "vnet", "vnet-" } },
        { "microsoft.network/networksecuritygroups", new[] { "nsg", "nsg-" } },
        { "microsoft.sql/servers", new[] { "sql", "sqlsrv", "sql-" } },
        { "microsoft.web/sites", new[] { "app", "web", "app-", "web-" } }
    };

    public PreferenceAwareNamingAnalyzer(ILogger<PreferenceAwareNamingAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<NamingConventionResults> AnalyzeNamingConventionsAsync(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting client-preference-aware naming analysis for {ResourceCount} resources", resources.Count);

        if (clientConfig != null)
        {
            _logger.LogInformation("Using client-specific preferences: Allowed patterns: {Patterns}, Required elements: {Elements}",
                string.Join(", ", clientConfig.AllowedNamingPatterns),
                string.Join(", ", clientConfig.RequiredNamingElements));
        }

        var results = new NamingConventionResults
        {
            TotalResources = resources.Count
        };

        // Enhanced pattern analysis with client preferences
        results.PatternDistribution = AnalyzePatternDistribution(resources, clientConfig);
        results.PatternsByResourceType = AnalyzePatternsByResourceType(resources, clientConfig);
        results.EnvironmentIndicators = AnalyzeEnvironmentIndicators(resources, clientConfig);
        results.Consistency = AnalyzeConsistencyAgainstPreferences(resources, clientConfig);
        results.Violations = FindNamingViolationsAgainstPreferences(resources, clientConfig).ToList();
        results.RepresentativeExamples = GenerateRepresentativeExamples(resources);

        // Calculate compliance score based on client preferences
        results.CompliantResources = results.TotalResources - results.Violations.Count;
        results.Score = CalculatePreferenceBasedScore(results, clientConfig);

        _logger.LogInformation("Client-preference-aware naming analysis completed. Score: {Score}%, Violations: {ViolationCount}",
            results.Score, results.Violations.Count);

        return Task.FromResult(results);
    }

    private Dictionary<string, NamingPatternStats> AnalyzePatternDistribution(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig)
    {
        var patternStats = new Dictionary<string, NamingPatternStats>
        {
            ["Lowercase"] = new NamingPatternStats { Pattern = "Lowercase", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["Uppercase"] = new NamingPatternStats { Pattern = "Uppercase", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["CamelCase"] = new NamingPatternStats { Pattern = "CamelCase", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["PascalCase"] = new NamingPatternStats { Pattern = "PascalCase", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["Snake_case"] = new NamingPatternStats { Pattern = "Snake_case", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["Kebab-case"] = new NamingPatternStats { Pattern = "Kebab-case", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["UUID"] = new NamingPatternStats { Pattern = "UUID", Count = 0, Percentage = 0m, Examples = new List<string>() },
            ["Other"] = new NamingPatternStats { Pattern = "Other", Count = 0, Percentage = 0m, Examples = new List<string>() }
        };

        foreach (var resource in resources)
        {
            var pattern = ClassifyNamingPattern(resource.Name);
            patternStats[pattern].Count++;
            patternStats[pattern].Examples.Add(resource.Name);
        }

        // Calculate percentages and mark compliance based on client preferences
        foreach (var stat in patternStats.Values)
        {
            stat.Percentage = resources.Count > 0
                ? Math.Round((decimal)stat.Count / resources.Count * 100, 1)
                : 0m;

            // Mark as compliant if it matches client preferences
            if (clientConfig?.AllowedNamingPatterns.Contains(stat.Pattern) == true)
            {
                // Note: IsCompliantWithPreferences property doesn't exist in the base model
                // We'll track this in the scoring logic instead
            }

            stat.Examples = stat.Examples.Take(3).ToList();
        }

        return patternStats;
    }

    private Dictionary<string, NamingPatternAnalysis> AnalyzePatternsByResourceType(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig)
    {
        var resourcesByType = resources.GroupBy(r => r.Type.ToLowerInvariant()).ToList();
        var patternsByType = new Dictionary<string, NamingPatternAnalysis>();

        foreach (var group in resourcesByType)
        {
            var resourceType = group.Key;
            var typeResources = group.ToList();

            var patterns = new Dictionary<string, int>();
            foreach (var resource in typeResources)
            {
                var pattern = ClassifyNamingPattern(resource.Name);
                patterns[pattern] = patterns.GetValueOrDefault(pattern, 0) + 1;
            }

            var mostCommonPattern = patterns.Any()
                ? patterns.OrderByDescending(kvp => kvp.Value).First()
                : new KeyValuePair<string, int>("Other", 0);

            // Check if most common pattern aligns with client preferences
            var isPreferenceCompliant = clientConfig?.AllowedNamingPatterns.Contains(mostCommonPattern.Key) ?? false;

            var consistencyScore = typeResources.Count > 0
                ? (decimal)mostCommonPattern.Value / typeResources.Count * 100
                : 100m;

            // Adjust score based on client preference compliance
            if (clientConfig != null && !isPreferenceCompliant)
            {
                consistencyScore *= 0.5m; // Reduce score if not matching client preferences
            }

            patternsByType[resourceType] = new NamingPatternAnalysis
            {
                ResourceType = resourceType,
                TotalResources = typeResources.Count,
                MostCommonPattern = mostCommonPattern.Key,
                PatternCompliantResources = mostCommonPattern.Value,
                ConsistencyScore = Math.Round(consistencyScore, 2),
                DetectedPatterns = patterns.Keys.ToList(),
                PatternDistribution = patterns
                // Note: IsAlignedWithClientPreferences property doesn't exist in the base model
            };
        }

        return patternsByType;
    }

    private EnvironmentIndicatorAnalysis AnalyzeEnvironmentIndicators(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig)
    {
        var analysis = new EnvironmentIndicatorAnalysis();

        // Use client-specific environment indicators if available
        var environmentIndicators = clientConfig?.EnvironmentIndicators == true
            ? new List<string> { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat" }
            : new List<string> { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat" };

        foreach (var resource in resources)
        {
            var detectedEnv = DetectEnvironmentFromName(resource.Name, environmentIndicators);
            if (!string.IsNullOrEmpty(detectedEnv))
            {
                analysis.ResourcesWithEnvironmentIndicators++;
                analysis.EnvironmentDistribution[detectedEnv] =
                    analysis.EnvironmentDistribution.GetValueOrDefault(detectedEnv, 0) + 1;
            }
        }

        analysis.PercentageWithEnvironmentIndicators = resources.Count > 0
            ? Math.Round((decimal)analysis.ResourcesWithEnvironmentIndicators / resources.Count * 100, 2)
            : 0m;

        // Check if this meets client requirements
        if (clientConfig?.RequiredNamingElements.Contains("Environment indicator") == true)
        {
            analysis.MeetsClientRequirements = analysis.PercentageWithEnvironmentIndicators >= 80; // 80% threshold
        }

        return analysis;
    }

    private NamingConsistencyMetrics AnalyzeConsistencyAgainstPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig)
    {
        var metrics = new NamingConsistencyMetrics();

        // Analyze pattern consistency against client preferences
        var patternDistribution = AnalyzePatternDistribution(resources, clientConfig);

        if (clientConfig?.AllowedNamingPatterns.Any() == true)
        {
            // Calculate compliance with client's preferred patterns
            var compliantResources = 0;
            foreach (var resource in resources)
            {
                var pattern = ClassifyNamingPattern(resource.Name);
                if (clientConfig.AllowedNamingPatterns.Contains(pattern))
                {
                    compliantResources++;
                }
            }

            metrics.ClientPreferenceCompliance = resources.Count > 0
                ? Math.Round((decimal)compliantResources / resources.Count * 100, 2)
                : 100m;

            metrics.OverallConsistency = metrics.ClientPreferenceCompliance;
        }
        else
        {
            // Fallback to standard consistency analysis
            var topPattern = patternDistribution.Values.OrderByDescending(p => p.Count).FirstOrDefault();
            metrics.OverallConsistency = topPattern?.Percentage ?? 0m;
        }

        // Environment prefix analysis based on client requirements
        if (clientConfig?.RequiredNamingElements.Contains("Environment indicator") == true)
        {
            var envAnalysis = AnalyzeEnvironmentIndicators(resources, clientConfig);
            metrics.UsesEnvironmentPrefixes = envAnalysis.PercentageWithEnvironmentIndicators > 50;
        }

        return metrics;
    }

    private IEnumerable<NamingViolation> FindNamingViolationsAgainstPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig)
    {
        foreach (var resource in resources)
        {
            // Check against client's preferred naming patterns
            if (clientConfig?.AllowedNamingPatterns.Any() == true)
            {
                var resourcePattern = ClassifyNamingPattern(resource.Name);
                if (!clientConfig.AllowedNamingPatterns.Contains(resourcePattern))
                {
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "ClientPreferenceViolation",
                        Issue = $"Resource naming pattern '{resourcePattern}' doesn't match client's preferred patterns: {string.Join(", ", clientConfig.AllowedNamingPatterns)}",
                        SuggestedName = ConvertToClientPreferredPattern(resource.Name, clientConfig.AllowedNamingPatterns.First()),
                        Severity = "Medium"
                    };
                }
            }

            // Check for required naming elements
            if (clientConfig?.RequiredNamingElements.Contains("Environment indicator") == true)
            {
                var environmentIndicators = clientConfig.EnvironmentIndicators == true
                    ? new List<string> { "dev", "test", "prod", "staging" }
                    : new List<string> { "dev", "test", "prod", "staging" };

                var hasEnvironmentIndicator = environmentIndicators.Any(env =>
                    resource.Name.ToLowerInvariant().Contains(env));

                if (!hasEnvironmentIndicator)
                {
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "MissingRequiredElement",
                        Issue = "Resource name doesn't include required environment indicator as specified in client preferences",
                        SuggestedName = $"{resource.Name}-env",
                        Severity = "High"
                    };
                }
            }

            // Standard violations (invalid characters, length, etc.)
            if (Regex.IsMatch(resource.Name, @"[^a-zA-Z0-9\-_\.]"))
            {
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "InvalidCharacters",
                    Issue = "Resource name contains invalid characters",
                    SuggestedName = Regex.Replace(resource.Name, @"[^a-zA-Z0-9\-_\.]", ""),
                    Severity = "High"
                };
            }

            if (resource.Name.Length > 63)
            {
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "NameTooLong",
                    Issue = "Resource name exceeds maximum length of 63 characters",
                    SuggestedName = resource.Name.Substring(0, 60) + "...",
                    Severity = "Medium"
                };
            }
        }
    }

    private decimal CalculatePreferenceBasedScore(NamingConventionResults results, ClientAssessmentConfiguration? clientConfig)
    {
        if (clientConfig == null)
        {
            // Fallback to standard scoring
            return results.TotalResources > 0
                ? Math.Round((decimal)results.CompliantResources / results.TotalResources * 100, 2)
                : 100m;
        }

        var scoringFactors = new List<decimal>();

        // Client preference compliance (40% weight)
        if (results.Consistency.ClientPreferenceCompliance > 0)
        {
            scoringFactors.Add(results.Consistency.ClientPreferenceCompliance * 0.4m);
        }

        // Required elements compliance (30% weight)
        if (clientConfig.RequiredNamingElements.Contains("Environment indicator"))
        {
            var envCompliance = results.EnvironmentIndicators.MeetsClientRequirements ? 100m :
                results.EnvironmentIndicators.PercentageWithEnvironmentIndicators;
            scoringFactors.Add(envCompliance * 0.3m);
        }

        // Standard violations penalty (30% weight)
        var standardViolations = results.Violations.Count(v =>
            v.ViolationType == "InvalidCharacters" || v.ViolationType == "NameTooLong");
        var violationPenalty = results.TotalResources > 0
            ? (decimal)standardViolations / results.TotalResources * 100
            : 0m;
        scoringFactors.Add((100 - violationPenalty) * 0.3m);

        return scoringFactors.Any() ? Math.Round(scoringFactors.Sum(), 2) : 0m;
    }

    // Helper methods
    private string ClassifyNamingPattern(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Other";

        if (Regex.IsMatch(name, @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"))
            return "UUID";

        if (name.Contains("_")) return "Snake_case";
        if (name.Contains("-")) return "Kebab-case";
        if (name.All(c => !char.IsLetter(c) || char.IsUpper(c))) return "Uppercase";
        if (name.All(c => !char.IsLetter(c) || char.IsLower(c))) return "Lowercase";
        if (char.IsUpper(name[0]) && name.Any(char.IsUpper)) return "PascalCase";
        if (char.IsLower(name[0]) && name.Any(char.IsUpper)) return "CamelCase";

        return "Other";
    }

    private string? DetectEnvironmentFromName(string name, List<string> environmentIndicators)
    {
        var nameLower = name.ToLowerInvariant();
        return environmentIndicators.FirstOrDefault(env => nameLower.Contains(env));
    }

    private string ConvertToClientPreferredPattern(string name, string targetPattern)
    {
        return targetPattern.ToLowerInvariant() switch
        {
            "lowercase" => name.ToLowerInvariant(),
            "uppercase" => name.ToUpperInvariant(),
            "kebab-case" => Regex.Replace(name, @"[_\s]+", "-").ToLowerInvariant(),
            "snake_case" => Regex.Replace(name, @"[-\s]+", "_").ToLowerInvariant(),
            "camelcase" => ConvertToCamelCase(name),
            _ => name.ToLowerInvariant()
        };
    }

    private string ConvertToCamelCase(string name)
    {
        var words = Regex.Split(name, @"[-_\s]+");
        if (!words.Any()) return name;

        var result = words[0].ToLowerInvariant();
        for (int i = 1; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                result += char.ToUpperInvariant(words[i][0]) + words[i].Substring(1).ToLowerInvariant();
            }
        }
        return result;
    }

    private Dictionary<string, List<string>> GenerateRepresentativeExamples(List<AzureResource> resources)
    {
        var examples = new Dictionary<string, List<string>>();

        foreach (var resource in resources.Take(50))
        {
            var pattern = ClassifyNamingPattern(resource.Name);
            if (!examples.ContainsKey(pattern))
            {
                examples[pattern] = new List<string>();
            }

            if (examples[pattern].Count < 3)
            {
                examples[pattern].Add(resource.Name);
            }
        }

        return examples;
    }
}