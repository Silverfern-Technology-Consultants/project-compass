namespace Compass.Core.Models;

public class AssessmentRequest
{
    public Guid CustomerId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty; // ✅ FIXED: Add this property
    public string[] SubscriptionIds { get; set; } = Array.Empty<string>();
    public AssessmentType Type { get; set; }
    public AssessmentOptions? Options { get; set; }
}