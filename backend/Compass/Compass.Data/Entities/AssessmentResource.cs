namespace Compass.Data.Entities;

public class AssessmentResource
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }

    // Azure Resource properties
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ResourceTypeName { get; set; } = string.Empty;
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    public string? SubscriptionId { get; set; }
    public string? Kind { get; set; }
    public string? Sku { get; set; }
    public string Tags { get; set; } = "{}"; // JSON serialized tags
    public int TagCount { get; set; }
    public string? Environment { get; set; }
    public string? Properties { get; set; } // JSON serialized properties

    // Metadata
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public virtual Assessment Assessment { get; set; } = null!;
}