namespace Compass.Core.Models.Assessment;

public class ResourceListResponse
{
    public List<ResourceDto> Resources { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ResourceFilters Filters { get; set; } = new();
}

public class ResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ResourceTypeName { get; set; } = string.Empty;
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? SubscriptionId { get; set; }
    public string? Kind { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
    public int TagCount { get; set; }
    public string? Environment { get; set; }
    public string? Sku { get; set; }
}

public class ResourceFilters
{
    public Dictionary<string, int> ResourceTypes { get; set; } = new();
    public Dictionary<string, int> ResourceGroups { get; set; } = new();
    public Dictionary<string, int> Locations { get; set; } = new();
    public Dictionary<string, int> Environments { get; set; } = new();
}

public class ResourceGroupSummary
{
    public string Name { get; set; } = string.Empty;
    public int ResourceCount { get; set; }
    public List<string> Locations { get; set; } = new();
    public Dictionary<string, int> ResourceTypes { get; set; } = new();
}

public class ResourceTypeSummary
{
    public string Type { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> Examples { get; set; } = new();
    public List<string> ResourceGroups { get; set; } = new();
}