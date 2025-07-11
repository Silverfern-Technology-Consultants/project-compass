namespace Compass.Core.Models;

public class AzureResource
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? SubscriptionId { get; set; }
    public string? Kind { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public string? Properties { get; set; }
    public string? Sku { get; set; }

    // Computed properties for analysis
    public string ResourceTypeName => Type.Split('/').LastOrDefault() ?? Type;
    public string ResourceProvider => Type.Split('/').FirstOrDefault() ?? Type;

    public bool HasTags => Tags.Any();
    public int TagCount => Tags.Count;

    // Extract environment from resource name or tags
    public string? Environment
    {
        get
        {
            // Check tags first
            if (Tags.TryGetValue("Environment", out var envTag) ||
                Tags.TryGetValue("environment", out envTag) ||
                Tags.TryGetValue("Env", out envTag))
            {
                return envTag;
            }

            // Check name patterns
            var nameLower = Name.ToLowerInvariant();
            if (nameLower.Contains("-dev-") || nameLower.Contains("-dev")) return "dev";
            if (nameLower.Contains("-test-") || nameLower.Contains("-test")) return "test";
            if (nameLower.Contains("-staging-") || nameLower.Contains("-staging")) return "staging";
            if (nameLower.Contains("-prod-") || nameLower.Contains("-prod")) return "prod";
            if (nameLower.Contains("-production-") || nameLower.Contains("-production")) return "production";

            return null;
        }
    }
}