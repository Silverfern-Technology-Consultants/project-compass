using System.Text.Json;
using System.Text.RegularExpressions;
using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Core.Services.Naming;

namespace Compass.Core.Models.Assessment;

// ENHANCED CLIENT ASSESSMENT CONFIGURATION
public class ClientAssessmentConfiguration
{
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;

    // NEW: Reference to the full client preferences entity for accessing additional fields
    public Data.Entities.ClientPreferences? ClientPreferences { get; set; }

    // Legacy naming preferences (for backward compatibility)
    public List<string> AllowedNamingPatterns { get; set; } = new();
    public List<string> RequiredNamingElements { get; set; } = new();
    public bool EnvironmentIndicators { get; set; } = false;

    // Naming scheme configuration (the main enhancement)
    public NamingSchemeConfiguration? NamingScheme { get; set; }
    public List<ComponentDefinition> ComponentDefinitions { get; set; } = new();

    // Enhanced naming preferences
    public string? NamingStyle { get; set; }
    public string? EnvironmentSize { get; set; }
    public string? OrganizationMethod { get; set; }
    public string? EnvironmentIndicatorLevel { get; set; }

    // Tagging preferences
    public List<string> RequiredTags { get; set; } = new();
    public bool EnforceTagCompliance { get; set; } = true;
    public string? TaggingApproach { get; set; }
    public List<string> SelectedTags { get; set; } = new();
    public List<string> CustomTags { get; set; } = new();

    // Compliance preferences
    public List<string> ComplianceFrameworks { get; set; } = new();
    public List<string> SelectedCompliances { get; set; } = new();
    public bool NoSpecificRequirements { get; set; } = false;

    // Legacy properties (keep for backward compatibility)
    public Guid CustomerId { get; set; }
    public List<string> NamingConventions { get; set; } = new();
    public bool EnvironmentSeparationRequired { get; set; }
    public DateTime ConfigurationDate { get; set; } = DateTime.UtcNow;

    // Helper methods
    public bool HasNamingPreferences =>
        AllowedNamingPatterns.Any() ||
        RequiredNamingElements.Any() ||
        !string.IsNullOrEmpty(NamingStyle) ||
        NamingScheme?.Components.Any() == true;

    public bool HasTaggingPreferences =>
        RequiredTags.Any() ||
        SelectedTags.Any() ||
        !string.IsNullOrEmpty(TaggingApproach);

    public bool HasCompliancePreferences =>
        ComplianceFrameworks.Any() ||
        SelectedCompliances.Any();

    /// <summary>
    /// Factory method to create ClientAssessmentConfiguration from ClientPreferences entity with full reference
    /// </summary>
    public static ClientAssessmentConfiguration FromClientPreferences(Data.Entities.ClientPreferences preferences)
    {
        var config = new ClientAssessmentConfiguration
        {
            ClientId = preferences.ClientId,
            ClientName = preferences.Client?.Name ?? "Unknown Client",

            // NEW: Store reference to full ClientPreferences entity for accessing AcceptedCompanyNames
            ClientPreferences = preferences,

            // Legacy fields
            AllowedNamingPatterns = !string.IsNullOrEmpty(preferences.AllowedNamingPatterns)
                ? JsonSerializer.Deserialize<List<string>>(preferences.AllowedNamingPatterns) ?? new List<string>()
                : new List<string>(),
            RequiredNamingElements = !string.IsNullOrEmpty(preferences.RequiredNamingElements)
                ? JsonSerializer.Deserialize<List<string>>(preferences.RequiredNamingElements) ?? new List<string>()
                : new List<string>(),
            EnvironmentIndicators = preferences.EnvironmentIndicators,

            // Enhanced fields
            NamingStyle = preferences.NamingStyle,
            EnvironmentSize = preferences.EnvironmentSize,
            OrganizationMethod = preferences.OrganizationMethod,
            EnvironmentIndicatorLevel = preferences.EnvironmentIndicatorLevel,

            // Tagging fields
            RequiredTags = !string.IsNullOrEmpty(preferences.RequiredTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.RequiredTags) ?? new List<string>()
                : new List<string>(),
            EnforceTagCompliance = preferences.EnforceTagCompliance,
            TaggingApproach = preferences.TaggingApproach,
            SelectedTags = !string.IsNullOrEmpty(preferences.SelectedTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.SelectedTags) ?? new List<string>()
                : new List<string>(),
            CustomTags = !string.IsNullOrEmpty(preferences.CustomTags)
                ? JsonSerializer.Deserialize<List<string>>(preferences.CustomTags) ?? new List<string>()
                : new List<string>(),

            // Compliance fields
            ComplianceFrameworks = !string.IsNullOrEmpty(preferences.ComplianceFrameworks)
                ? JsonSerializer.Deserialize<List<string>>(preferences.ComplianceFrameworks) ?? new List<string>()
                : new List<string>(),
            SelectedCompliances = !string.IsNullOrEmpty(preferences.SelectedCompliances)
                ? JsonSerializer.Deserialize<List<string>>(preferences.SelectedCompliances) ?? new List<string>()
                : new List<string>(),
            NoSpecificRequirements = preferences.NoSpecificRequirements,

            // NEW: Naming scheme configuration
            NamingScheme = !string.IsNullOrEmpty(preferences.NamingSchemeConfiguration)
                ? JsonSerializer.Deserialize<NamingSchemeConfiguration>(preferences.NamingSchemeConfiguration)
                : null,
            ComponentDefinitions = !string.IsNullOrEmpty(preferences.ComponentDefinitions)
                ? JsonSerializer.Deserialize<List<ComponentDefinition>>(preferences.ComponentDefinitions) ?? new List<ComponentDefinition>()
                : new List<ComponentDefinition>(),

            // Legacy compatibility
            CustomerId = preferences.ClientId, // Map ClientId to CustomerId for legacy support
            EnvironmentSeparationRequired = preferences.EnvironmentIndicators,
            ConfigurationDate = preferences.CreatedDate
        };

        return config;
    }

    /// <summary>
    /// Validate a resource against the configured naming scheme
    /// </summary>
    public NamingSchemeValidationResult ValidateResourceName(AzureResource resource)
    {
        // If we have a custom naming scheme, use it
        if (NamingScheme?.Components.Any() == true)
        {
            return ValidateResourceAgainstScheme(resource);
        }

        // Fall back to legacy validation
        return ValidateResourceLegacy(resource);
    }

