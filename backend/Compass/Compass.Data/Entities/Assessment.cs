using Compass.Data.Entities;

namespace Compass.Data.Entities;

public class Assessment
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    // Organization scoping (existing)
    public Guid? OrganizationId { get; set; }

    // Client scoping for MSP isolation (existing)
    public Guid? ClientId { get; set; }

    public Guid EnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;

    // NEW: Assessment Category for Sprint 6
    public string AssessmentCategory { get; set; } = string.Empty;

    // Client preferences flag
    public bool UseClientPreferences { get; set; } = false;

    public decimal? OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? ReportBlobUrl { get; set; }

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual Organization? Organization { get; set; }
    public virtual Client? Client { get; set; }
    public virtual ICollection<AssessmentFinding> Findings { get; set; } = new List<AssessmentFinding>();
    public virtual ICollection<AssessmentResource> Resources { get; set; } = new List<AssessmentResource>();
}