using Compass.Core.Models;
using Compass.Core.Services.Naming;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

namespace Compass.Core.Services;

public interface IPreferenceAwareNamingAnalyzer
{
    Task<NamingConventionResults> AnalyzeNamingConventionsAsync(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig = null,
        CancellationToken cancellationToken = default);
}

public interface INamingConventionAnalyzer : IPreferenceAwareNamingAnalyzer
{
    // Keep existing method for backward compatibility
    Task<NamingConventionResults> AnalyzeNamingConventionsAsync(List<AzureResource> resources, CancellationToken cancellationToken = default);
}

public class NamingConventionAnalyzer : INamingConventionAnalyzer
{
    private readonly ILogger<NamingConventionAnalyzer> _logger;

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
    // ENHANCED: Client preference-aware analysis with flexible naming patterns
    private NamingConventionResults AnalyzeWithClientPreferences(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        _logger.LogInformation("Running enhanced naming analysis for {ResourceCount} resources", resources.Count);

        // FILTER OUT system-generated resources that cannot be renamed
        var analyzeableResources = SystemResourceFilter.FilterAnalyzableResources(resources);
        var filterStats = SystemResourceFilter.GetFilterStats(resources, analyzeableResources);

        _logger.LogInformation("Filtered to {AnalyzeableCount} analyzeable resources (excluded {SystemCount} system-generated)",
            analyzeableResources.Count, filterStats.FilteredOutResources);

        var results = new NamingConventionResults
        {
            TotalResources = analyzeableResources.Count
        };

        // Enhanced pattern analysis with flexible structure support
        results.PatternDistribution = AnalyzePatternDistributionWithPreferences(analyzeableResources, clientConfig);
        results.PatternsByResourceType = AnalyzePatternsByResourceTypeWithPreferences(analyzeableResources, clientConfig);
        results.EnvironmentIndicators = AnalyzeEnvironmentIndicatorsWithPreferences(analyzeableResources, clientConfig);
        results.Consistency = AnalyzeConsistencyAgainstPreferences(analyzeableResources, clientConfig);

        // ENHANCED: Use consolidated violations instead of duplicates
        results.Violations = FindConsolidatedViolations(analyzeableResources, clientConfig).ToList();
        results.RepresentativeExamples = GenerateRepresentativeExamples(analyzeableResources);

        // Calculate compliance score based on flexible patterns
        results.CompliantResources = results.TotalResources - results.Violations.Count;
        results.Score = CalculatePreferenceBasedScore(results, clientConfig);

        _logger.LogInformation("Enhanced naming analysis completed. Score: {Score}%, Violations: {ViolationCount}",
            results.Score, results.Violations.Count);

        return results;
    }

    // Synchronous analysis method (existing functionality preserved)
    private NamingConventionResults AnalyzeNamingConventionsSync(List<AzureResource> resources, ClientAssessmentConfiguration? clientConfig)
    {
        // ENHANCED: Filter system resources even in standard analysis
        var analyzeableResources = SystemResourceFilter.FilterAnalyzableResources(resources);

        var results = new NamingConventionResults
        {
            TotalResources = analyzeableResources.Count
        };

        // Enhanced pattern analysis - all using Models namespace now
        results.PatternDistribution = AnalyzePatternDistribution(analyzeableResources);
        results.PatternsByResourceType = AnalyzePatternsByResourceType(analyzeableResources);
        results.EnvironmentIndicators = AnalyzeEnvironmentIndicators(analyzeableResources);
        results.Consistency = AnalyzeOverallConsistency(analyzeableResources);

        // ENHANCED: Use consolidated violations for standard analysis too
        results.Violations = FindNamingViolations(analyzeableResources, results.PatternsByResourceType).ToList();
        results.RepresentativeExamples = GenerateRepresentativeExamples(analyzeableResources);

        // Calculate scores
        results.CompliantResources = results.TotalResources - results.Violations.Count;
        results.Score = results.TotalResources > 0
            ? Math.Round((decimal)results.CompliantResources / results.TotalResources * 100, 2)
            : 100m;

        _logger.LogInformation("Standard naming convention analysis completed. Score: {Score}%, Violations: {ViolationCount}",
            results.Score, results.Violations.Count);

        return results;
    }

