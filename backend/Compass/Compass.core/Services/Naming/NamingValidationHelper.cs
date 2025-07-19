using System.Text.RegularExpressions;

namespace Compass.Core.Services.Naming;

/// <summary>
/// Helper methods for naming convention validation and component classification
/// </summary>
public static class NamingValidationHelpers
{
    private static readonly string[] CommonEnvironments = { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat", "shared" };
    private static readonly string[] CommonSeparators = { "-", "_", "." };
    private static readonly string[] CommonWords = { "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by" };

    /// <summary>
    /// Classify what type of component a name part represents
    /// </summary>
    public static string ClassifyComponentType(string component, string resourceType, string? resourceKind = null)
    {
        var comp = component.ToLowerInvariant();

        // Check if it's a resource type abbreviation
        if (AzureResourceAbbreviations.IsValidAbbreviation(resourceType, comp))
            return "resource-type";

        // Check if it's an environment indicator
        if (CommonEnvironments.Contains(comp))
            return "environment";

        // Check for numeric patterns (instance numbers)
        if (Regex.IsMatch(comp, @"^\d{1,3}$") || Regex.IsMatch(comp, @"^[a-z]+\d{1,3}$"))
            return "instance";

        // Check if it looks like a company identifier (2-5 chars, not environment or resource type)
        if (comp.Length >= 2 && comp.Length <= 5 &&
            !CommonEnvironments.Contains(comp) &&
            !AzureResourceAbbreviations.IsKnownAbbreviation(comp))
        {
            return "company";
        }

        // Check if it could be a service/application name (longer identifiers)
        if (comp.Length > 3 &&
            !CommonEnvironments.Contains(comp) &&
            !AzureResourceAbbreviations.IsKnownAbbreviation(comp) &&
            !Regex.IsMatch(comp, @"^\d"))
        {
            return "service";
        }

        return string.Empty; // Unknown component type
    }

    /// <summary>
    /// Classify naming pattern for a resource name
    /// </summary>
    public static string ClassifyNamingPattern(string name)
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

    /// <summary>
    /// Detect the primary separator used in a name
    /// </summary>
    public static string? DetectPrimarySeparator(string name)
    {
        foreach (var separator in CommonSeparators)
        {
            if (name.Contains(separator))
                return separator;
        }
        return null;
    }

    /// <summary>
    /// Detect environment from name using provided patterns
    /// </summary>
    public static string? DetectEnvironmentFromName(string name, List<string> environmentPatterns)
    {
        var nameLower = name.ToLowerInvariant();
        return environmentPatterns.FirstOrDefault(env => nameLower.Contains(env.ToLowerInvariant()));
    }

    /// <summary>
    /// Extract company identifier from resource context
    /// </summary>
    public static string? ExtractCompanyFromResourceContext(string resourceName, string? resourceGroup = null)
    {
        // Try resource group first if available
        if (!string.IsNullOrEmpty(resourceGroup))
        {
            var rgParts = resourceGroup.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            var companyFromRG = rgParts.FirstOrDefault(p =>
                p.Length >= 2 && p.Length <= 5 &&
                !IsEnvironment(p) &&
                !AzureResourceAbbreviations.IsKnownAbbreviation(p));

            if (!string.IsNullOrEmpty(companyFromRG))
                return companyFromRG;
        }

        // Try resource name
        var parts = resourceName.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.FirstOrDefault(p =>
            p.Length >= 2 && p.Length <= 5 &&
            !IsEnvironment(p) &&
            !AzureResourceAbbreviations.IsKnownAbbreviation(p) &&
            !IsCommonWord(p));
    }

    /// <summary>
    /// Extract service name from resource context, avoiding company names
    /// </summary>
    public static string? ExtractServiceFromResourceContext(string resourceName, string? resourceGroup = null, List<string>? acceptedCompanyNames = null)
    {
        // Try resource group first
        if (!string.IsNullOrEmpty(resourceGroup))
        {
            var rgParts = resourceGroup.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            var servicePart = rgParts.FirstOrDefault(p =>
                p.Length > 2 &&
                !IsCommonWord(p) &&
                !IsEnvironment(p) &&
                !AzureResourceAbbreviations.IsKnownAbbreviation(p) &&
                !IsAcceptedCompanyName(p, acceptedCompanyNames));

            if (!string.IsNullOrEmpty(servicePart))
                return servicePart.ToLowerInvariant();
        }

        // Try original resource name but avoid company names
        var nameParts = resourceName.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

        var nameServicePart = nameParts.FirstOrDefault(p =>
            p.Length > 2 &&
            !IsCommonWord(p) &&
            !IsEnvironment(p) &&
            !AzureResourceAbbreviations.IsKnownAbbreviation(p) &&
            !Regex.IsMatch(p, @"^\d+$") &&
            !IsAcceptedCompanyName(p, acceptedCompanyNames));

        return nameServicePart?.ToLowerInvariant();
    }

    /// <summary>
    /// Check if a value is a valid environment indicator
    /// </summary>
    public static bool IsEnvironment(string value)
    {
        return CommonEnvironments.Contains(value.ToLowerInvariant());
    }

    /// <summary>
    /// Check if a word is a common word that shouldn't be used as a component
    /// </summary>
    public static bool IsCommonWord(string word)
    {
        return CommonWords.Contains(word.ToLowerInvariant());
    }

    /// <summary>
    /// Check if a value is in the accepted company names list
    /// </summary>
    public static bool IsAcceptedCompanyName(string value, List<string>? acceptedCompanyNames)
    {
        if (acceptedCompanyNames?.Any() != true)
            return false;

        return acceptedCompanyNames.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a value looks like a company identifier (short, not environment/resource type)
    /// </summary>
    public static bool IsLikelyCompanyIdentifier(string value)
    {
        return value.Length >= 2 && value.Length <= 5 &&
               !IsEnvironment(value) &&
               !AzureResourceAbbreviations.IsKnownAbbreviation(value) &&
               !IsCommonWord(value);
    }

    /// <summary>
    /// Get location abbreviation for common Azure regions
    /// </summary>
    public static string GetLocationAbbreviation(string? location)
    {
        if (string.IsNullOrEmpty(location))
            return "eus";

        var locationAbbreviations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["eastus"] = "eus",
            ["eastus2"] = "eus2",
            ["westus"] = "wus",
            ["westus2"] = "wus2",
            ["westus3"] = "wus3",
            ["centralus"] = "cus",
            ["northcentralus"] = "ncus",
            ["southcentralus"] = "scus",
            ["westcentralus"] = "wcus",
            ["canadacentral"] = "cac",
            ["canadaeast"] = "cae",
            ["northeurope"] = "neu",
            ["westeurope"] = "weu",
            ["uksouth"] = "uks",
            ["ukwest"] = "ukw",
            ["francecentral"] = "frc",
            ["germanywestcentral"] = "gwc",
            ["switzerlandnorth"] = "szn",
            ["norwayeast"] = "noe",
            ["southeastasia"] = "sea",
            ["eastasia"] = "ea",
            ["australiaeast"] = "aue",
            ["australiasoutheast"] = "ause",
            ["australiacentral"] = "auc",
            ["japaneast"] = "jpe",
            ["japanwest"] = "jpw",
            ["koreacentral"] = "krc",
            ["koreasouth"] = "krs",
            ["southindia"] = "ins",
            ["westindia"] = "inw",
            ["centralindia"] = "inc",
            ["brazilsouth"] = "brs",
            ["southafricanorth"] = "san",
            ["uaenorth"] = "uaen"
        };

        return locationAbbreviations.TryGetValue(location, out var abbr) ? abbr : "eus";
    }

    /// <summary>
    /// Convert name to specified pattern
    /// </summary>
    public static string ConvertToPattern(string name, string? targetPattern)
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

    /// <summary>
    /// Validate resource name against basic rules (length, characters, etc.)
    /// </summary>
    public static List<string> ValidateBasicRules(string resourceName)
    {
        var issues = new List<string>();

        if (string.IsNullOrEmpty(resourceName))
        {
            issues.Add("Resource name cannot be empty");
            return issues;
        }

        // Invalid characters
        if (Regex.IsMatch(resourceName, @"[^a-zA-Z0-9\-_\.]"))
        {
            issues.Add("Contains invalid characters");
        }

        // Length validation
        if (resourceName.Length > 63)
        {
            issues.Add("Name too long (>63 characters)");
        }

        if (resourceName.Length < 2)
        {
            issues.Add("Name too short (<2 characters)");
        }

        return issues;
    }
}