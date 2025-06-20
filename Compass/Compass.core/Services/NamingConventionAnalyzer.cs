using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Compass.Core.Services;

public interface INamingConventionAnalyzer
{
    Task<NamingConventionResults> AnalyzeNamingConventionsAsync(List<AzureResource> resources, CancellationToken cancellationToken = default);
}

public class NamingConventionAnalyzer : INamingConventionAnalyzer
{
    private readonly ILogger<NamingConventionAnalyzer> _logger;

    // Common Azure resource type prefixes based on Microsoft recommendations
    private readonly Dictionary<string, string[]> _resourceTypePrefixes = new()
    {
        // Compute
        { "microsoft.compute/virtualmachines", new[] { "vm", "vm-" } },
        { "microsoft.compute/availabilitysets", new[] { "avail", "as", "as-" } },
        { "microsoft.compute/virtualmachinescalesets", new[] { "vmss", "vmss-" } },
        { "microsoft.compute/disks", new[] { "disk", "disk-" } },
        
        // Storage
        { "microsoft.storage/storageaccounts", new[] { "st", "stor", "storage" } },
        
        // Networking
        { "microsoft.network/virtualnetworks", new[] { "vnet", "vnet-" } },
        { "microsoft.network/subnets", new[] { "snet", "subnet", "snet-" } },
        { "microsoft.network/networkinterfaces", new[] { "nic", "nic-" } },
        { "microsoft.network/networksecuritygroups", new[] { "nsg", "nsg-" } },
        { "microsoft.network/publicipaddresses", new[] { "pip", "ip", "pip-" } },
        { "microsoft.network/loadbalancers", new[] { "lb", "lbe", "lbi", "lb-" } },
        { "microsoft.network/applicationgateways", new[] { "agw", "appgw", "agw-" } },
        
        // Web
        { "microsoft.web/sites", new[] { "app", "web", "app-", "web-" } },
        { "microsoft.web/serverfarms", new[] { "plan", "asp", "plan-", "asp-" } },
        
        // Database
        { "microsoft.sql/servers", new[] { "sql", "sqlsrv", "sql-" } },
        { "microsoft.sql/servers/databases", new[] { "sqldb", "db", "sqldb-" } },
        { "microsoft.documentdb/databaseaccounts", new[] { "cosmos", "cdb", "cosmos-" } },
        
        // Management
        { "microsoft.resources/resourcegroups", new[] { "rg", "rg-" } },
        { "microsoft.keyvault/vaults", new[] { "kv", "vault", "kv-" } },
        
        // Container
        { "microsoft.containerregistry/registries", new[] { "cr", "acr", "cr-" } },
        { "microsoft.containerservice/managedclusters", new[] { "aks", "k8s", "aks-" } }
    };

