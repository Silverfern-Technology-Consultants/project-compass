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
        
        // Storage
        { "microsoft.storage/storageaccounts", new[] { "st", "stor", "storage" } },
        { "microsoft.storage/storageaccounts/blobservices", new[] { "blob", "blob-" } },
        
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
    private readonly string[] _commonSeparators = { "-", "_", "." };

    public NamingConventionAnalyzer(ILogger<NamingConventionAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<NamingConventionResults> AnalyzeNamingConventionsAsync(List<AzureResource> resources, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting naming convention analysis for {ResourceCount} resources", resources.Count);

        var results = new NamingConventionResults
        {
            TotalResources = resources.Count
        };

        // Group resources by type for pattern analysis
        var resourcesByType = resources.GroupBy(r => r.Type.ToLowerInvariant()).ToList();

        // Analyze patterns for each resource type
        foreach (var group in resourcesByType)
        {
            var patternAnalysis = AnalyzeResourceTypePatterns(group.ToList());
            results.PatternsByResourceType[group.Key] = patternAnalysis;
        }

        // Analyze overall consistency
        results.Consistency = AnalyzeOverallConsistency(resources);

        // Find violations
        results.Violations = FindNamingViolations(resources, results.PatternsByResourceType).ToList();

        // Calculate scores
        results.CompliantResources = results.TotalResources - results.Violations.Count;
        results.Score = results.TotalResources > 0
            ? Math.Round((decimal)results.CompliantResources / results.TotalResources * 100, 2)
            : 100m;

        _logger.LogInformation("Naming convention analysis completed. Score: {Score}%, Violations: {ViolationCount}",
            results.Score, results.Violations.Count);

        return Task.FromResult(results);
    }

    private NamingPatternAnalysis AnalyzeResourceTypePatterns(List<AzureResource> resources)
    {
        var resourceType = resources.First().Type.ToLowerInvariant();
        var patterns = new List<string>();

        // Extract patterns from resource names
        foreach (var resource in resources)
        {
            var pattern = ExtractNamingPattern(resource.Name);
            if (!string.IsNullOrEmpty(pattern) && !patterns.Contains(pattern))
            {
                patterns.Add(pattern);
            }
        }

        // Find the most common pattern
        var patternCounts = patterns.GroupBy(p => p)
            .ToDictionary(g => g.Key, g => g.Count());

        var mostCommonPattern = patternCounts.Any()
            ? patternCounts.OrderByDescending(kvp => kvp.Value).First().Key
            : null;

        // Calculate consistency score
        var consistentResources = 0;
        if (!string.IsNullOrEmpty(mostCommonPattern))
        {
            consistentResources = resources.Count(r =>
                ExtractNamingPattern(r.Name) == mostCommonPattern);
        }

        var consistencyScore = resources.Count > 0
            ? (decimal)consistentResources / resources.Count * 100
            : 100m;

        return new NamingPatternAnalysis
        {
            ResourceType = resourceType,
            DetectedPatterns = patterns,
            MostCommonPattern = mostCommonPattern,
            ConsistencyScore = Math.Round(consistencyScore, 2),
            TotalResources = resources.Count,
            PatternCompliantResources = consistentResources
        };
    }

    private string ExtractNamingPattern(string resourceName)
    {
        var name = resourceName.ToLowerInvariant();
        var pattern = new List<string>();

        // Check for environment indicators
        foreach (var env in _commonEnvironments)
        {
            if (name.Contains(env))
            {
                pattern.Add("{env}");
                break;
            }
        }

        // Check for separators
        foreach (var separator in _commonSeparators)
        {
            if (name.Contains(separator))
            {
                pattern.Add($"{{{separator}}}");
                break;
            }
        }

        // Look for number patterns
        if (Regex.IsMatch(name, @"\d+"))
        {
            pattern.Add("{number}");
        }

        // Look for common suffixes
        if (name.EndsWith("001") || name.EndsWith("01") || name.EndsWith("1"))
        {
            pattern.Add("{sequence}");
        }

        return pattern.Any() ? string.Join("", pattern) : "custom";
    }

    private NamingConsistencyMetrics AnalyzeOverallConsistency(List<AzureResource> resources)
    {
        var metrics = new NamingConsistencyMetrics();

        // Check separator consistency
        var separatorUsage = new Dictionary<string, int>();
        foreach (var separator in _commonSeparators)
        {
            var count = resources.Count(r => r.Name.Contains(separator));
            if (count > 0)
            {
                separatorUsage[separator] = count;
            }
        }

        if (separatorUsage.Any())
        {
            var mostUsedSeparator = separatorUsage.OrderByDescending(kvp => kvp.Value).First();
            metrics.PrimarySeparator = mostUsedSeparator.Key;
            metrics.UsesConsistentSeparators = mostUsedSeparator.Value > resources.Count * 0.7; // 70% threshold
        }

        // Check environment prefix usage
        var envPrefixCount = resources.Count(r =>
            _commonEnvironments.Any(env => r.Name.ToLowerInvariant().Contains(env)));
        metrics.UsesEnvironmentPrefixes = envPrefixCount > resources.Count * 0.5; // 50% threshold

        // Check resource type prefix usage
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
        metrics.UsesResourceTypePrefixes = prefixCount > resources.Count * 0.5; // 50% threshold

        // Calculate overall consistency
        var consistencyFactors = new[]
        {
            metrics.UsesConsistentSeparators ? 1 : 0,
            metrics.UsesEnvironmentPrefixes ? 1 : 0,
            metrics.UsesResourceTypePrefixes ? 1 : 0
        };

        metrics.OverallConsistency = Math.Round((decimal)consistencyFactors.Sum() / consistencyFactors.Length * 100, 2);

        return metrics;
    }

    private IEnumerable<NamingViolation> FindNamingViolations(List<AzureResource> resources, Dictionary<string, NamingPatternAnalysis> patternsByType)
    {
        foreach (var resource in resources)
        {
            var violations = new List<NamingViolation>();

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
                        SuggestedName = $"{recommendedPrefixes[0]}-{resource.Name}",
                        Severity = "Medium"
                    };
                }
            }

            // Check for pattern consistency within resource type
            if (patternsByType.TryGetValue(resourceType, out var typeAnalysis) &&
                !string.IsNullOrEmpty(typeAnalysis.MostCommonPattern))
            {
                var resourcePattern = ExtractNamingPattern(resource.Name);
                if (resourcePattern != typeAnalysis.MostCommonPattern && typeAnalysis.ConsistencyScore < 80)
                {
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "InconsistentPattern",
                        Issue = $"Resource naming pattern '{resourcePattern}' doesn't match the most common pattern '{typeAnalysis.MostCommonPattern}' for this resource type",
                        SuggestedName = GenerateSuggestedName(resource, typeAnalysis.MostCommonPattern),
                        Severity = "Low"
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

            // Check for length violations (general guidance)
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
        }
    }

    private string GenerateSuggestedName(AzureResource resource, string pattern)
    {
        // This is a simplified suggestion generator
        // In a real implementation, you'd have more sophisticated logic
        var resourceType = resource.Type.ToLowerInvariant();

        if (_resourceTypePrefixes.TryGetValue(resourceType, out var prefixes))
        {
            var prefix = prefixes[0];
            var environment = resource.Environment ?? "env";
            return $"{prefix}-{environment}-{resource.Name.ToLowerInvariant()}";
        }

        return resource.Name.ToLowerInvariant();
    }
}