    // ENHANCED: Client preference-aware pattern analysis
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
                var detectedEnv = NamingValidationHelpers.DetectEnvironmentFromName(resource.Name, expectedPatterns);
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

        // If client has a custom naming scheme, use it for validation
        if (clientConfig.NamingScheme?.Components.Any() == true)
        {
            var schemeCompliance = AnalyzeNamingSchemeCompliance(resources, clientConfig);
            metrics.OverallConsistency = schemeCompliance.OverallCompliance;
            metrics.ClientPreferenceCompliance = schemeCompliance.OverallCompliance;

            _logger.LogInformation("Custom naming scheme compliance: {Compliance}%", schemeCompliance.OverallCompliance);
        }
        else
        {
            // Use flexible structure detection
            var structureAnalysis = AnalyzeFlexibleNamingConsistency(resources, clientConfig);
            metrics.OverallConsistency = structureAnalysis.OverallConsistency;
            metrics.ClientPreferenceCompliance = structureAnalysis.ClientPreferenceCompliance;
        }

        // Environment prefix analysis based on client requirements
        if (clientConfig.AreEnvironmentIndicatorsRequired())
        {
            var envAnalysis = AnalyzeEnvironmentIndicatorsWithPreferences(resources, clientConfig);
            metrics.UsesEnvironmentPrefixes = envAnalysis.PercentageWithEnvironmentIndicators > 50;
        }

