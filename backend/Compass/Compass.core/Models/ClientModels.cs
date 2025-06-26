// ClientModels.cs - Add to Compass.Core/Models/
using System.ComponentModel.DataAnnotations;

namespace Compass.Core.Models;

public class ClientContext
{
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public bool IsMultiClient { get; set; }
}

public class ClientSelectionDto
{
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public bool IsActive { get; set; }
    public bool HasActiveContract { get; set; }
    public int AssessmentCount { get; set; }
}

public class ClientStatsDto
{
    public int TotalClients { get; set; }
    public int ActiveClients { get; set; }
    public int InactiveClients { get; set; }
    public int TotalAssessments { get; set; }
    public int TotalEnvironments { get; set; }
    public Dictionary<string, int> ClientsByIndustry { get; set; } = new();
    public Dictionary<string, int> ClientsByStatus { get; set; } = new();
}

public class ClientActivityDto
{
    public Guid ActivityId { get; set; }
    public Guid ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty; // "assessment_created", "environment_added", etc.
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public object? Metadata { get; set; }
}

// SIMPLIFIED: Only require what the user should actually provide
public class ClientScopedAssessmentRequest
{
    [Required]
    public Guid EnvironmentId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int Type { get; set; } // 0=NamingConvention, 1=Tagging, 2=Full

    public AssessmentOptions? Options { get; set; } = new AssessmentOptions
    {
        AnalyzeNamingConventions = true,
        AnalyzeTagging = true,
        IncludeRecommendations = true
    };

    // REMOVED: CustomerId, OrganizationId, ClientId, SubscriptionIds
    // These will be resolved automatically by the controller
}

// Enhanced CustomerInfo to include client context  
// Note: This extends the existing CustomerInfo class from your AuthModels.cs
public class CustomerInfoWithClientContext
{
    // Include all properties from existing CustomerInfo
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string Role { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTime? TrialEndDate { get; set; }

    // NEW: Client context properties
    public List<ClientSelectionDto> AccessibleClients { get; set; } = new();
    public Guid? SelectedClientId { get; set; }
    public string? SelectedClientName { get; set; }
    public bool HasMultipleClients => AccessibleClients.Count > 1;
}