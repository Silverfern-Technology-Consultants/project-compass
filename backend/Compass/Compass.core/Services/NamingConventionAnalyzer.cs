using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Compass.Core.Services;

public interface INamingConventionAnalyzer : IPreferenceAwareNamingAnalyzer
{
    // Keep existing method for backward compatibility
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

    // Standard analysis method (backward compatibility)
    public Task<NamingConventionResults> AnalyzeNamingConventionsAsync(List<AzureResource> resources, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting standard naming convention analysis for {ResourceCount} resources", resources.Count);
        var results = AnalyzeNamingConventionsSync(resources, null);
        return Task.FromResult(results);
    }

    // Preference-aware analysis method (implements IPreferenceAwareNamingAnalyzer)
    public Task<NamingConventionResults> AnalyzeNamingConventionsAsync(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig = null,
        CancellationToken cancellationToken = default)
    {
        if (clientConfig != null)
        {
            _logger.LogInformation("Starting client-preference-aware naming analysis for {ResourceCount} resources with client {ClientName}",
                resources.Count, clientConfig.ClientName);
            var results = AnalyzeWithClientPreferences(resources, clientConfig);
            return Task.FromResult(results);
        }

        _logger.LogInformation("Starting standard naming convention analysis for {ResourceCount} resources", resources.Count);
        var standardResults = AnalyzeNamingConventionsSync(resources, null);
        return Task.FromResult(standardResults);
    }

    // Synchronous analysis method
    private NamingConventionResults AnalyzeNamingConventionsSync(List<AzureResource> resources, ClientAssessmentConfiguration? clientConfig)
    {
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

        _logger.LogInformation("Standard naming convention analysis completed. Score: {Score}%, Violations: {ViolationCount}",
            results.Score, results.Violations.Count);

        return results;
    }

    // Client preference-aware analysis
    private NamingConventionResults AnalyzeWithClientPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        _logger.LogInformation("Running client-preference-aware naming analysis for {ResourceCount} resources", resources.Count);

        var results = new NamingConventionResults
        {
            TotalResources = resources.Count
        };

        // Enhanced pattern analysis with client preferences
        results.PatternDistribution = AnalyzePatternDistributionWithPreferences(resources, clientConfig);
        results.PatternsByResourceType = AnalyzePatternsByResourceTypeWithPreferences(resources, clientConfig);
        results.EnvironmentIndicators = AnalyzeEnvironmentIndicatorsWithPreferences(resources, clientConfig);
        results.Consistency = AnalyzeConsistencyAgainstPreferences(resources, clientConfig);
        results.Violations = FindNamingViolationsAgainstPreferences(resources, clientConfig).ToList();
        results.RepresentativeExamples = GenerateRepresentativeExamples(resources);

        // Calculate compliance score based on client preferences
        results.CompliantResources = results.TotalResources - results.Violations.Count;
        results.Score = CalculatePreferenceBasedScore(results, clientConfig);

        _logger.LogInformation("Client-preference-aware naming analysis completed. Score: {Score}%, Violations: {ViolationCount}, Client compliance: {ClientCompliance}%",
            results.Score, results.Violations.Count, results.Consistency.ClientPreferenceCompliance);

