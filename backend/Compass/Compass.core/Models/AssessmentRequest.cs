using Compass.Core.Models;

public class AssessmentRequest
{
    public Guid EnvironmentId { get; set; }
    public string[] SubscriptionIds { get; set; } = Array.Empty<string>();
    public AssessmentType Type { get; set; } = AssessmentType.Full;
    public AssessmentOptions? Options { get; set; }

    // This will be set by the controller based on authenticated user
    public Guid CustomerId { get; set; }
}