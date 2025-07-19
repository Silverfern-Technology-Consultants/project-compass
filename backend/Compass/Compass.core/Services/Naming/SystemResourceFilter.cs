using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Compass.Core.Services.Naming;

/// <summary>
/// Filters out Azure resources that are system-generated and cannot be renamed
/// </summary>
public static class SystemResourceFilter
{
    /// <summary>
    /// Resource types that CANNOT be renamed (system-generated names)
    /// </summary>
    private static readonly HashSet<string> SystemGeneratedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "microsoft.managedidentity/userassignedidentities", // System generates random IDs
        "microsoft.storage/storageaccounts/blobservices", // System services
        "microsoft.storage/storageaccounts/fileservices", // System services
        "microsoft.storage/storageaccounts/queueservices", // System services
        "microsoft.storage/storageaccounts/tableservices", // System services
        "microsoft.authorization/roleassignments", // System-generated
        "microsoft.authorization/roledefinitions", // System-defined
        "microsoft.resources/deployments", // Deployment artifacts
        "microsoft.resources/providers", // Azure providers
        "microsoft.insights/diagnosticsettings", // Often auto-generated
        "microsoft.security/assessments", // Security Center assessments
        "microsoft.security/pricings", // Security Center pricing tiers
    };

    /// <summary>
    /// System-generated resource name patterns to skip
    /// </summary>
    private static readonly List<Regex> SystemNamePatterns = new()
    {
        // Default workspaces with UUIDs
        new Regex(@"^DefaultWorkspace-[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}.*$", RegexOptions.IgnoreCase),
        
        // Application Insights Smart Detection (exact match)
        new Regex(@"^Application Insights Smart Detection$", RegexOptions.IgnoreCase),
        
        // Pure UUID names
        new Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase),
        
        // Auto-generated identity suffixes
        new Regex(@"-id-[0-9a-f]{4,}$", RegexOptions.IgnoreCase),
        
        // System-generated backup vault names
        new Regex(@"^DefaultBackupVault-.*$", RegexOptions.IgnoreCase),
        
        // Auto-generated Network Watcher names
        new Regex(@"^NetworkWatcher_.*$", RegexOptions.IgnoreCase),
        
        // System-generated diagnostic settings
        new Regex(@"^microsoft\.insights-.*$", RegexOptions.IgnoreCase),
        
        // Auto-generated security assessments
        new Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}.*Assessment$", RegexOptions.IgnoreCase),
        
        // Classic deployment artifacts
        new Regex(@"^Microsoft\.Classic.*$", RegexOptions.IgnoreCase)
    };

    /// <summary>
    /// System database names that should not be analyzed
    /// </summary>
    private static readonly HashSet<string> SystemDatabaseNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "master",
        "tempdb",
        "model",
        "msdb",
        "azure_maintenance",
        "azure_sys"
    };

    /// <summary>
    /// Filter resources to only include those that can be renamed
    /// </summary>
    /// <param name="resources">All Azure resources</param>
    /// <returns>Resources that can be analyzed for naming conventions</returns>
    public static List<AzureResource> FilterAnalyzableResources(List<AzureResource> resources)
    {
        var analyzable = new List<AzureResource>();

        foreach (var resource in resources)
        {
            if (ShouldSkipResource(resource))
            {
                continue;
            }

            analyzable.Add(resource);
        }

        return analyzable;
    }

    /// <summary>
    /// Get filtering statistics showing what was excluded and why
    /// </summary>
    /// <param name="originalResources">Original resource list</param>
    /// <param name="filteredResources">Filtered resource list</param>
    /// <returns>Filtering statistics</returns>
    public static FilteringStats GetFilterStats(List<AzureResource> originalResources, List<AzureResource> filteredResources)
    {
        var stats = new FilteringStats
        {
            TotalOriginalResources = originalResources.Count,
            AnalyzableResources = filteredResources.Count,
            FilteredOutResources = originalResources.Count - filteredResources.Count
        };

        // Analyze what was filtered out and why
        foreach (var resource in originalResources)
        {
            if (!filteredResources.Contains(resource))
            {
                var reason = GetFilterReason(resource);
                stats.FilterReasons[reason] = stats.FilterReasons.GetValueOrDefault(reason, 0) + 1;

                if (stats.FilteredResourceExamples.Count < 10) // Keep first 10 examples
                {
                    stats.FilteredResourceExamples.Add(new FilteredResourceExample
                    {
                        ResourceName = resource.Name,
                        ResourceType = resource.Type,
                        FilterReason = reason
                    });
                }
            }
        }

        stats.FilteringPercentage = stats.TotalOriginalResources > 0
            ? Math.Round((decimal)stats.FilteredOutResources / stats.TotalOriginalResources * 100, 2)
            : 0m;

        return stats;
    }

    /// <summary>
    /// Check if a resource should be skipped from naming analysis
    /// </summary>
    /// <param name="resource">Resource to check</param>
    /// <returns>True if resource should be skipped</returns>
    private static bool ShouldSkipResource(AzureResource resource)
    {
        // Skip system-generated resource types
        if (SystemGeneratedResourceTypes.Contains(resource.Type))
        {
            return true;
        }

        // Skip system-generated name patterns
        if (SystemNamePatterns.Any(pattern => pattern.IsMatch(resource.Name)))
        {
            return true;
        }

        // Skip SQL master databases specifically
        if (resource.Type.Contains("databases", StringComparison.OrdinalIgnoreCase) &&
            SystemDatabaseNames.Contains(resource.Name))
        {
            return true;
        }

        // Skip resources with no meaningful names (very short or all numbers)
        if (resource.Name.Length < 2 || resource.Name.All(char.IsDigit))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Get the reason why a resource was filtered out
    /// </summary>
    /// <param name="resource">Resource that was filtered</param>
    /// <returns>Human-readable filter reason</returns>
    private static string GetFilterReason(AzureResource resource)
    {
        // Check system-generated resource types
        if (SystemGeneratedResourceTypes.Contains(resource.Type))
        {
            return "System-generated resource type";
        }

        // Check system-generated name patterns
        if (SystemNamePatterns.Any(pattern => pattern.IsMatch(resource.Name)))
        {
            return "System-generated name pattern";
        }

        // Check SQL system databases
        if (resource.Type.Contains("databases", StringComparison.OrdinalIgnoreCase) &&
            SystemDatabaseNames.Contains(resource.Name))
        {
            return "System database";
        }

        // Check for very short or numeric names
        if (resource.Name.Length < 2)
        {
            return "Name too short";
        }

        if (resource.Name.All(char.IsDigit))
        {
            return "Numeric-only name";
        }

        return "Other system resource";
    }

    /// <summary>
    /// Check if a specific resource type is system-generated
    /// </summary>
    /// <param name="resourceType">Azure resource type</param>
    /// <returns>True if the resource type is system-generated</returns>
    public static bool IsSystemGeneratedResourceType(string resourceType)
    {
        return SystemGeneratedResourceTypes.Contains(resourceType);
    }

    /// <summary>
    /// Check if a resource name matches system-generated patterns
    /// </summary>
    /// <param name="resourceName">Resource name to check</param>
    /// <returns>True if the name matches system patterns</returns>
    public static bool IsSystemGeneratedName(string resourceName)
    {
        return SystemNamePatterns.Any(pattern => pattern.IsMatch(resourceName));
    }

    /// <summary>
    /// Get all system-generated resource types (for documentation/debugging)
    /// </summary>
    /// <returns>List of system-generated resource types</returns>
    public static List<string> GetSystemGeneratedResourceTypes()
    {
        return SystemGeneratedResourceTypes.ToList();
    }

    /// <summary>
    /// Get all system name patterns (for documentation/debugging)
    /// </summary>
    /// <returns>List of system name pattern descriptions</returns>
    public static List<string> GetSystemNamePatternDescriptions()
    {
        return new List<string>
        {
            "Default workspaces with UUIDs",
            "Application Insights Smart Detection",
            "Pure UUID names",
            "Auto-generated identity suffixes",
            "System-generated backup vault names",
            "Auto-generated Network Watcher names",
            "System-generated diagnostic settings",
            "Auto-generated security assessments",
            "Classic deployment artifacts"
        };
    }
}

/// <summary>
/// Statistics about resource filtering
/// </summary>
public class FilteringStats
{
    public int TotalOriginalResources { get; set; }
    public int AnalyzableResources { get; set; }
    public int FilteredOutResources { get; set; }
    public decimal FilteringPercentage { get; set; }
    public Dictionary<string, int> FilterReasons { get; set; } = new();
    public List<FilteredResourceExample> FilteredResourceExamples { get; set; } = new();
}

/// <summary>
/// Example of a filtered resource
/// </summary>
public class FilteredResourceExample
{
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string FilterReason { get; set; } = string.Empty;
}