    private readonly string[] _commonEnvironments = { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat" };

    public NamingConventionAnalyzer(ILogger<NamingConventionAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<NamingConventionResults> AnalyzeNamingConventionsAsync(List<AzureResource> resources, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting enhanced naming convention analysis for {ResourceCount} resources", resources.Count);

        var results = new NamingConventionResults
        {
            TotalResources = resources.Count
        };

        // Enhanced pattern analysis - all using Models namespace now
        results.PatternDistribution = AnalyzePatternDistribution(resources);
        results.PatternsByResourceType = AnalyzePatternsByResourceType(resources);
        results.EnvironmentIndicators = AnalyzeEnvironmentIndicators(resources);
        results.Consistency = AnalyzeOverallConsistency(resources);
        results.Violations = FindNamingViolations(resources, results.PatternsByResourceType).ToList();
        results.RepresentativeExamples = GenerateRepresentativeExamples(resources);

        // Calculate scores
        results.CompliantResources = results.TotalResources - results.Violations.Count;
        results.Score = results.TotalResources > 0
            ? Math.Round((decimal)results.CompliantResources / results.TotalResources * 100, 2)
            : 100m;

        _logger.LogInformation("Enhanced naming convention analysis completed. Score: {Score}%, Violations: {ViolationCount}",
            results.Score, results.Violations.Count);

        return Task.FromResult(results);
    }

    private Dictionary<string, NamingPatternStats> AnalyzePatternDistribution(List<AzureResource> resources)
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

        // Calculate percentages
        foreach (var stat in patternStats.Values)
        {
            stat.Percentage = resources.Count > 0
                ? Math.Round((decimal)stat.Count / resources.Count * 100, 1)
                : 0m;

            // Keep only top 3 examples for each pattern
            stat.Examples = stat.Examples.Take(3).ToList();
        }

        return patternStats;
    }

    private string ClassifyNamingPattern(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Other";

        // UUID pattern
        if (Regex.IsMatch(name, @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"))
            return "UUID";

        // Contains UUID
        if (Regex.IsMatch(name, @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}"))
            return "UUID";

        // Snake_case (contains underscores)
        if (name.Contains("_"))
            return "Snake_case";

        // Kebab-case (contains hyphens)
        if (name.Contains("-"))
            return "Kebab-case";

        // All uppercase
        if (name.All(c => !char.IsLetter(c) || char.IsUpper(c)))
            return "Uppercase";

        // All lowercase
        if (name.All(c => !char.IsLetter(c) || char.IsLower(c)))
            return "Lowercase";

        // PascalCase (starts with uppercase)
        if (char.IsUpper(name[0]) && Regex.IsMatch(name, @"^[A-Z][a-zA-Z0-9]*$"))
            return "PascalCase";

        // CamelCase (starts with lowercase, has uppercase)
        if (char.IsLower(name[0]) && name.Any(char.IsUpper))
            return "CamelCase";

        return "Other";
    }

    private Dictionary<string, NamingPatternAnalysis> AnalyzePatternsByResourceType(List<AzureResource> resources)
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

            var consistencyScore = typeResources.Count > 0
                ? (decimal)mostCommonPattern.Value / typeResources.Count * 100
                : 100m;

            patternsByType[resourceType] = new NamingPatternAnalysis
            {
                ResourceType = resourceType,
                TotalResources = typeResources.Count,
                MostCommonPattern = mostCommonPattern.Key,
                PatternCompliantResources = mostCommonPattern.Value,
                ConsistencyScore = Math.Round(consistencyScore, 2),
                DetectedPatterns = patterns.Keys.ToList(),
                PatternDistribution = patterns
            };
        }

        return patternsByType;
    }

    private EnvironmentIndicatorAnalysis AnalyzeEnvironmentIndicators(List<AzureResource> resources)
    {
        var analysis = new EnvironmentIndicatorAnalysis();

        foreach (var resource in resources)
        {
            var detectedEnv = DetectEnvironmentFromName(resource.Name);
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

        return analysis;
    }

    private string? DetectEnvironmentFromName(string name)
    {
        var nameLower = name.ToLowerInvariant();

        foreach (var env in _commonEnvironments)
        {
            if (nameLower.Contains(env))
                return env;
        }

        return null;
    }

    private NamingConsistencyMetrics AnalyzeOverallConsistency(List<AzureResource> resources)
    {
        var metrics = new NamingConsistencyMetrics();

        // Pattern consistency across all resources
        var patternDistribution = AnalyzePatternDistribution(resources);
        var topTwoPatterns = patternDistribution.Values
            .OrderByDescending(p => p.Count)
            .Take(2)
            .ToList();

        if (topTwoPatterns.Any())
        {
            var topPattern = topTwoPatterns.First();
            metrics.DominantPattern = topPattern.Pattern;
            metrics.DominantPatternPercentage = topPattern.Percentage;

            // Overall consistency is higher if top pattern dominates
            metrics.OverallConsistency = topPattern.Percentage;
        }

        // Environment prefix usage
        var envAnalysis = AnalyzeEnvironmentIndicators(resources);
        metrics.UsesEnvironmentPrefixes = envAnalysis.PercentageWithEnvironmentIndicators > 30; // 30% threshold

        // Resource type prefix usage
        var prefixCount = 0;
        foreach (var resource in resources)
        {
            var resourceType = resource.Type.ToLowerInvariant();
            if (_resourceTypePrefixes.TryGetValue(resourceType, out var prefixes))
            {
                if (prefixes.Any(prefix => resource.Name.ToLowerInvariant().StartsWith(prefix)))
                {
                    prefixCount++;
                }
            }
        }
        metrics.UsesResourceTypePrefixes = prefixCount > resources.Count * 0.3; // 30% threshold
        metrics.ResourceTypePrefixPercentage = resources.Count > 0
            ? Math.Round((decimal)prefixCount / resources.Count * 100, 2)
            : 0m;

        return metrics;
    }

    private Dictionary<string, List<string>> GenerateRepresentativeExamples(List<AzureResource> resources)
    {
        var examples = new Dictionary<string, List<string>>();

        foreach (var resource in resources.Take(50)) // Limit to avoid too many examples
        {
            var pattern = ClassifyNamingPattern(resource.Name);
            if (!examples.ContainsKey(pattern))
            {
                examples[pattern] = new List<string>();
            }

            if (examples[pattern].Count < 3) // Max 3 examples per pattern
            {
                examples[pattern].Add(resource.Name);
            }
        }

        return examples;
    }

    private IEnumerable<NamingViolation> FindNamingViolations(List<AzureResource> resources, Dictionary<string, NamingPatternAnalysis> patternsByType)
    {
        foreach (var resource in resources)
        {
            // Check for recommended prefixes
            var resourceType = resource.Type.ToLowerInvariant();
            if (_resourceTypePrefixes.TryGetValue(resourceType, out var recommendedPrefixes))
            {
                var hasCorrectPrefix = recommendedPrefixes.Any(prefix =>
                    resource.Name.ToLowerInvariant().StartsWith(prefix));

                if (!hasCorrectPrefix)
                {
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "MissingResourceTypePrefix",
                        Issue = $"Resource name doesn't start with recommended prefix: {string.Join(", ", recommendedPrefixes)}",
                        SuggestedName = $"{recommendedPrefixes[0]}-{resource.Name.ToLowerInvariant()}",
                        Severity = "Medium"
                    };
                }
            }

            // Check for invalid characters
            if (Regex.IsMatch(resource.Name, @"[^a-zA-Z0-9\-_\.]"))
            {
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "InvalidCharacters",
                    Issue = "Resource name contains invalid characters (only letters, numbers, hyphens, underscores, and periods are recommended)",
                    SuggestedName = Regex.Replace(resource.Name, @"[^a-zA-Z0-9\-_\.]", ""),
                    Severity = "High"
                };
            }

            // Check for length violations
            if (resource.Name.Length > 63)
            {
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "NameTooLong",
                    Issue = "Resource name exceeds recommended maximum length of 63 characters",
                    SuggestedName = resource.Name.Substring(0, 60) + "...",
                    Severity = "Medium"
                };
            }

            // Check for pattern inconsistency within resource type
            if (patternsByType.TryGetValue(resourceType, out var typeAnalysis) &&
                typeAnalysis.ConsistencyScore < 70) // Less than 70% consistency
            {
                var resourcePattern = ClassifyNamingPattern(resource.Name);
                if (resourcePattern != typeAnalysis.MostCommonPattern)
                {
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "InconsistentPattern",
                        Issue = $"Resource naming pattern '{resourcePattern}' doesn't match the most common pattern '{typeAnalysis.MostCommonPattern}' for this resource type",
                        SuggestedName = ConvertToPattern(resource.Name, typeAnalysis.MostCommonPattern),
                        Severity = "Low"
                    };
                }
            }
        }
    }

    private string ConvertToPattern(string name, string targetPattern)
    {
        return targetPattern.ToLowerInvariant() switch
        {
            "lowercase" => name.ToLowerInvariant(),
            "uppercase" => name.ToUpperInvariant(),
            "kebab-case" => Regex.Replace(name, @"[_\s]+", "-").ToLowerInvariant(),
            "snake_case" => Regex.Replace(name, @"[-\s]+", "_").ToLowerInvariant(),
            _ => name.ToLowerInvariant()
        };
    }
}