        return results;
    }

    // Preference-aware analysis methods
    private Dictionary<string, NamingPatternStats> AnalyzePatternDistributionWithPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var patternStats = AnalyzePatternDistribution(resources);
        var allowedPatterns = clientConfig.GetEffectiveNamingPatterns();

        if (allowedPatterns.Any())
        {
            _logger.LogInformation("Client prefers naming patterns: {Patterns}", string.Join(", ", allowedPatterns));

            // Mark patterns as compliant/non-compliant based on client preferences
            foreach (var stat in patternStats.Values)
            {
                // Note: We can't modify the base NamingPatternStats class to add compliance info
                // So we'll handle this in the scoring logic instead
                if (allowedPatterns.Contains(stat.Pattern))
                {
                    _logger.LogDebug("Pattern {Pattern} is client-preferred ({Count} resources)",
                        stat.Pattern, stat.Count);
                }
            }
        }

        return patternStats;
    }

    private Dictionary<string, NamingPatternAnalysis> AnalyzePatternsByResourceTypeWithPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var patternsByType = AnalyzePatternsByResourceType(resources);
        var allowedPatterns = clientConfig.GetEffectiveNamingPatterns();

        if (allowedPatterns.Any())
        {
            foreach (var analysis in patternsByType.Values)
            {
                // Adjust consistency score based on client preference compliance
                if (!string.IsNullOrEmpty(analysis.MostCommonPattern) && !allowedPatterns.Contains(analysis.MostCommonPattern))
                {
                    analysis.ConsistencyScore *= 0.7m; // Reduce score for non-preferred patterns
                    _logger.LogDebug("Reduced consistency score for {ResourceType} - pattern {Pattern} not client-preferred",
                        analysis.ResourceType, analysis.MostCommonPattern);
                }
            }
        }

        return patternsByType;
    }

    private EnvironmentIndicatorAnalysis AnalyzeEnvironmentIndicatorsWithPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var analysis = AnalyzeEnvironmentIndicators(resources);

        // Use client-specific environment patterns if available
        var expectedPatterns = clientConfig.GetExpectedEnvironmentPatterns();
        if (expectedPatterns.Any())
        {
            _logger.LogInformation("Using client-specific environment patterns: {Patterns}",
                string.Join(", ", expectedPatterns));

            // Re-analyze with client-specific patterns
            analysis = new EnvironmentIndicatorAnalysis();
            foreach (var resource in resources)
            {
                var detectedEnv = DetectEnvironmentFromName(resource.Name, expectedPatterns);
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
        }

        // Check if this meets client requirements
        if (clientConfig.AreEnvironmentIndicatorsRequired())
        {
            var requiredThreshold = clientConfig.EnvironmentIndicatorLevel == "required" ? 90m : 70m;
            analysis.MeetsClientRequirements = analysis.PercentageWithEnvironmentIndicators >= requiredThreshold;

            _logger.LogInformation("Environment indicator compliance: {Percentage}% (required: {Threshold}%, meets requirement: {Meets})",
                analysis.PercentageWithEnvironmentIndicators, requiredThreshold, analysis.MeetsClientRequirements);
        }

        return analysis;
    }

    private NamingConsistencyMetrics AnalyzeConsistencyAgainstPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var metrics = new NamingConsistencyMetrics();

        // Analyze pattern consistency against client preferences
        var allowedPatterns = clientConfig.GetEffectiveNamingPatterns();

        if (allowedPatterns.Any())
        {
            // Calculate compliance with client's preferred patterns
            var compliantResources = 0;
            foreach (var resource in resources)
            {
                var pattern = ClassifyNamingPattern(resource.Name);
                if (allowedPatterns.Contains(pattern))
                {
                    compliantResources++;
                }
            }

            metrics.ClientPreferenceCompliance = resources.Count > 0
                ? Math.Round((decimal)compliantResources / resources.Count * 100, 2)
                : 100m;

            metrics.OverallConsistency = metrics.ClientPreferenceCompliance;

            _logger.LogInformation("Client preference compliance calculated: {Compliance}% ({Compliant}/{Total} resources)",
                metrics.ClientPreferenceCompliance, compliantResources, resources.Count);
        }
        else
        {
            // Fallback to standard consistency analysis
            var patternDistribution = AnalyzePatternDistribution(resources);
            var topPattern = patternDistribution.Values.OrderByDescending(p => p.Count).FirstOrDefault();
            metrics.OverallConsistency = topPattern?.Percentage ?? 0m;
            metrics.ClientPreferenceCompliance = 0m; // No preferences to comply with
        }

        // Environment prefix analysis based on client requirements
        if (clientConfig.AreEnvironmentIndicatorsRequired())
        {
            var envAnalysis = AnalyzeEnvironmentIndicatorsWithPreferences(resources, clientConfig);
            metrics.UsesEnvironmentPrefixes = envAnalysis.PercentageWithEnvironmentIndicators > 50;
        }

        return metrics;
    }

    private IEnumerable<NamingViolation> FindNamingViolationsAgainstPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var allowedPatterns = clientConfig.GetEffectiveNamingPatterns();
        var envPatternsRequired = clientConfig.AreEnvironmentIndicatorsRequired();
        var expectedEnvPatterns = clientConfig.GetExpectedEnvironmentPatterns();

        foreach (var resource in resources)
        {
            // Check against client's preferred naming patterns
            if (allowedPatterns.Any())
            {
                var resourcePattern = ClassifyNamingPattern(resource.Name);
                if (!allowedPatterns.Contains(resourcePattern))
                {
                    var severity = clientConfig.AdjustViolationSeverity("Medium", "ClientPreferenceViolation");
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "ClientPreferenceViolation",
                        Issue = $"Resource naming pattern '{resourcePattern}' doesn't match client's preferred patterns: {string.Join(", ", allowedPatterns)}",
                        SuggestedName = ConvertToClientPreferredPattern(resource.Name, allowedPatterns.First()),
                        Severity = severity
                    };
                }
            }

            // Check for required environment indicators
            if (envPatternsRequired)
            {
                var hasEnvironmentIndicator = expectedEnvPatterns.Any(env =>
                    resource.Name.ToLowerInvariant().Contains(env.ToLowerInvariant()));

                if (!hasEnvironmentIndicator)
                {
                    var severity = clientConfig.AdjustViolationSeverity("High", "MissingRequiredElement");
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "MissingRequiredElement",
                        Issue = $"Resource name doesn't include required environment indicator. Expected patterns: {string.Join(", ", expectedEnvPatterns)}",
                        SuggestedName = $"{resource.Name}-{expectedEnvPatterns.FirstOrDefault() ?? "env"}",
                        Severity = severity
                    };
                }
            }

            // Standard violations (invalid characters, length, etc.)
            foreach (var violation in FindStandardViolations(resource, clientConfig))
            {
                yield return violation;
            }
        }
    }

    private IEnumerable<NamingViolation> FindStandardViolations(AzureResource resource, ClientAssessmentConfiguration? clientConfig = null)
    {
        // Invalid characters
        if (Regex.IsMatch(resource.Name, @"[^a-zA-Z0-9\-_\.]"))
        {
            var severity = clientConfig?.AdjustViolationSeverity("High", "InvalidCharacters") ?? "High";
            yield return new NamingViolation
            {
                ResourceId = resource.Id,
                ResourceName = resource.Name,
                ResourceType = resource.Type,
                ViolationType = "InvalidCharacters",
                Issue = "Resource name contains invalid characters",
                SuggestedName = Regex.Replace(resource.Name, @"[^a-zA-Z0-9\-_\.]", ""),
                Severity = severity
            };
        }

        // Length violations
        if (resource.Name.Length > 63)
        {
            var severity = clientConfig?.AdjustViolationSeverity("Medium", "NameTooLong") ?? "Medium";
            yield return new NamingViolation
            {
                ResourceId = resource.Id,
                ResourceName = resource.Name,
                ResourceType = resource.Type,
                ViolationType = "NameTooLong",
                Issue = "Resource name exceeds maximum length of 63 characters",
                SuggestedName = resource.Name.Substring(0, 60) + "...",
                Severity = severity
            };
        }

        // Resource type prefix violations (only if not overridden by client preferences)
        var resourceType = resource.Type.ToLowerInvariant();
        if (_resourceTypePrefixes.TryGetValue(resourceType, out var recommendedPrefixes))
        {
            var hasCorrectPrefix = recommendedPrefixes.Any(prefix =>
                resource.Name.ToLowerInvariant().StartsWith(prefix));

            if (!hasCorrectPrefix)
            {
                var severity = clientConfig?.AdjustViolationSeverity("Medium", "MissingResourceTypePrefix") ?? "Medium";
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "MissingResourceTypePrefix",
                    Issue = $"Resource name doesn't start with recommended prefix: {string.Join(", ", recommendedPrefixes)}",
                    SuggestedName = $"{recommendedPrefixes[0]}-{resource.Name.ToLowerInvariant()}",
                    Severity = severity
                };
            }
        }
    }

    private decimal CalculatePreferenceBasedScore(NamingConventionResults results, ClientAssessmentConfiguration clientConfig)
    {
        var scoringFactors = new List<decimal>();

        // Client preference compliance (50% weight if preferences exist)
        if (clientConfig.HasNamingPreferences && results.Consistency.ClientPreferenceCompliance > 0)
        {
            scoringFactors.Add(results.Consistency.ClientPreferenceCompliance * 0.5m);
            _logger.LogDebug("Client preference compliance score: {Score}%", results.Consistency.ClientPreferenceCompliance);
        }
        else
        {
            // Fall back to pattern consistency if no preferences
            scoringFactors.Add(results.Consistency.OverallConsistency * 0.5m);
        }

        // Environment indicator compliance (25% weight if required)
        if (clientConfig.AreEnvironmentIndicatorsRequired())
        {
            var envCompliance = results.EnvironmentIndicators.MeetsClientRequirements ? 100m :
                results.EnvironmentIndicators.PercentageWithEnvironmentIndicators;
            scoringFactors.Add(envCompliance * 0.25m);
            _logger.LogDebug("Environment indicator compliance score: {Score}%", envCompliance);
        }
        else
        {
            // No requirement, so full points
            scoringFactors.Add(100m * 0.25m);
        }

        // Standard violations penalty (25% weight)
        var standardViolations = results.Violations.Count(v =>
            v.ViolationType == "InvalidCharacters" ||
            v.ViolationType == "NameTooLong" ||
            v.ViolationType == "MissingResourceTypePrefix");

        var violationPenalty = results.TotalResources > 0
            ? (decimal)standardViolations / results.TotalResources * 100
            : 0m;
        scoringFactors.Add((100 - violationPenalty) * 0.25m);
        _logger.LogDebug("Standard violations penalty: {Penalty}% ({Violations}/{Total} violations)",
            violationPenalty, standardViolations, results.TotalResources);

        var finalScore = Math.Round(scoringFactors.Sum(), 2);
        _logger.LogInformation("Preference-based score calculated: {Score}% (factors: {Factors})",
            finalScore, string.Join(", ", scoringFactors.Select(f => f.ToString("F1"))));

        return finalScore;
    }

    // Standard analysis methods (unchanged from original)
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
            var detectedEnv = DetectEnvironmentFromName(resource.Name, _commonEnvironments.ToList());
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

    private string? DetectEnvironmentFromName(string name, List<string> environmentPatterns)
    {
        var nameLower = name.ToLowerInvariant();
        return environmentPatterns.FirstOrDefault(env => nameLower.Contains(env.ToLowerInvariant()));
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
            metrics.OverallConsistency = topPattern.Percentage;
        }

        // Environment prefix usage
        var envAnalysis = AnalyzeEnvironmentIndicators(resources);
        metrics.UsesEnvironmentPrefixes = envAnalysis.PercentageWithEnvironmentIndicators > 30;

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
        metrics.UsesResourceTypePrefixes = prefixCount > resources.Count * 0.3;
        metrics.ResourceTypePrefixPercentage = resources.Count > 0
            ? Math.Round((decimal)prefixCount / resources.Count * 100, 2)
            : 0m;

        return metrics;
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

    private IEnumerable<NamingViolation> FindNamingViolations(List<AzureResource> resources, Dictionary<string, NamingPatternAnalysis> patternsByType)
    {
        foreach (var resource in resources)
        {
            foreach (var violation in FindStandardViolations(resource))
            {
                yield return violation;
            }

            // Check for pattern inconsistency within resource type
            var resourceType = resource.Type.ToLowerInvariant();
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

    private string ConvertToClientPreferredPattern(string name, string targetPattern)
    {
        return targetPattern.ToLowerInvariant() switch
        {
            "lowercase" => name.ToLowerInvariant(),
            "uppercase" => name.ToUpperInvariant(),
            "kebab-case" => Regex.Replace(name, @"[_\s]+", "-").ToLowerInvariant(),
            "snake_case" => Regex.Replace(name, @"[-\s]+", "_").ToLowerInvariant(),
            "camelcase" => ConvertToCamelCase(name),
            "pascalcase" => ConvertToPascalCase(name),
            _ => name.ToLowerInvariant()
        };
    }

    private string ConvertToPattern(string name, string? targetPattern)
    {
        if (string.IsNullOrEmpty(targetPattern))
            return name.ToLowerInvariant();

        return targetPattern.ToLowerInvariant() switch
        {
            "lowercase" => name.ToLowerInvariant(),
            "uppercase" => name.ToUpperInvariant(),
            "kebab-case" => Regex.Replace(name, @"[_\s]+", "-").ToLowerInvariant(),
            "snake_case" => Regex.Replace(name, @"[-\s]+", "_").ToLowerInvariant(),
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

    private string ConvertToPascalCase(string name)
    {
        var words = Regex.Split(name, @"[-_\s]+");
        var result = "";
        foreach (var word in words)
        {
            if (word.Length > 0)
            {
                result += char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
            }
        }
        return result;
    }
}