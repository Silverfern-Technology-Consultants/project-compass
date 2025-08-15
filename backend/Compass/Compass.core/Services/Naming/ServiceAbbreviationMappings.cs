using System.Collections.Generic;

namespace Compass.Core.Services.Naming;

/// <summary>
/// Service name detection and abbreviation mapping for better naming convention analysis
/// </summary>
public static class ServiceAbbreviationMappings
{
    /// <summary>
    /// Common service abbreviations mapped to their full names
    /// </summary>
    private static readonly Dictionary<string, string> ServiceAbbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        // Storage and Data
        ["stg"] = "storage",
        ["stor"] = "storage",
        ["store"] = "storage",
        ["data"] = "data",
        ["blob"] = "blob",
        ["file"] = "file",
        ["queue"] = "queue",
        ["table"] = "table",
        ["backup"] = "backup",
        ["bkp"] = "backup",
        ["bak"] = "backup",
        ["archive"] = "archive",
        ["arch"] = "archive",

        // Web and API
        ["web"] = "web",
        ["www"] = "web",
        ["site"] = "website",
        ["website"] = "website",
        ["portal"] = "portal",
        ["app"] = "application",
        ["application"] = "application",
        ["api"] = "api",
        ["svc"] = "service",
        ["service"] = "service",
        ["func"] = "function",
        ["functions"] = "function",
        ["worker"] = "worker",
        ["job"] = "job",
        ["task"] = "task",

        // Database
        ["db"] = "database",
        ["database"] = "database",
        ["sql"] = "sql",
        ["nosql"] = "nosql",
        ["mongo"] = "mongodb",
        ["redis"] = "redis",
        ["cache"] = "cache",
        ["elastic"] = "elasticsearch",
        ["search"] = "search",

        // Business Applications
        ["crm"] = "crm",
        ["erp"] = "erp",
        ["hr"] = "hr",
        ["finance"] = "finance",
        ["accounting"] = "accounting",
        ["inventory"] = "inventory",
        ["warehouse"] = "warehouse",
        ["logistics"] = "logistics",
        ["shipping"] = "shipping",
        ["billing"] = "billing",
        ["payment"] = "payment",
        ["ecommerce"] = "ecommerce",
        ["shop"] = "shop",
        ["cart"] = "cart",

        // Communication and Collaboration
        ["email"] = "email",
        ["mail"] = "email",
        ["chat"] = "chat",
        ["teams"] = "teams",
        ["meeting"] = "meeting",
        ["video"] = "video",
        ["voice"] = "voice",
        ["sms"] = "sms",
        ["notification"] = "notification",
        ["notify"] = "notification",

        // Analytics and Monitoring
        ["analytics"] = "analytics",
        ["reporting"] = "reporting",
        ["dashboard"] = "dashboard",
        ["monitoring"] = "monitoring",
        ["metrics"] = "metrics",
        ["logging"] = "logging",
        ["audit"] = "audit",
        ["tracking"] = "tracking",
        ["telemetry"] = "telemetry",

        // Security
        ["auth"] = "authentication",
        ["identity"] = "identity",
        ["security"] = "security",
        ["firewall"] = "firewall",
        ["vpn"] = "vpn",
        ["ssl"] = "ssl",
        ["cert"] = "certificate",
        ["key"] = "key",
        ["vault"] = "vault",
        ["secrets"] = "secrets",

        // DevOps and Infrastructure
        ["ci"] = "ci",
        ["cd"] = "cd",
        ["build"] = "build",
        ["deploy"] = "deployment",
        ["deployment"] = "deployment",
        ["pipeline"] = "pipeline",
        ["automation"] = "automation",
        ["orchestration"] = "orchestration",
        ["config"] = "configuration",
        ["settings"] = "settings",

        // Media and Content
        ["media"] = "media",
        ["image"] = "image",
        ["video"] = "video",
        ["audio"] = "audio",
        ["content"] = "content",
        ["cms"] = "cms",
        ["cdn"] = "cdn",
        ["streaming"] = "streaming",

        // Integration
        ["integration"] = "integration",
        ["connector"] = "connector",
        ["bridge"] = "bridge",
        ["gateway"] = "gateway",
        ["proxy"] = "proxy",
        ["hub"] = "hub",
        ["broker"] = "broker",
        ["queue"] = "queue",
        ["eventbus"] = "eventbus",

