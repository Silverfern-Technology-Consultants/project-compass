// Update ClientPreferences.cs entity

using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class ClientPreferences
{
    [Key]
    public Guid ClientPreferencesId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ClientId { get; set; }

    [Required]
    public Guid OrganizationId { get; set; }

    // Legacy naming strategy fields (keep for backward compatibility)
    public string? AllowedNamingPatterns { get; set; } // JSON array - ["Kebab-case", "Lowercase"]
    public string? RequiredNamingElements { get; set; } // JSON array - ["Environment indicator", "Resource type prefix"]
    public bool EnvironmentIndicators { get; set; } = false;

    // NEW: Naming scheme configuration (JSON serialized NamingSchemeConfiguration)
    public string? NamingSchemeConfiguration { get; set; } // JSON - Complete drag-and-drop scheme
    public string? ComponentDefinitions { get; set; } // JSON array of ComponentDefinition - Client-specific component definitions

    // NEW: Accepted company names for validation
    public string? AcceptedCompanyNames { get; set; } // JSON array - ["abc", "xyz", "company"] - Valid company identifiers

    // NEW: Service Abbreviations (Phase 1 - Service Abbreviations Feature)
    public string? ServiceAbbreviations { get; set; } // JSON array of ServiceAbbreviationDto

    // Enhanced naming strategy fields
    public string? NamingStyle { get; set; } // 'standardized', 'mixed', 'legacy'
    public string? EnvironmentSize { get; set; } // 'small', 'medium', 'large', 'enterprise'
    public string? OrganizationMethod { get; set; } // 'environment', 'application', 'business-unit'
    public string? EnvironmentIndicatorLevel { get; set; } // 'required', 'recommended', 'optional', 'none'

    // Enhanced tagging strategy fields
    public string? RequiredTags { get; set; } // JSON array - ["Environment", "Owner", "Project"] (legacy)
    public bool EnforceTagCompliance { get; set; } = true;
    public string? TaggingApproach { get; set; } // 'comprehensive', 'basic', 'minimal', 'custom'
    public string? SelectedTags { get; set; } // JSON array - combination of standard + custom tags
    public string? CustomTags { get; set; } // JSON array - user-defined custom tags

    // Enhanced governance preferences
    public string? ComplianceFrameworks { get; set; } // JSON array - ["SOC2", "PCI DSS", "HIPAA"]
    public string? SelectedCompliances { get; set; } // JSON array - selected compliance frameworks
    public bool NoSpecificRequirements { get; set; } = false;

    // Audit fields
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByCustomerId { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public Guid? LastModifiedByCustomerId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Client Client { get; set; } = null!;
    public virtual Organization Organization { get; set; } = null!;
    public virtual Customer? CreatedBy { get; set; }
    public virtual Customer? LastModifiedBy { get; set; }
}