    /// <summary>
    /// ENHANCED: Validate an existing Azure resource name against the client's naming scheme with better component detection
    /// </summary>
    public NamingSchemeValidationResult ValidateResourceAgainstScheme(AzureResource resource)
    {
        if (NamingScheme?.Components.Any() != true)
        {
            return new NamingSchemeValidationResult
            {
                IsCompliant = true,
                Resource = resource,
                Message = "No naming scheme configured"
            };
        }

        var result = new NamingSchemeValidationResult
        {
            Resource = resource,
            DetectedComponents = new Dictionary<string, string>(),
            MissingComponents = new List<string>(),
            InvalidComponents = new List<string>()
        };

        // ENHANCED: Handle storage accounts without separators specially
        if (IsStorageAccountWithoutSeparators(resource))
        {
            return ValidateStorageAccountSpecialCase(resource, result, this);
        }

        // Parse the resource name using the configured separator
        var separator = NamingScheme.Separator;
        var nameParts = resource.Name.Split(separator.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var orderedComponents = NamingScheme.Components.OrderBy(c => c.Position).ToList();

        // ENHANCED: First pass - detect ALL component types present in the name (regardless of position)
        var detectedComponentTypes = new Dictionary<string, DetectedComponent>();

        // Scan all parts to identify what components exist
        for (int i = 0; i < nameParts.Length; i++)
        {
            var part = nameParts[i];
            var detectedType = IdentifyComponentType(part, resource.Type, resource.Kind, this);

            if (!string.IsNullOrEmpty(detectedType))
            {
                // If we already detected this component type, prefer the one in the expected position
                if (detectedComponentTypes.ContainsKey(detectedType))
                {
                    var expectedPosition = orderedComponents.FirstOrDefault(c => c.ComponentType == detectedType)?.Position ?? 0;
                    var currentPosition = i + 1;
                    var existingPosition = detectedComponentTypes[detectedType].ActualPosition;

                    // Keep the one closer to expected position
                    if (Math.Abs(currentPosition - expectedPosition) < Math.Abs(existingPosition - expectedPosition))
                    {
                        detectedComponentTypes[detectedType] = new DetectedComponent
                        {
                            Type = detectedType,
                            Value = part,
                            ActualPosition = currentPosition,
                            ExpectedPosition = expectedPosition
                        };
                    }
                }
                else
                {
                    detectedComponentTypes[detectedType] = new DetectedComponent
                    {
                        Type = detectedType,
                        Value = part,
                        ActualPosition = i + 1,
                        ExpectedPosition = orderedComponents.FirstOrDefault(c => c.ComponentType == detectedType)?.Position ?? 0
                    };
                }
            }
        }

        // ENHANCED: Second pass - validate positions and identify issues
        foreach (var component in orderedComponents)
        {
            if (detectedComponentTypes.TryGetValue(component.ComponentType, out var detected))
            {
                // Component exists but check if it's in the right position
                if (detected.ActualPosition != component.Position)
                {
                    result.InvalidComponents.Add(
                        $"{component.ComponentType}: '{detected.Value}' in wrong position ({detected.ActualPosition}), should be position {component.Position}");
                }
                else
                {
                    // Component is in correct position - validate the value
                    if (!ValidateComponentValue(component, detected.Value, resource.Type))
                    {
                        result.InvalidComponents.Add($"{component.ComponentType}: '{detected.Value}' invalid format");
                    }
                }

                result.DetectedComponents[component.ComponentType] = detected.Value;
            }
            else if (component.IsRequired)
            {
                result.MissingComponents.Add(component.ComponentType);
            }
        }

        // ENHANCED: Check for position mismatches at each position
        for (int i = 0; i < Math.Min(nameParts.Length, orderedComponents.Count); i++)
        {
            var part = nameParts[i];
            var expectedComponent = orderedComponents[i];
            var actualPosition = i + 1;

            var detectedType = IdentifyComponentType(part, resource.Type, resource.Kind, this);

            if (!string.IsNullOrEmpty(detectedType) && detectedType != expectedComponent.ComponentType)
            {
                result.InvalidComponents.Add(
                    $"Position {actualPosition}: found '{detectedType}' component but expected '{expectedComponent.ComponentType}'");
            }
            else if (string.IsNullOrEmpty(detectedType))
            {
                // Unknown component type at this position
                result.InvalidComponents.Add(
                    $"Position {actualPosition}: unrecognized component '{part}', expected '{expectedComponent.ComponentType}'");
            }
        }

        // Determine overall compliance
        result.IsCompliant = !result.MissingComponents.Any() && !result.InvalidComponents.Any();

        // Generate compliance message
        if (result.IsCompliant)
        {
            result.Message = "Complies with naming scheme";
        }
        else
        {
            var issues = new List<string>();
            if (result.MissingComponents.Any())
                issues.Add($"Missing: {string.Join(", ", result.MissingComponents)}");
            if (result.InvalidComponents.Any())
                issues.Add($"Issues: {string.Join("; ", result.InvalidComponents)}");

            result.Message = string.Join("; ", issues);
        }

        // ENHANCED: Generate better suggested name if not compliant
        if (!result.IsCompliant)
        {
            result.SuggestedName = GenerateImprovedCompliantName(resource, detectedComponentTypes, this);
        }

        return result;
    }

    /// <summary>
    /// Check if this is a storage account without separators
    /// </summary>
    private bool IsStorageAccountWithoutSeparators(AzureResource resource)
    {
        return resource.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts" &&
               !resource.Name.Contains("-") && !resource.Name.Contains("_") && !resource.Name.Contains(".");
    }

    /// <summary>
    /// STRICT: Identify component type - PRIORITIZE AcceptedCompanyNames over known services
    /// </summary>
    private string IdentifyComponentType(string part, string resourceType, string? resourceKind, ClientAssessmentConfiguration clientConfig)
    {
        var partLower = part.ToLowerInvariant();

        // 1. Check if it's a resource type abbreviation (using official Microsoft abbreviations)
        if (AzureResourceAbbreviations.IsValidAbbreviation(resourceType, partLower))
            return "resource-type";

        // 2. Check if it's an environment indicator (predefined list)
        var commonEnvironments = new[] { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat", "shared" };
        if (commonEnvironments.Contains(partLower))
            return "environment";

        // 3. PRIORITY: Check if it's a company name (ONLY from AcceptedCompanyNames) - CHECK FIRST
        var acceptedCompanies = clientConfig.GetAcceptedCompanyNames();
        if (acceptedCompanies.Any() && acceptedCompanies.Contains(partLower, StringComparer.OrdinalIgnoreCase))
            return "company";

        // 4. Check for numeric patterns (instance numbers)
        if (Regex.IsMatch(partLower, @"^\d{1,3}$") || Regex.IsMatch(partLower, @"^[a-z]+\d{1,3}$"))
            return "instance";

        // 5. ENHANCED: Check for known service names using ServiceAbbreviationMappings
        if (ServiceAbbreviationMappings.IsDefinitelyServiceName(partLower, acceptedCompanies))
            return "service";

        // 6. If longer than 3 chars and not in above categories, likely a service/application
        if (partLower.Length > 3 &&
            !commonEnvironments.Contains(partLower) &&
            !AzureResourceAbbreviations.IsKnownAbbreviation(partLower) &&
            !Regex.IsMatch(partLower, @"^\d"))
        {
            return "service";
        }

        return string.Empty; // Unknown component type - don't guess
    }

    /// <summary>
    /// Check if a part is a known service name (explicit list only) - EXCLUDE accepted company names
    /// </summary>
    private bool IsKnownServiceName(string part, List<string> acceptedCompanyNames)
    {
        // FIRST: Exclude if it's an accepted company name
        if (acceptedCompanyNames.Contains(part, StringComparer.OrdinalIgnoreCase))
            return false;

        // Explicit list of known service/application names - no guessing
        var knownServices = new[] {
            "compass", "assessments", "functions", "api", "web", "website",
            "admin", "portal", "dashboard", "backend", "frontend", "database", "cache",
            "storage", "backup", "monitoring", "logging"
        };

        return knownServices.Contains(part.ToLowerInvariant());
    }

    /// <summary>
    /// FIXED: Generate component value using correct company priority
    /// </summary>
    private string GenerateStrictComponentValue(NamingComponent component, AzureResource resource, string? existingValue, Dictionary<string, DetectedComponent> detectedComponents, ClientAssessmentConfiguration clientConfig)
    {
        switch (component.ComponentType.ToLowerInvariant())
        {
            case "company":
                // STRICT: Only use AcceptedCompanyNames from ClientPreferences
                var acceptedNames = clientConfig.GetAcceptedCompanyNames();
                if (acceptedNames.Any())
                {
                    // First try to find any accepted company name in the existing resource name
                    var detectedCompany = acceptedNames.FirstOrDefault(company =>
                        resource.Name.ToLowerInvariant().Contains(company.ToLowerInvariant()));

                    if (!string.IsNullOrEmpty(detectedCompany))
                        return detectedCompany.ToLowerInvariant();

                    // If none detected, use the FIRST accepted company name
                    return acceptedNames.First().ToLowerInvariant();
                }

                // FIXED: If no accepted company names configured, fallback to component default or "abc"
                return component.DefaultValue ?? "abc";

            case "environment":
                // Use detected environment or component configuration
                if (detectedComponents.TryGetValue("environment", out var detectedEnv))
                    return detectedEnv.Value;

                // Try to detect from resource context using predefined environments
                var envFromContext = DetectEnvironmentFromResourceContext(resource);
                if (!string.IsNullOrEmpty(envFromContext))
                    return envFromContext;

                // Use component configuration
                return component.AllowedValues.FirstOrDefault() ?? component.DefaultValue ?? "prod";

            case "service":
            case "application":
            case "service/application":
                // Use detected service or try to extract from context
                if (detectedComponents.TryGetValue("service", out var detectedService))
                    return detectedService.Value;

                // ENHANCED: Use ServiceAbbreviationMappings for better service extraction
                var serviceFromMappings = ServiceAbbreviationMappings.ExtractServiceFromResourceName(resource.Name, clientConfig.GetAcceptedCompanyNames());
                if (!string.IsNullOrEmpty(serviceFromMappings))
                    return serviceFromMappings;

                // Fallback to legacy extraction
                var serviceFromName = ExtractServiceFromResourceName(resource.Name, clientConfig);
                if (!string.IsNullOrEmpty(serviceFromName))
                    return serviceFromName;

                return component.DefaultValue ?? "app";

            case "resource-type":
                // Always use the correct official abbreviation
                return AzureResourceAbbreviations.GetAbbreviationWithKind(resource.Type, resource.Kind);

            case "instance":
                // Use detected instance or extract from existing name
                if (detectedComponents.TryGetValue("instance", out var detectedInstance))
                    return ExtractNumericPart(detectedInstance.Value);

                // Try to extract instance number from resource name
                var instanceFromName = ExtractInstanceFromResourceName(resource.Name);
                if (!string.IsNullOrEmpty(instanceFromName))
                {
                    return component.Format?.Contains("zero-padded") == true
                        ? instanceFromName.PadLeft(2, '0')
                        : instanceFromName;
                }

                return component.Format?.Contains("zero-padded") == true ? "01" : "1";

            case "location":
                return NamingValidationHelpers.GetLocationAbbreviation(resource.Location);

            default:
                return existingValue ?? component.DefaultValue ?? "comp";
        }
    }

    /// <summary>
    /// STRICT: Detect environment from resource context (no guessing)
    /// </summary>
    private string? DetectEnvironmentFromResourceContext(AzureResource resource)
    {
        var environments = new[] { "dev", "test", "staging", "prod", "production", "qa", "uat" };
        var contexts = new[] { resource.Name, resource.ResourceGroup ?? "" };

        foreach (var context in contexts)
        {
            var contextLower = context.ToLowerInvariant();
            var detectedEnv = environments.FirstOrDefault(env => contextLower.Contains(env));
            if (!string.IsNullOrEmpty(detectedEnv))
            {
                return detectedEnv == "production" ? "prod" : detectedEnv;
            }
        }

        return null;
    }

    /// <summary>
    /// FIXED: Extract service from resource name - exclude accepted company names
    /// </summary>
    private string? ExtractServiceFromResourceName(string resourceName, ClientAssessmentConfiguration clientConfig)
    {
        // Use the enhanced service detection first
        var detectedService = ServiceAbbreviationMappings.ExtractServiceFromResourceName(resourceName, clientConfig.GetAcceptedCompanyNames());
        if (!string.IsNullOrEmpty(detectedService))
            return detectedService;

        // Fallback to legacy logic for backward compatibility
        var nameParts = resourceName.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var acceptedCompanies = clientConfig.GetAcceptedCompanyNames();

        // Look for known service names first (but exclude company names)
        foreach (var part in nameParts)
        {
            if (IsKnownServiceName(part, acceptedCompanies))
                return part.ToLowerInvariant();
        }

        // Look for parts that are likely service names (longer, not env/resource type/company)
        var environments = new[] { "dev", "test", "staging", "prod", "production", "qa", "uat" };

        foreach (var part in nameParts)
        {
            if (part.Length > 3 &&
                !environments.Contains(part.ToLowerInvariant()) &&
                !AzureResourceAbbreviations.IsKnownAbbreviation(part) &&
                !acceptedCompanies.Contains(part, StringComparer.OrdinalIgnoreCase) &&
                !Regex.IsMatch(part, @"^\d+$"))
            {
                return part.ToLowerInvariant();
            }
        }

        return null;
    }

    /// <summary>
    /// Extract instance number from resource name
    /// </summary>
    private string? ExtractInstanceFromResourceName(string resourceName)
    {
        // Look for numbers at the end
        var instanceMatch = Regex.Match(resourceName, @"(\d+)$");
        if (instanceMatch.Success)
            return instanceMatch.Value;

        // Look for patterns like "compass001", "cmp001", etc.
        var alphaNumericMatch = Regex.Match(resourceName, @"([a-z]+)(\d+)$");
        if (alphaNumericMatch.Success)
            return alphaNumericMatch.Groups[2].Value;

        return null;
    }

    /// <summary>
    /// Extract numeric part from a component value
    /// </summary>
    private string ExtractNumericPart(string value)
    {
        var match = Regex.Match(value, @"(\d+)");
        return match.Success ? match.Value : "01";
    }

    /// <summary>
    /// STRICT: Updated suggestion generation using non-guessing logic
    /// </summary>
    private string GenerateImprovedCompliantName(AzureResource resource, Dictionary<string, DetectedComponent> detectedComponents, ClientAssessmentConfiguration clientConfig)
    {
        var orderedComponents = NamingScheme?.Components.OrderBy(c => c.Position).ToList() ?? new List<NamingComponent>();
        var nameParts = new List<string>();
        var separator = NamingScheme?.Separator ?? "-";

        foreach (var component in orderedComponents)
        {
            string value;

            if (detectedComponents.TryGetValue(component.ComponentType, out var detected))
            {
                // Use the detected value if it's valid, otherwise generate using strict logic
                if (ValidateComponentValue(component, detected.Value, resource.Type))
                {
                    value = detected.Value;
                }
                else
                {
                    value = GenerateStrictComponentValue(component, resource, detected.Value, detectedComponents, clientConfig);
                }
            }
            else
            {
                // Component is missing - generate using strict logic
                value = GenerateStrictComponentValue(component, resource, null, detectedComponents, clientConfig);
            }

            nameParts.Add(value);
        }

        // Special handling for storage accounts (no separators allowed)
        if (resource.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts")
        {
            return string.Join("", nameParts);
        }

        return string.Join(separator, nameParts);
    }
    private void DetectStorageAccountComponents(string name, ClientAssessmentConfiguration clientConfig, NamingSchemeValidationResult result)
    {
        // Try to detect any recognizable components in the storage account name
        var acceptedCompanies = clientConfig.GetAcceptedCompanyNames();
        var environments = new[] { "dev", "prod", "test", "staging", "qa", "uat" };
        var validStorageAbbrevs = AzureResourceAbbreviations.GetValidAbbreviations("microsoft.storage/storageaccounts");

        // Check for company names
        foreach (var company in acceptedCompanies)
        {
            if (name.Contains(company.ToLowerInvariant()))
            {
                result.DetectedComponents["company"] = company.ToLowerInvariant();
                break;
            }
        }

        // Check for environments
        foreach (var env in environments)
        {
            if (name.Contains(env))
            {
                result.DetectedComponents["environment"] = env;
                break;
            }
        }

        // Check for resource type abbreviations
        foreach (var abbrev in validStorageAbbrevs)
        {
            if (name.Contains(abbrev))
            {
                result.DetectedComponents["resource-type"] = abbrev;
                break;
            }
        }

        // Check for instance numbers
        var instanceMatch = System.Text.RegularExpressions.Regex.Match(name, @"(\d+)");
        if (instanceMatch.Success)
        {
            result.DetectedComponents["instance"] = instanceMatch.Value;
        }
    }

    private List<string> GetMissingRequiredComponents(NamingSchemeValidationResult result, ClientAssessmentConfiguration clientConfig)
    {
        var missing = new List<string>();
        var requiredComponents = clientConfig.NamingScheme?.Components.Where(c => c.IsRequired) ?? new List<NamingComponent>();

        foreach (var component in requiredComponents)
        {
            if (!result.DetectedComponents.ContainsKey(component.ComponentType))
            {
                missing.Add(component.ComponentType);
            }
        }

        return missing;
    }

    private List<string> ValidateComponentPositions(NamingSchemeValidationResult result, ClientAssessmentConfiguration clientConfig)
    {
        var issues = new List<string>();
        var orderedComponents = clientConfig.NamingScheme?.Components.OrderBy(c => c.Position).ToList();

        if (orderedComponents == null) return issues;

        // For storage accounts without separators, position validation is more complex
        // This is a simplified version - you may want to enhance this based on your needs

        return issues; // Return empty for now, as position validation for concatenated names is complex
    }
    private NamingSchemeValidationResult ValidateStorageAccountSpecialCase(AzureResource resource, NamingSchemeValidationResult result, ClientAssessmentConfiguration clientConfig)
    {
        var name = resource.Name.ToLowerInvariant();
        var issues = new List<string>();

        // Parse according to client's naming scheme (not resource-type-first)
        if (TryParseStorageAccountByClientScheme(name, clientConfig, result))
        {
            // Successfully parsed according to client naming scheme
            var missingComponents = GetMissingRequiredComponents(result, clientConfig);
            if (missingComponents.Any())
            {
                issues.Add($"Missing: {string.Join(", ", missingComponents)}");
            }

            // Check for component position issues
            var positionIssues = ValidateComponentPositions(result, clientConfig);
            if (positionIssues.Any())
            {
                issues.AddRange(positionIssues);
            }
        }
        else
        {
            // Fallback: try to detect any components possible
            DetectStorageAccountComponents(name, clientConfig, result);
            issues.Add("Storage account name doesn't follow client naming scheme - components detected where possible");
        }

        // Storage accounts are non-compliant if they don't have all required components in correct positions
        result.IsCompliant = !issues.Any();
        result.InvalidComponents.AddRange(issues);
        result.Message = string.Join("; ", issues);

        // Generate a suggested name based on client scheme
        result.SuggestedName = GenerateSchemeCompliantStorageAccountName(resource, clientConfig, result);

        return result;
    }

    // Parse storage account according to client's naming scheme order
    private bool TryParseStorageAccountByClientScheme(string name, ClientAssessmentConfiguration clientConfig, NamingSchemeValidationResult result)
    {
        var orderedComponents = clientConfig.NamingScheme?.Components.OrderBy(c => c.Position).ToList();
        if (orderedComponents == null) return false;

        var remainingName = name;
        var position = 0;

        foreach (var component in orderedComponents)
        {
            if (string.IsNullOrEmpty(remainingName)) break;

            var detectedValue = DetectComponentInStorageAccountName(remainingName, component, clientConfig);
            if (!string.IsNullOrEmpty(detectedValue))
            {
                result.DetectedComponents[component.ComponentType] = detectedValue;

                // Remove the detected component from the remaining name
                var index = remainingName.IndexOf(detectedValue);
                if (index >= 0)
                {
                    remainingName = remainingName.Substring(index + detectedValue.Length);
                }
            }
        }

        return result.DetectedComponents.Any();
    }

    // Detect specific component in storage account name
    private string? DetectComponentInStorageAccountName(string remainingName, NamingComponent component, ClientAssessmentConfiguration clientConfig)
    {
        switch (component.ComponentType.ToLowerInvariant())
        {
            case "company":
                var companies = clientConfig.GetAcceptedCompanyNames();
                return companies.FirstOrDefault(company =>
                    remainingName.StartsWith(company.ToLowerInvariant()));

            case "environment":
                var environments = new[] { "dev", "prod", "test", "staging", "qa", "uat" };
                return environments.FirstOrDefault(env =>
                    remainingName.StartsWith(env));

            case "service":
                // Try to detect service abbreviations
                var serviceAbbrevs = ServiceAbbreviationMappings.GetAllServiceAbbreviations().Keys;
                return serviceAbbrevs.FirstOrDefault(abbr =>
                    remainingName.StartsWith(abbr.ToLowerInvariant()));

            case "resource-type":
                var validAbbreviations = AzureResourceAbbreviations.GetValidAbbreviations("microsoft.storage/storageaccounts");
                return validAbbreviations.FirstOrDefault(abbr =>
                    remainingName.StartsWith(abbr));

            case "instance":
                var instanceMatch = System.Text.RegularExpressions.Regex.Match(remainingName, @"^(\d+)");
                return instanceMatch.Success ? instanceMatch.Value : null;

            default:
                return null;
        }
    }

    // Generate compliant storage account name based on client scheme
    private string GenerateSchemeCompliantStorageAccountName(AzureResource resource, ClientAssessmentConfiguration clientConfig, NamingSchemeValidationResult result)
    {
        var orderedComponents = clientConfig.NamingScheme?.Components.OrderBy(c => c.Position).ToList();
        if (orderedComponents == null) return resource.Name.ToLowerInvariant();

        var nameParts = new List<string>();

        foreach (var component in orderedComponents)
        {
            string value;

            if (result.DetectedComponents.TryGetValue(component.ComponentType, out var detectedValue))
            {
                value = detectedValue;
            }
            else
            {
                // Generate missing component value
                value = GenerateComponentValueForStorageAccount(component, resource, clientConfig);
            }

            nameParts.Add(value);
        }

        // Storage accounts can't have separators, so concatenate
        return string.Join("", nameParts);
    }

    private string GenerateComponentValueForStorageAccount(NamingComponent component, AzureResource resource, ClientAssessmentConfiguration clientConfig)
    {
        switch (component.ComponentType.ToLowerInvariant())
        {
            case "company":
                var companies = clientConfig.GetAcceptedCompanyNames();
                return companies.FirstOrDefault()?.ToLowerInvariant() ?? "comp";

            case "environment":
                return component.AllowedValues.FirstOrDefault() ?? component.DefaultValue ?? "prod";

            case "service":
                return component.DefaultValue ?? "app";

            case "resource-type":
                // FIXED: Check if "stg" is already present in the resource name
                var resourceName = resource.Name.ToLowerInvariant();
                var validStorageAbbrevs = AzureResourceAbbreviations.GetValidAbbreviations("microsoft.storage/storageaccounts");

                // If any valid storage abbreviation is already in the name, use it
                var existingAbbrev = validStorageAbbrevs.FirstOrDefault(abbr => resourceName.Contains(abbr));
                if (!string.IsNullOrEmpty(existingAbbrev))
                {
                    return existingAbbrev;
                }

                // Default to "stg" if no abbreviation found
                return "stg";

            case "instance":
                return "01";

            default:
                return component.DefaultValue ?? "default";
        }
    }

    private string GenerateStrictStorageAccountName(AzureResource resource, ClientAssessmentConfiguration clientConfig)
    {
        var orderedComponents = NamingScheme?.Components.OrderBy(c => c.Position).ToList() ?? new List<NamingComponent>();
        var nameParts = new List<string>();

        foreach (var component in orderedComponents)
        {
            var value = component.ComponentType.ToLowerInvariant() switch
            {
                "company" => GetStrictCompanyName(resource, clientConfig),
                "environment" => DetectEnvironmentFromResourceContext(resource) ?? "prod",
                "service" => ExtractServiceFromResourceName(resource.Name, clientConfig) ?? "storage",
                "resource-type" => "st",
                "instance" => ExtractInstanceFromResourceName(resource.Name) ?? "01",
                _ => component.DefaultValue ?? "comp"
            };
            nameParts.Add(value);
        }

        // Storage accounts require no separators and have length limits
        var result = string.Join("", nameParts);

        // Azure storage account names must be 3-24 characters, lowercase letters and numbers only
        if (result.Length > 24)
        {
            // Truncate while preserving the most important parts (company, env, resource type)
            var important = new List<string>();
            foreach (var component in orderedComponents.Take(3)) // Take first 3 most important
            {
                var value = component.ComponentType.ToLowerInvariant() switch
                {
                    "company" => GetStrictCompanyName(resource, clientConfig),
                    "environment" => DetectEnvironmentFromResourceContext(resource) ?? "prod",
                    "service" => "st", // Use short service name for storage
                    "resource-type" => "st",
                    "instance" => "01",
                    _ => component.DefaultValue ?? "comp"
                };
                important.Add(value);
            }
            result = string.Join("", important);

            // If still too long, truncate further
            if (result.Length > 24)
            {
                result = result.Substring(0, 24);
            }
        }

        return result;
    }

    /// <summary>
    /// Get company name using strict AcceptedCompanyNames only
    /// </summary>
    private string GetStrictCompanyName(AzureResource resource, ClientAssessmentConfiguration clientConfig)
    {
        var acceptedNames = clientConfig.GetAcceptedCompanyNames();
        if (acceptedNames.Any())
        {
            // Try to find any accepted company name in the resource name
            var detectedCompany = acceptedNames.FirstOrDefault(company =>
                resource.Name.ToLowerInvariant().Contains(company.ToLowerInvariant()));

            if (!string.IsNullOrEmpty(detectedCompany))
                return detectedCompany.ToLowerInvariant();

            // If none detected, use the first accepted company name
            return acceptedNames.First().ToLowerInvariant();
        }

        // FIXED: Fallback to "abc" if no accepted company names configured
        return "abc";
    }

    /// <summary>
    /// Helper class for detected component information
    /// </summary>
    private class DetectedComponent
    {
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int ActualPosition { get; set; }
        public int ExpectedPosition { get; set; }
    }

    /// <summary>
    /// Legacy validation for backward compatibility
    /// </summary>
    private NamingSchemeValidationResult ValidateResourceLegacy(AzureResource resource)
    {
        var result = new NamingSchemeValidationResult
        {
            Resource = resource,
            IsCompliant = true,
            Message = "Using legacy validation"
        };

        // Check allowed naming patterns
        if (AllowedNamingPatterns.Any())
        {
            var resourcePattern = ClassifyNamingPattern(resource.Name);
            if (!AllowedNamingPatterns.Contains(resourcePattern))
            {
                result.IsCompliant = false;
                result.Message = $"Naming pattern '{resourcePattern}' not in allowed patterns: {string.Join(", ", AllowedNamingPatterns)}";
            }
        }

        // Check required naming elements
        if (RequiredNamingElements.Contains("Environment indicator") && AreEnvironmentIndicatorsRequired())
        {
            var hasEnvIndicator = new[] { "dev", "test", "prod", "staging" }
                .Any(env => resource.Name.ToLowerInvariant().Contains(env));

            if (!hasEnvIndicator)
            {
                result.IsCompliant = false;
                result.Message = "Missing required environment indicator";
            }
        }

        return result;
    }

    /// <summary>
    /// Classify naming pattern for legacy support
    /// </summary>
    private string ClassifyNamingPattern(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Other";

        if (name.Contains("_")) return "Snake_case";
        if (name.Contains("-")) return "Kebab-case";
        if (name.All(c => !char.IsLetter(c) || char.IsUpper(c))) return "Uppercase";
        if (name.All(c => !char.IsLetter(c) || char.IsLower(c))) return "Lowercase";
        if (char.IsUpper(name[0]) && name.Any(char.IsUpper)) return "PascalCase";
        if (char.IsLower(name[0]) && name.Any(char.IsUpper)) return "CamelCase";

        return "Other";
    }

    /// <summary>
    /// Get effective required tags combining legacy and enhanced settings
    /// </summary>
    public List<string> GetEffectiveRequiredTags()
    {
        var effectiveTags = new List<string>();

        // Add legacy required tags
        effectiveTags.AddRange(RequiredTags);

        // Add enhanced selected tags
        effectiveTags.AddRange(SelectedTags);

        // Add custom tags
        effectiveTags.AddRange(CustomTags);

        return effectiveTags.Distinct().ToList();
    }

    /// <summary>
    /// Get effective naming patterns combining legacy and enhanced settings
    /// </summary>
    public List<string> GetEffectiveNamingPatterns()
    {
        var effectivePatterns = new List<string>();

        // Add explicit allowed patterns
        effectivePatterns.AddRange(AllowedNamingPatterns);

        // Add legacy naming conventions
        effectivePatterns.AddRange(NamingConventions);

        // Infer patterns from naming style
        if (!string.IsNullOrEmpty(NamingStyle))
        {
            switch (NamingStyle.ToLowerInvariant())
            {
                case "standardized":
                    effectivePatterns.AddRange(new[] { "Kebab-case", "Lowercase" });
                    break;
                case "legacy":
                    effectivePatterns.AddRange(new[] { "Other", "Uppercase", "Lowercase" });
                    break;
                case "mixed":
                    // Allow multiple patterns for mixed environments
                    break;
            }
        }

        return effectivePatterns.Distinct().ToList();
    }

    /// <summary>
    /// Check if environment indicators are required based on preferences
    /// </summary>
    public bool AreEnvironmentIndicatorsRequired()
    {
        return EnvironmentIndicators ||
               EnvironmentSeparationRequired || // Legacy
               EnvironmentIndicatorLevel == "required" ||
               RequiredNamingElements.Contains("Environment indicator");
    }

    /// <summary>
    /// Get the strictness level for compliance checking
    /// </summary>
    public string GetComplianceStrictnessLevel()
    {
        if (NoSpecificRequirements) return "none";
        if (SelectedCompliances.Any(c => c.Contains("SOC") || c.Contains("PCI") || c.Contains("HIPAA"))) return "high";
        if (ComplianceFrameworks.Any(c => c.Contains("SOC") || c.Contains("PCI") || c.Contains("HIPAA"))) return "high";
        if (SelectedCompliances.Any() || ComplianceFrameworks.Any()) return "medium";
        return "low";
    }

    /// <summary>
    /// Get tagging enforcement level based on client preferences
    /// </summary>
    public string GetTaggingEnforcementLevel()
    {
        if (!EnforceTagCompliance) return "none";

        return TaggingApproach?.ToLowerInvariant() switch
        {
            "comprehensive" => "strict",
            "basic" => "moderate",
            "minimal" => "light",
            "custom" => "custom",
            _ => "moderate"
        };
    }

    /// <summary>
    /// Get expected environment patterns based on organization method
    /// </summary>
    public List<string> GetExpectedEnvironmentPatterns()
    {
        var patterns = new List<string>();

        if (AreEnvironmentIndicatorsRequired())
        {
            switch (OrganizationMethod?.ToLowerInvariant())
            {
                case "environment":
                    patterns.AddRange(new[] { "dev", "test", "staging", "prod", "production" });
                    break;
                case "application":
                    patterns.AddRange(new[] { "app", "web", "api", "db", "cache" });
                    break;
                case "business-unit":
                    patterns.AddRange(new[] { "hr", "finance", "ops", "sales", "marketing" });
                    break;
                default:
                    patterns.AddRange(new[] { "dev", "test", "prod" });
                    break;
            }
        }

        return patterns;
    }

    /// <summary>
    /// Get severity adjustment for violations based on client preferences
    /// </summary>
    public string AdjustViolationSeverity(string baseSeverity, string violationType)
    {
        // Increase severity for client preference violations
        if (violationType.Contains("ClientPreference") || violationType.Contains("RequiredElement"))
        {
            return baseSeverity switch
            {
                "Low" => "Medium",
                "Medium" => "High",
                "High" => "Critical",
                _ => baseSeverity
            };
        }

        // Adjust based on compliance strictness
        var strictnessLevel = GetComplianceStrictnessLevel();
        if (strictnessLevel == "high" && (violationType.Contains("Tag") || violationType.Contains("Naming")))
        {
            return baseSeverity switch
            {
                "Low" => "Medium",
                "Medium" => "High",
                _ => baseSeverity
            };
        }

        return baseSeverity;
    }

    /// <summary>
    /// Generate client-specific recommendations based on preferences
    /// </summary>
    public List<string> GenerateClientSpecificRecommendations(string category)
    {
        var recommendations = new List<string>();

        switch (category.ToLowerInvariant())
        {
            case "naming":
            case "namingconvention":
                if (NamingScheme?.Components.Any() == true)
                {
                    var components = string.Join(" → ", NamingScheme.Components.OrderBy(c => c.Position).Select(c => c.ComponentType));
                    recommendations.Add($"Apply client naming scheme: {components}");

                    if (NamingScheme.Examples.Any())
                    {
                        var example = NamingScheme.Examples.FirstOrDefault();
                        recommendations.Add($"Example: {example?.ExampleName}");
                    }
                }
                else if (HasNamingPreferences)
                {
                    var patterns = GetEffectiveNamingPatterns();
                    if (patterns.Any())
                    {
                        recommendations.Add($"Apply client-preferred naming patterns: {string.Join(", ", patterns)}");
                    }

                    if (AreEnvironmentIndicatorsRequired())
                    {
                        var envPatterns = GetExpectedEnvironmentPatterns();
                        recommendations.Add($"Include environment indicators: {string.Join(", ", envPatterns)}");
                    }
                }
                break;

            case "tagging":
                if (HasTaggingPreferences)
                {
                    var requiredTags = GetEffectiveRequiredTags();
                    if (requiredTags.Any())
                    {
                        recommendations.Add($"Apply client-required tags: {string.Join(", ", requiredTags.Take(5))}");
                    }

                    var enforcementLevel = GetTaggingEnforcementLevel();
                    if (enforcementLevel != "none")
                    {
                        recommendations.Add($"Follow {enforcementLevel} tagging enforcement as per client preferences");
                    }
                }
                break;

            case "compliance":
                if (HasCompliancePreferences && !NoSpecificRequirements)
                {
                    if (SelectedCompliances.Any() || ComplianceFrameworks.Any())
                    {
                        var allCompliances = SelectedCompliances.Concat(ComplianceFrameworks).Distinct();
                        recommendations.Add($"Ensure compliance with: {string.Join(", ", allCompliances)}");
                    }

                    var strictness = GetComplianceStrictnessLevel();
                    recommendations.Add($"Apply {strictness} compliance standards as specified by client");
                }
                break;
        }

        return recommendations;
    }

    /// <summary>
    /// Generate examples based on the configured naming scheme
    /// </summary>
    public List<NamingSchemeExample> GenerateNamingExamples()
    {
        if (NamingScheme?.Components.Any() != true)
            return new List<NamingSchemeExample>();

        var examples = new List<NamingSchemeExample>();
        var commonResourceTypes = new[]
        {
            "microsoft.compute/virtualmachines",
            "microsoft.storage/storageaccounts",
            "microsoft.keyvault/vaults",
            "microsoft.network/virtualnetworks",
            "microsoft.web/sites"
        };

        foreach (var resourceType in commonResourceTypes)
        {
            var example = GenerateExampleForResourceType(resourceType);
            examples.Add(example);
        }

        return examples;
    }

    /// <summary>
    /// Generate a single naming example for a specific resource type
    /// </summary>
    public NamingSchemeExample GenerateExampleForResourceType(string resourceType)
    {
        var example = new NamingSchemeExample
        {
            ResourceType = resourceType,
            ComponentValues = new Dictionary<string, string>()
        };

        var nameParts = new List<string>();

        // Build name based on component order
        var orderedComponents = NamingScheme?.Components.OrderBy(c => c.Position).ToList() ?? new List<NamingComponent>();

        foreach (var component in orderedComponents)
        {
            var value = GenerateComponentValue(component, resourceType);
            example.ComponentValues[component.ComponentType] = value;
            nameParts.Add(value);
        }

        example.ExampleName = string.Join(NamingScheme?.Separator ?? "-", nameParts);

        // Validate the generated example
        var validationResult = ValidateGeneratedName(example.ExampleName, resourceType);
        example.IsValid = validationResult.IsValid;
        example.ValidationMessages = validationResult.Messages;

        return example;
    }

    /// <summary>
    /// Generate a component value based on component type and resource type
    /// </summary>
    private string GenerateComponentValue(NamingComponent component, string resourceType)
    {
        switch (component.ComponentType.ToLowerInvariant())
        {
            case "company":
                return component.DefaultValue?.ToLowerInvariant() ?? "abc";

            case "environment":
                var envValue = component.AllowedValues.FirstOrDefault() ?? component.DefaultValue ?? "prod";
                return envValue.ToLowerInvariant();

            case "service":
            case "application":
            case "service/application":
                return component.DefaultValue?.ToLowerInvariant() ?? "app";

            case "resource-type":
                return AzureResourceAbbreviations.GetAbbreviationWithKind(resourceType, null);

            case "instance":
                return component.Format?.Contains("zero-padded") == true ? "01" : "1";

            default:
                return component.DefaultValue?.ToLowerInvariant() ?? "comp";
        }
    }

    /// <summary>
    /// Validate a generated name against the naming scheme rules
    /// </summary>
    public NamingValidationResult ValidateGeneratedName(string name, string resourceType)
    {
        var result = new NamingValidationResult { IsValid = true, Messages = new List<string>() };

        // Basic validation
        if (string.IsNullOrEmpty(name))
        {
            result.IsValid = false;
            result.Messages.Add("Name cannot be empty");
            return result;
        }

        // Length validation
        if (name.Length > 63)
        {
            result.IsValid = false;
            result.Messages.Add("Name exceeds maximum length of 63 characters");
        }

        // Separator validation
        var expectedSeparator = NamingScheme?.Separator ?? "-";
        if (!string.IsNullOrEmpty(expectedSeparator) && !name.Contains(expectedSeparator))
        {
            result.Messages.Add($"Name should use '{expectedSeparator}' as separator");
        }

        // Component count validation
        var parts = name.Split((NamingScheme?.Separator ?? "-").ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        var requiredComponents = NamingScheme?.Components.Count(c => c.IsRequired) ?? 0;

        if (parts.Length < requiredComponents)
        {
            result.IsValid = false;
            result.Messages.Add($"Name has {parts.Length} components but {requiredComponents} are required");
        }

        return result;
    }

    /// <summary>
    /// Validate a single component value
    /// </summary>
    private bool ValidateComponentValue(NamingComponent component, string value, string resourceType)
    {
        var rules = component.ValidationRules;

        // Length validation
        if (rules.MinLength.HasValue && value.Length < rules.MinLength.Value) return false;
        if (rules.MaxLength.HasValue && value.Length > rules.MaxLength.Value) return false;

        // Pattern validation
        if (!string.IsNullOrEmpty(rules.Pattern) && !Regex.IsMatch(value, rules.Pattern)) return false;

        // Allowed values validation
        if (component.AllowedValues.Any() && !component.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            return false;

        // Reserved words check
        if (rules.ReservedWords.Contains(value, StringComparer.OrdinalIgnoreCase)) return false;

        // Special logic for resource-type component
        if (component.ComponentType == "resource-type")
        {
            var expectedAbbreviation = AzureResourceAbbreviations.GetAbbreviationWithKind(resourceType, null);
            return value.Equals(expectedAbbreviation, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }
}

public static class ClientAssessmentConfigurationExtensions
{
    /// <summary>
    /// Get the list of accepted company names for validation from ClientPreferences
    /// </summary>
    public static List<string> GetAcceptedCompanyNames(this ClientAssessmentConfiguration config)
    {
        // Check if we have client preferences with accepted company names
        if (config.ClientPreferences?.AcceptedCompanyNames == null)
            return new List<string>();

        try
        {
            var acceptedNames = JsonSerializer.Deserialize<List<string>>(config.ClientPreferences.AcceptedCompanyNames);
            return acceptedNames?.Where(name => !string.IsNullOrWhiteSpace(name)).ToList() ?? new List<string>();
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return empty list
            return new List<string>();
        }
    }

    /// <summary>
    /// Check if a company name is in the accepted list
    /// </summary>
    public static bool IsAcceptedCompanyName(this ClientAssessmentConfiguration config, string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return false;

        var acceptedNames = config.GetAcceptedCompanyNames();
        if (!acceptedNames.Any())
            return true; // If no restrictions specified, allow any reasonable name

        return acceptedNames.Contains(companyName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validate if a resource name contains an accepted company identifier
    /// </summary>
    public static bool HasValidCompanyIdentifier(this ClientAssessmentConfiguration config, string resourceName)
    {
        var acceptedNames = config.GetAcceptedCompanyNames();
        if (!acceptedNames.Any())
            return true; // No restrictions specified

        var nameLower = resourceName.ToLowerInvariant();
        return acceptedNames.Any(name => nameLower.Contains(name.ToLowerInvariant()));
    }

    /// <summary>
    /// Get the most likely company identifier from a resource name
    /// </summary>
    public static string? ExtractCompanyIdentifier(this ClientAssessmentConfiguration config, string resourceName)
    {
        var acceptedNames = config.GetAcceptedCompanyNames();
        if (!acceptedNames.Any())
            return null;

        var nameLower = resourceName.ToLowerInvariant();
        return acceptedNames.FirstOrDefault(name => nameLower.Contains(name.ToLowerInvariant()));
    }

    /// <summary>
    /// Generate suggested company name for corrections
    /// </summary>
    public static string GetSuggestedCompanyName(this ClientAssessmentConfiguration config)
    {
        var acceptedNames = config.GetAcceptedCompanyNames();
        return acceptedNames.FirstOrDefault() ?? "abc";
    }

    /// <summary>
    /// Get the list of service abbreviations from ClientPreferences (Phase 1 - Service Abbreviations Feature)
    /// </summary>
    public static List<object> GetServiceAbbreviations(this ClientAssessmentConfiguration config)
    {
        // Check if we have client preferences with service abbreviations
        if (config.ClientPreferences?.ServiceAbbreviations == null)
            return new List<object>();

        try
        {
            var serviceAbbreviations = JsonSerializer.Deserialize<List<object>>(config.ClientPreferences.ServiceAbbreviations);
            return serviceAbbreviations ?? new List<object>();
        }
        catch (JsonException)
        {
            // If JSON parsing fails, return empty list
            return new List<object>();
        }
    }
}