        // AI and Machine Learning
        ["ai"] = "ai",
        ["ml"] = "machinelearning",
        ["bot"] = "bot",
        ["chatbot"] = "chatbot",
        ["vision"] = "vision",
        ["speech"] = "speech",
        ["nlp"] = "nlp",
        ["cognitive"] = "cognitive",

        // Industry-Specific
        ["healthcare"] = "healthcare",
        ["medical"] = "medical",
        ["patient"] = "patient",
        ["claims"] = "claims",
        ["insurance"] = "insurance",
        ["banking"] = "banking",
        ["trading"] = "trading",
        ["portfolio"] = "portfolio",
        ["real-estate"] = "realestate",
        ["property"] = "property",
        ["education"] = "education",
        ["learning"] = "learning",
        ["course"] = "course",
        ["student"] = "student",

        // Common Business Functions
        ["admin"] = "admin",
        ["management"] = "management",
        ["support"] = "support",
        ["help"] = "help",
        ["docs"] = "documentation",
        ["documentation"] = "documentation",
        ["wiki"] = "wiki",
        ["knowledge"] = "knowledge",
        ["feedback"] = "feedback",
        ["survey"] = "survey",
        ["review"] = "review",
        ["rating"] = "rating"
    };

    /// <summary>
    /// Common variations and truncations that should be recognized as the same service
    /// </summary>
    private static readonly Dictionary<string, List<string>> ServiceVariations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["storage"] = new() { "stg", "stor", "store", "storage" },
        ["backup"] = new() { "bkp", "bak", "backup" },
        ["database"] = new() { "db", "database" },
        ["application"] = new() { "app", "application" },
        ["function"] = new() { "func", "functions", "function" },
        ["website"] = new() { "web", "www", "site", "website" },
        ["api"] = new() { "api", "service", "svc" },
        ["authentication"] = new() { "auth", "identity", "authentication" },
        ["configuration"] = new() { "config", "settings", "configuration" },
        ["documentation"] = new() { "docs", "documentation" },
        ["notification"] = new() { "notify", "notification" },
        ["administration"] = new() { "admin", "administration" },
        ["machinelearning"] = new() { "ml", "ai", "machinelearning" }
    };

    /// <summary>
    /// Technology and vendor-specific service names
    /// </summary>
    private static readonly Dictionary<string, string> TechnologyServices = new(StringComparer.OrdinalIgnoreCase)
    {
        // Microsoft Technologies
        ["sharepoint"] = "sharepoint",
        ["exchange"] = "exchange",
        ["outlook"] = "outlook",
        ["teams"] = "teams",
        ["onedrive"] = "onedrive",
        ["powerbi"] = "powerbi",
        ["dynamics"] = "dynamics",
        ["azure"] = "azure",
        ["office365"] = "office365",
        ["o365"] = "office365",

        // Development Frameworks and Tools
        ["dotnet"] = "dotnet",
        ["nodejs"] = "nodejs",
        ["react"] = "react",
        ["angular"] = "angular",
        ["vue"] = "vue",
        ["express"] = "express",
        ["nestjs"] = "nestjs",
        ["spring"] = "spring",
        ["django"] = "django",
        ["flask"] = "flask",
        ["rails"] = "rails",

        // Databases and Storage
        ["sqlserver"] = "sqlserver",
        ["mysql"] = "mysql",
        ["postgresql"] = "postgresql",
        ["postgres"] = "postgresql",
        ["mongodb"] = "mongodb",
        ["redis"] = "redis",
        ["elasticsearch"] = "elasticsearch",
        ["solr"] = "solr",
        ["cassandra"] = "cassandra",
        ["dynamodb"] = "dynamodb",
        ["cosmosdb"] = "cosmosdb",

        // Third-Party Services
        ["salesforce"] = "salesforce",
        ["hubspot"] = "hubspot",
        ["mailchimp"] = "mailchimp",
        ["stripe"] = "stripe",
        ["paypal"] = "paypal",
        ["twilio"] = "twilio",
        ["sendgrid"] = "sendgrid",
        ["slack"] = "slack",
        ["discord"] = "discord",
        ["zoom"] = "zoom",

        // Business Applications
        ["compass"] = "compass",
        ["veeam"] = "veeam", // Backup software
        ["vmware"] = "vmware",
        ["citrix"] = "citrix",
        ["tableau"] = "tableau",
        ["powerbi"] = "powerbi",
        ["qlikview"] = "qlikview",
        ["splunk"] = "splunk",
        ["newrelic"] = "newrelic",
        ["datadog"] = "datadog"
    };

    /// <summary>
    /// Check if a string is a known service abbreviation
    /// </summary>
    /// <param name="abbreviation">Potential service abbreviation</param>
    /// <returns>True if it's a recognized service abbreviation</returns>
    public static bool IsKnownServiceAbbreviation(string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(abbreviation))
            return false;

        return ServiceAbbreviations.ContainsKey(abbreviation) ||
               TechnologyServices.ContainsKey(abbreviation);
    }

    /// <summary>
    /// Get the full service name from an abbreviation
    /// </summary>
    /// <param name="abbreviation">Service abbreviation</param>
    /// <returns>Full service name or the original abbreviation if not found</returns>
    public static string GetFullServiceName(string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(abbreviation))
            return abbreviation;

        if (ServiceAbbreviations.TryGetValue(abbreviation, out var fullName))
            return fullName;

        if (TechnologyServices.TryGetValue(abbreviation, out var techName))
            return techName;

        return abbreviation; // Return original if no mapping found
    }

    /// <summary>
    /// Check if two service names represent the same service (considering variations)
    /// </summary>
    /// <param name="service1">First service name</param>
    /// <param name="service2">Second service name</param>
    /// <returns>True if they represent the same service</returns>
    public static bool AreEquivalentServices(string service1, string service2)
    {
        if (string.IsNullOrWhiteSpace(service1) || string.IsNullOrWhiteSpace(service2))
            return false;

        var normalized1 = GetFullServiceName(service1.ToLowerInvariant());
        var normalized2 = GetFullServiceName(service2.ToLowerInvariant());

        if (normalized1.Equals(normalized2, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if they're in the same variation group
        foreach (var variations in ServiceVariations.Values)
        {
            if (variations.Contains(service1, StringComparer.OrdinalIgnoreCase) &&
                variations.Contains(service2, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extract service name from a resource name, considering common abbreviations and client-specific mappings
    /// </summary>
    /// <param name="resourceName">Resource name to analyze</param>
    /// <param name="acceptedCompanyNames">List of accepted company names to exclude</param>
    /// <param name="clientServiceAbbreviations">Client-specific service abbreviations</param>
    /// <returns>Detected service name or null if not found</returns>
    public static string? ExtractServiceFromResourceName(
        string resourceName, 
        List<string>? acceptedCompanyNames = null,
        List<object>? clientServiceAbbreviations = null)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
            return null;

        var parts = resourceName.Split(new[] { '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        acceptedCompanyNames ??= new List<string>();

        // Convert client abbreviations to dictionary for faster lookup
        var clientAbbrevDict = new Dictionary<string, string>(StringComparer.Ordinal); // Case-sensitive as per requirements
        if (clientServiceAbbreviations != null)
        {
            foreach (var abbr in clientServiceAbbreviations)
            {
                // Handle both ServiceAbbreviationDto objects and dynamic objects
                var abbreviation = GetPropertyValue(abbr, "Abbreviation")?.ToString();
                var fullName = GetPropertyValue(abbr, "FullName")?.ToString();
                
                if (!string.IsNullOrEmpty(abbreviation) && !string.IsNullOrEmpty(fullName))
                {
                    clientAbbrevDict[abbreviation] = fullName;
                }
            }
        }

        // Common environments to exclude
        var commonEnvironments = new[] { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat", "shared" };

        foreach (var part in parts)
        {
            var partLower = part.ToLowerInvariant();

            // Skip if it's a company name
            if (acceptedCompanyNames.Contains(partLower, StringComparer.OrdinalIgnoreCase))
                continue;

            // Skip if it's an environment
            if (commonEnvironments.Contains(partLower))
                continue;

            // Skip if it's a resource type abbreviation
            if (AzureResourceAbbreviations.IsKnownAbbreviation(partLower))
                continue;

            // Skip numeric patterns
            if (System.Text.RegularExpressions.Regex.IsMatch(partLower, @"^\d+$") ||
                System.Text.RegularExpressions.Regex.IsMatch(partLower, @"^[a-z]+\d+$"))
                continue;

            // PRIORITY 1: Check client-specific abbreviations FIRST (case-sensitive)
            if (clientAbbrevDict.TryGetValue(part, out var clientMapping))
            {
                return clientMapping;
            }

            // PRIORITY 2: Check case-insensitive client abbreviations as fallback
            var caseInsensitiveMatch = clientAbbrevDict.FirstOrDefault(kvp => 
                kvp.Key.Equals(part, StringComparison.OrdinalIgnoreCase));
            if (!caseInsensitiveMatch.Equals(default(KeyValuePair<string, string>)))
            {
                return caseInsensitiveMatch.Value;
            }

            // PRIORITY 3: Check if it's a known service abbreviation
            if (IsKnownServiceAbbreviation(partLower))
            {
                return GetFullServiceName(partLower);
            }

            // PRIORITY 4: If it's longer than 2 characters and not excluded, might be a service
            if (partLower.Length > 2)
            {
                return partLower;
            }
        }

        return null;
    }

    /// <summary>
    /// Get the preferred abbreviation for a service name
    /// </summary>
    /// <param name="serviceName">Full service name</param>
    /// <returns>Preferred abbreviation or the original name if no abbreviation exists</returns>
    public static string GetPreferredAbbreviation(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return serviceName;

        var serviceLower = serviceName.ToLowerInvariant();

        // Find the abbreviation that maps to this service
        var abbreviation = ServiceAbbreviations.FirstOrDefault(kvp =>
            kvp.Value.Equals(serviceLower, StringComparison.OrdinalIgnoreCase)).Key;

        if (!string.IsNullOrEmpty(abbreviation))
            return abbreviation;

        // Check technology services
        var techAbbreviation = TechnologyServices.FirstOrDefault(kvp =>
            kvp.Value.Equals(serviceLower, StringComparison.OrdinalIgnoreCase)).Key;

        if (!string.IsNullOrEmpty(techAbbreviation))
            return techAbbreviation;

        // Return original if no abbreviation found
        return serviceName;
    }

    /// <summary>
    /// Get all known service abbreviations (for debugging/documentation)
    /// </summary>
    /// <returns>Dictionary of all service abbreviations</returns>
    public static Dictionary<string, string> GetAllServiceAbbreviations()
    {
        var combined = new Dictionary<string, string>(ServiceAbbreviations, StringComparer.OrdinalIgnoreCase);

        foreach (var tech in TechnologyServices)
        {
            combined[tech.Key] = tech.Value;
        }

        return combined;
    }

    /// <summary>
    /// Check if a service name should be excluded from company name detection
    /// </summary>
    /// <param name="serviceName">Service name to check</param>
    /// <param name="acceptedCompanyNames">List of accepted company names</param>
    /// <returns>True if it's definitely a service and not a company name</returns>
    public static bool IsDefinitelyServiceName(string serviceName, List<string>? acceptedCompanyNames = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return false;

        acceptedCompanyNames ??= new List<string>();
        var serviceLower = serviceName.ToLowerInvariant();

        // If it's in accepted company names, it's definitely not a service for classification purposes
        if (acceptedCompanyNames.Contains(serviceLower, StringComparer.OrdinalIgnoreCase))
            return false;

        // If it's a known service abbreviation or technology, it's definitely a service
        return IsKnownServiceAbbreviation(serviceLower) ||
               TechnologyServices.ContainsKey(serviceLower);
    }

    /// <summary>
    /// Suggest service name variations for better naming consistency
    /// </summary>
    /// <param name="serviceName">Current service name</param>
    /// <returns>List of suggested variations</returns>
    public static List<string> GetServiceNameSuggestions(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return new List<string>();

        var suggestions = new List<string>();
        var serviceLower = serviceName.ToLowerInvariant();

        // Find variations for this service
        foreach (var variationGroup in ServiceVariations)
        {
            if (variationGroup.Value.Contains(serviceLower, StringComparer.OrdinalIgnoreCase))
            {
                suggestions.AddRange(variationGroup.Value.Where(v =>
                    !v.Equals(serviceLower, StringComparison.OrdinalIgnoreCase)));
                break;
            }
        }

        // If no variations found, try to find the full name
        var fullName = GetFullServiceName(serviceLower);
        if (!fullName.Equals(serviceLower, StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(fullName);
        }

        // Add preferred abbreviation
        var abbreviation = GetPreferredAbbreviation(serviceLower);
        if (!abbreviation.Equals(serviceLower, StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(abbreviation);
        }

        return suggestions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Helper method to safely get property values from objects (handles both strongly typed and dynamic objects)
    /// </summary>
    private static object? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null) return null;

        var type = obj.GetType();
        var property = type.GetProperty(propertyName);
        
        if (property != null)
        {
            return property.GetValue(obj);
        }

        // Try to handle dynamic objects or dictionaries
        if (obj is IDictionary<string, object> dict)
        {
            return dict.TryGetValue(propertyName, out var value) ? value : null;
        }

        return null;
    }
}