        return metrics;
    }

    // ENHANCED: Analyze flexible naming structure consistency
    private NamingConsistencyMetrics AnalyzeFlexibleNamingConsistency(
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
                var pattern = NamingValidationHelpers.ClassifyNamingPattern(resource.Name);
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

        // Resource type abbreviation usage
        var resourceTypeUsage = AnalyzeResourceTypeAbbreviationUsage(resources);
        metrics.UsesResourceTypePrefixes = resourceTypeUsage.OverallUsagePercentage > 30;
        metrics.ResourceTypePrefixPercentage = resourceTypeUsage.OverallUsagePercentage;

        // Separator consistency
        var separatorAnalysis = AnalyzeSeparatorConsistency(resources);
        metrics.UsesConsistentSeparators = separatorAnalysis.ConsistencyPercentage > 70;
        metrics.PrimarySeparator = separatorAnalysis.PrimarySeparator;

        return metrics;
    }

    // ENHANCED: Analyze compliance with custom naming scheme
    private NamingSchemeComplianceAnalysis AnalyzeNamingSchemeCompliance(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        var analysis = new NamingSchemeComplianceAnalysis();
        var compliantResources = 0;

        foreach (var resource in resources)
        {
            var validation = clientConfig.ValidateResourceName(resource);
            if (validation.IsCompliant)
            {
                compliantResources++;
            }
        }

        analysis.OverallCompliance = resources.Count > 0
            ? Math.Round((decimal)compliantResources / resources.Count * 100, 2)
            : 100m;

        analysis.CompliantResources = compliantResources;
        analysis.TotalResources = resources.Count;

        return analysis;
    }

    /// <summary>
    /// ENHANCED: Find consolidated violations - ONE finding per resource instead of multiple duplicates
    /// </summary>
    private IEnumerable<NamingViolation> FindConsolidatedViolations(
        List<AzureResource> resources,
        ClientAssessmentConfiguration clientConfig)
    {
        foreach (var resource in resources)
        {
            var violations = new List<string>();
            var suggestedName = resource.Name.ToLowerInvariant();
            var severity = "Medium";

            // If client has custom naming scheme, validate against it
            if (clientConfig.NamingScheme?.Components.Any() == true)
            {
                var schemeValidation = clientConfig.ValidateResourceAgainstScheme(resource);
                if (!schemeValidation.IsCompliant)
                {
                    violations.Add($"Doesn't follow client naming scheme: {schemeValidation.Message}");
                    suggestedName = schemeValidation.SuggestedName ?? GenerateSchemeCompliantName(resource, clientConfig);
                    severity = "High";
                }
            }
            else
            {
                // Standard naming validation
                var standardIssues = FindStandardIssues(resource, clientConfig);
                violations.AddRange(standardIssues.Issues);
                if (standardIssues.SuggestedName != resource.Name)
                {
                    suggestedName = standardIssues.SuggestedName;
                }
                if (standardIssues.Issues.Any())
                {
                    severity = standardIssues.Issues.Any(i => i.Contains("Invalid characters")) ? "High" : "Medium";
                }
            }

            // Only yield a violation if we found actual issues
            if (violations.Any())
            {
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "ConsolidatedNamingViolation",
                    Issue = string.Join("; ", violations),
                    SuggestedName = suggestedName,
                    Severity = severity
                };
            }
        }
    }

    /// <summary>
    /// Find standard naming issues (character validation, length, etc.)
    /// </summary>
    private StandardValidationResult FindStandardIssues(AzureResource resource, ClientAssessmentConfiguration clientConfig)
    {
        var issues = NamingValidationHelpers.ValidateBasicRules(resource.Name);
        var suggestedName = resource.Name.ToLowerInvariant();

        // Resource type prefix validation (only if no custom scheme)
        if (clientConfig.NamingScheme?.Components.Any() != true)
        {
            var abbreviation = AzureResourceAbbreviations.GetAbbreviationWithKind(resource.Type, resource.Kind);
            var hasCorrectPrefix = resource.Name.ToLowerInvariant().Contains(abbreviation);

            if (!hasCorrectPrefix)
            {
                issues.Add($"Missing recommended resource type indicator: {abbreviation}");
                if (!suggestedName.Contains("-"))
                {
                    suggestedName = $"{abbreviation}-{suggestedName}";
                }
            }
        }

        // Check for accepted company names if configured
        var acceptedCompanyNames = clientConfig.GetAcceptedCompanyNames();
        if (acceptedCompanyNames.Any())
        {
            var hasAcceptedCompany = acceptedCompanyNames.Any(company =>
                resource.Name.ToLowerInvariant().Contains(company.ToLowerInvariant()));

            if (!hasAcceptedCompany)
            {
                issues.Add($"Missing accepted company identifier. Accepted names: {string.Join(", ", acceptedCompanyNames)}");

                // Suggest adding the first accepted company name
                var suggestedCompany = acceptedCompanyNames.First().ToLowerInvariant();
                if (!suggestedName.Contains("-"))
                {
                    suggestedName = $"{suggestedCompany}-{suggestedName}";
                }
                else if (!suggestedName.StartsWith(suggestedCompany))
                {
                    suggestedName = $"{suggestedCompany}-{suggestedName}";
                }
            }
        }

        return new StandardValidationResult
        {
            Issues = issues,
            SuggestedName = suggestedName
        };
    }

    /// <summary>
    /// Generate a compliant name based on client's naming scheme
    /// </summary>
    private string GenerateSchemeCompliantName(AzureResource resource, ClientAssessmentConfiguration clientConfig)
    {
        if (clientConfig.NamingScheme?.Components.Any() != true)
            return resource.Name.ToLowerInvariant();

        var orderedComponents = clientConfig.NamingScheme.Components.OrderBy(c => c.Position).ToList();
        var nameParts = new List<string>();
        var separator = clientConfig.NamingScheme.Separator ?? "-";

        foreach (var component in orderedComponents)
        {
            var value = GenerateComponentValueForResource(component, resource, clientConfig);
            nameParts.Add(value);
        }

        return string.Join(separator, nameParts);
    }

    /// <summary>
    /// Generate component value based on actual resource and client config
    /// </summary>
    private string GenerateComponentValueForResource(NamingComponent component, AzureResource resource, ClientAssessmentConfiguration clientConfig)
    {
        switch (component.ComponentType.ToLowerInvariant())
        {
            case "company":
                // Use accepted company names if available
                var acceptedNames = clientConfig.GetAcceptedCompanyNames();
                if (acceptedNames.Any())
                    return acceptedNames.First().ToLowerInvariant();

                // Try to extract from existing name or use default
                var existingParts = resource.Name.ToLowerInvariant().Split(new[] { '-', '_', '.' });
                var companyPart = existingParts.FirstOrDefault(p => p.Length >= 2 && p.Length <= 5 && !NamingValidationHelpers.IsCommonWord(p));
                return companyPart ?? component.DefaultValue ?? "comp";

            case "environment":
                // Try to detect environment from existing name
                var detectedEnv = NamingValidationHelpers.DetectEnvironmentFromName(resource.Name, GetEnvironmentPatterns(clientConfig));
                if (!string.IsNullOrEmpty(detectedEnv))
                    return detectedEnv;

                // Use first allowed value or default
                return component.AllowedValues.FirstOrDefault() ?? component.DefaultValue ?? "prod";

            case "service":
            case "application":
            case "service/application":
                // ENHANCED: Use ServiceAbbreviationMappings for better service detection
                var detectedService = ServiceAbbreviationMappings.ExtractServiceFromResourceName(
                    resource.Name, 
                    clientConfig.GetAcceptedCompanyNames(),
                    clientConfig.GetServiceAbbreviations());
                if (!string.IsNullOrEmpty(detectedService))
                    return detectedService;

                // Try legacy method as fallback
                var legacyService = NamingValidationHelpers.ExtractServiceFromResourceContext(resource.Name, resource.ResourceGroup, clientConfig.GetAcceptedCompanyNames());
                if (!string.IsNullOrEmpty(legacyService))
                    return legacyService;

                // Use component default value
                return component.DefaultValue ?? "app";

            case "resource-type":
                return AzureResourceAbbreviations.GetAbbreviationWithKind(resource.Type, resource.Kind);

            case "instance":
                // Try to extract instance number from existing name
                var instanceMatch = Regex.Match(resource.Name, @"(\d+)$");
                if (instanceMatch.Success)
                    return component.Format?.Contains("zero-padded") == true
                        ? instanceMatch.Value.PadLeft(2, '0')
                        : instanceMatch.Value;

                return component.Format?.Contains("zero-padded") == true ? "01" : "1";

            case "location":
                return NamingValidationHelpers.GetLocationAbbreviation(resource.Location);

            default:
                return component.DefaultValue ?? "comp";
        }
    }

    // ENHANCED: Analyze resource type abbreviation usage patterns
    private ResourceTypeUsageAnalysis AnalyzeResourceTypeAbbreviationUsage(List<AzureResource> resources)
    {
        var analysis = new ResourceTypeUsageAnalysis();
        var totalResourcesWithKnownTypes = 0;
        var resourcesUsingAbbreviations = 0;

        foreach (var resource in resources)
        {
            var resourceType = resource.Type.ToLowerInvariant();
            var abbreviations = AzureResourceAbbreviations.GetValidAbbreviations(resourceType);

            totalResourcesWithKnownTypes++;
            var nameToCheck = resource.Name.ToLowerInvariant();

            if (abbreviations.Any(abbr => nameToCheck.Contains(abbr)))
            {
                resourcesUsingAbbreviations++;
            }
        }

        analysis.OverallUsagePercentage = totalResourcesWithKnownTypes > 0
            ? Math.Round((decimal)resourcesUsingAbbreviations / totalResourcesWithKnownTypes * 100, 2)
            : 0m;

        return analysis;
    }

    private SeparatorAnalysis AnalyzeSeparatorConsistency(List<AzureResource> resources)
    {
        var separatorCounts = new Dictionary<string, int>();
        var resourcesWithSeparators = 0;

        foreach (var resource in resources)
        {
            var separator = NamingValidationHelpers.DetectPrimarySeparator(resource.Name);
            if (!string.IsNullOrEmpty(separator))
            {
                resourcesWithSeparators++;
                separatorCounts[separator] = separatorCounts.GetValueOrDefault(separator, 0) + 1;
            }
        }

        var primarySeparator = separatorCounts.Any()
            ? separatorCounts.OrderByDescending(kvp => kvp.Value).First()
            : new KeyValuePair<string, int>(string.Empty, 0);

        return new SeparatorAnalysis
        {
            PrimarySeparator = primarySeparator.Key,
            ConsistencyPercentage = resourcesWithSeparators > 0
                ? Math.Round((decimal)primarySeparator.Value / resourcesWithSeparators * 100, 2)
                : 100m
        };
    }

    // Standard analysis methods (keep existing implementation for backward compatibility)
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
            var pattern = NamingValidationHelpers.ClassifyNamingPattern(resource.Name);
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
                var pattern = NamingValidationHelpers.ClassifyNamingPattern(resource.Name);
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
        var commonEnvironments = new[] { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat", "shared" }.ToList();

        foreach (var resource in resources)
        {
            var detectedEnv = NamingValidationHelpers.DetectEnvironmentFromName(resource.Name, commonEnvironments);
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
            var abbreviation = AzureResourceAbbreviations.GetAbbreviationWithKind(resource.Type, resource.Kind);
            if (resource.Name.ToLowerInvariant().Contains(abbreviation))
            {
                prefixCount++;
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
            var pattern = NamingValidationHelpers.ClassifyNamingPattern(resource.Name);
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
            var basicIssues = NamingValidationHelpers.ValidateBasicRules(resource.Name);
            foreach (var issue in basicIssues)
            {
                yield return new NamingViolation
                {
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.Type,
                    ViolationType = "BasicValidation",
                    Issue = issue,
                    SuggestedName = GenerateBasicSuggestedName(resource),
                    Severity = issue.Contains("Invalid characters") ? "High" : "Medium"
                };
            }

            // Check for pattern inconsistency within resource type
            var resourceType = resource.Type.ToLowerInvariant();
            if (patternsByType.TryGetValue(resourceType, out var typeAnalysis) &&
                typeAnalysis.ConsistencyScore < 70) // Less than 70% consistency
            {
                var resourcePattern = NamingValidationHelpers.ClassifyNamingPattern(resource.Name);
                if (resourcePattern != typeAnalysis.MostCommonPattern)
                {
                    yield return new NamingViolation
                    {
                        ResourceId = resource.Id,
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        ViolationType = "InconsistentPattern",
                        Issue = $"Resource naming pattern '{resourcePattern}' doesn't match the most common pattern '{typeAnalysis.MostCommonPattern}' for this resource type",
                        SuggestedName = NamingValidationHelpers.ConvertToPattern(resource.Name, typeAnalysis.MostCommonPattern),
                        Severity = "Low"
                    };
                }
            }
        }
    }

    private string GenerateBasicSuggestedName(AzureResource resource)
    {
        var name = resource.Name.ToLowerInvariant();

        // Remove invalid characters
        name = Regex.Replace(name, @"[^a-zA-Z0-9\-_\.]", "");

        // Truncate if too long
        if (name.Length > 60)
        {
            name = name.Substring(0, 60);
        }

        // Add resource type prefix if missing
        var abbreviation = AzureResourceAbbreviations.GetAbbreviationWithKind(resource.Type, resource.Kind);
        if (!name.Contains(abbreviation) && !name.Contains("-"))
        {
            name = $"{abbreviation}-{name}";
        }

        return name;
    }

    // ENHANCED: Calculate flexible naming score
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
            v.ViolationType == "BasicValidation");

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

    // Helper methods for enhanced functionality
    private List<string> GetEnvironmentPatterns(ClientAssessmentConfiguration clientConfig)
    {
        var patterns = clientConfig.GetExpectedEnvironmentPatterns();
        if (patterns.Any())
            return patterns;

        return new[] { "dev", "test", "staging", "prod", "production", "qa", "uat" }.ToList();
    }

    // Helper classes for enhanced functionality
    private class StandardValidationResult
    {
        public List<string> Issues { get; set; } = new();
        public string SuggestedName { get; set; } = string.Empty;
    }

    private class ResourceTypeUsageAnalysis
    {
        public decimal OverallUsagePercentage { get; set; }
    }

    private class SeparatorAnalysis
    {
        public string? PrimarySeparator { get; set; }
        public decimal ConsistencyPercentage { get; set; }
    }

    private class NamingSchemeComplianceAnalysis
    {
        public decimal OverallCompliance { get; set; }
        public int CompliantResources { get; set; }
        public int TotalResources { get; set; }
    }
}