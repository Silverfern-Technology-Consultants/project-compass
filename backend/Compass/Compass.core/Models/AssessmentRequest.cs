namespace Compass.Core.Models;

public class AssessmentRequest
{
    public Guid CustomerId { get; set; }

    // NEW: Organization context
    public Guid? OrganizationId { get; set; }

    public Guid EnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string[] SubscriptionIds { get; set; } = Array.Empty<string>();
    public AssessmentType Type { get; set; }
    public AssessmentOptions? Options { get; set; }
}