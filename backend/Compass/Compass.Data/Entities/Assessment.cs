using Compass.Data.Entities;

namespace Compass.Data.Entities;

public class Assessment
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    // Organization scoping (existing)
    public Guid? OrganizationId { get; set; }

    // NEW: Client scoping for MSP isolation
    public Guid? ClientId { get; set; }

    public Guid EnvironmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? ReportBlobUrl { get; set; }

    // Navigation properties
    public virtual Customer Customer { get; set; } = null!;
    public virtual Organization? Organization { get; set; }
    public virtual Client? Client { get; set; } // NEW: Client navigation
    public virtual ICollection<AssessmentFinding> Findings { get; set; } = new List<AssessmentFinding>();
}