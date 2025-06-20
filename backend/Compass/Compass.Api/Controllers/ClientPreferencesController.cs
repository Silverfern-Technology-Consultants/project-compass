using Compass.Core.Models;
using Compass.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientPreferencesController : ControllerBase
{
    private readonly IClientPreferencesRepository _preferencesRepository;
    private readonly ILogger<ClientPreferencesController> _logger;

    public ClientPreferencesController(
        IClientPreferencesRepository preferencesRepository,
        ILogger<ClientPreferencesController> logger)
    {
        _preferencesRepository = preferencesRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get client preferences by customer ID
    /// </summary>
    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<ClientPreferences>> GetClientPreferences(Guid customerId)
    {
        try
        {
            var preferences = await _preferencesRepository.GetByCustomerIdAsync(customerId);
            if (preferences == null)
            {
                return NotFound(new { error = "Client preferences not found", customerId });
            }

            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve client preferences for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to retrieve client preferences" });
        }
    }

    /// <summary>
    /// Get assessment configuration derived from client preferences
    /// </summary>
    [HttpGet("customer/{customerId}/assessment-config")]
    public async Task<ActionResult<ClientAssessmentConfiguration>> GetAssessmentConfiguration(Guid customerId)
    {
        try
        {
            var config = await _preferencesRepository.GetAssessmentConfigurationAsync(customerId);
            if (config == null)
            {
                return NotFound(new { error = "Assessment configuration not found", customerId });
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve assessment configuration for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to retrieve assessment configuration" });
        }
    }

    /// <summary>
    /// Create or update client preferences
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ClientPreferences>> SaveClientPreferences([FromBody] ClientPreferences preferences)
    {
        try
        {
            if (string.IsNullOrEmpty(preferences.CustomerName))
            {
                return BadRequest(new { error = "Customer name is required" });
            }

            var savedPreferences = await _preferencesRepository.SaveClientPreferencesAsync(preferences);

            _logger.LogInformation("Successfully saved preferences for customer {CustomerName}", preferences.CustomerName);

            return CreatedAtAction(
                nameof(GetClientPreferences),
                new { customerId = savedPreferences.CustomerId },
                savedPreferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save client preferences");
            return StatusCode(500, new { error = "Failed to save client preferences" });
        }
    }

    /// <summary>
    /// Update existing client preferences
    /// </summary>
    [HttpPut("customer/{customerId}")]
    public async Task<ActionResult<ClientPreferences>> UpdateClientPreferences(
        Guid customerId,
        [FromBody] ClientPreferences preferences)
    {
        try
        {
            preferences.CustomerId = customerId;
            var updatedPreferences = await _preferencesRepository.UpdatePreferencesAsync(preferences);

            _logger.LogInformation("Successfully updated preferences for customer {CustomerId}", customerId);

            return Ok(updatedPreferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update client preferences for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to update client preferences" });
        }
    }

    /// <summary>
    /// Delete client preferences
    /// </summary>
    [HttpDelete("customer/{customerId}")]
    public async Task<ActionResult> DeleteClientPreferences(Guid customerId)
    {
        try
        {
            var deleted = await _preferencesRepository.DeletePreferencesAsync(customerId);
            if (!deleted)
            {
                return NotFound(new { error = "Client preferences not found", customerId });
            }

            _logger.LogInformation("Successfully deleted preferences for customer {CustomerId}", customerId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete client preferences for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to delete client preferences" });
        }
    }

    /// <summary>
    /// Get all active client preferences
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ClientPreferences>>> GetAllActivePreferences()
    {
        try
        {
            var preferences = await _preferencesRepository.GetAllActivePreferencesAsync();
            return Ok(preferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve all client preferences");
            return StatusCode(500, new { error = "Failed to retrieve client preferences" });
        }
    }

    /// <summary>
    /// Get Safe Haven demo preferences (for testing)
    /// </summary>
    [HttpGet("demo/safe-haven")]
    public ActionResult<ClientPreferences> GetSafeHavenDemoPreferences()
    {
        try
        {
            // Create demo Safe Haven preferences
            var safeHavenPreferences = new ClientPreferences
            {
                CustomerId = Guid.NewGuid(),
                CustomerName = "Safe Haven Technologies (Demo)",
                CreatedDate = DateTime.UtcNow,
                IsActive = true,

                OrganizationalStructure = new OrganizationalStructurePreferences
                {
                    EnvironmentSeparationMethods = new List<string> { "Subscription-based", "Resource Group based" },
                    EnvironmentIsolationLevel = "Strict",
                    ComplianceRequirements = new List<string> { "PCI DSS", "SOC 2" },
                    PrimaryResourceOrganizationMethod = "Environment-based resource groups"
                },

                NamingStrategy = new NamingConventionStrategy
                {
                    PreferredNamingStyles = new List<string> { "Uppercase", "CamelCase", "Snake_case" },
                    RequiredNameElements = new List<string> { "Environment indicator", "Project code" },
                    HasAutomationRequirements = true,
                    IncludeEnvironmentIndicator = true
                },

                TaggingStrategy = new TaggingStrategy
                {
                    RequiredTags = new List<string> { "Environment", "Owner", "Project", "CostCenter" },
                    EnforceTagCompliance = true
                },

                Governance = new GovernancePreferences
                {
                    AccessControlGranularity = "Resource Group level",
                    ComplianceFrameworks = new List<string> { "PCI DSS", "SOC 2" }
                },

                Implementation = new ImplementationPreferences
                {
                    MigrationStrategy = "Phased approach",
                    RiskTolerance = "Low"
                }
            };

            return Ok(safeHavenPreferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Safe Haven demo preferences");
            return StatusCode(500, new { error = "Failed to generate demo preferences" });
        }
    }

    /// <summary>
    /// Run assessment with client-specific preferences
    /// </summary>
    [HttpPost("customer/{customerId}/assess")]
    public async Task<ActionResult<PreferenceBasedAssessmentResult>> RunPreferenceBasedAssessment(
        Guid customerId,
        [FromBody] PreferenceBasedAssessmentRequest request)
    {
        try
        {
            _logger.LogInformation("Running preference-based assessment for customer {CustomerId}", customerId);

            // This would typically get resources from Azure Resource Graph
            // For demo purposes, return a placeholder response
            var result = new PreferenceBasedAssessmentResult
            {
                CustomerId = customerId,
                AssessmentDate = DateTime.UtcNow,
                Message = "Preference-based assessment functionality ready. Connect to Azure Resource Graph for real data.",
                HasClientPreferences = await _preferencesRepository.GetByCustomerIdAsync(customerId) != null
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run preference-based assessment for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to run assessment" });
        }
    }

    /// <summary>
    /// Create preferences from assessment form data (like Safe Haven form)
    /// </summary>
    [HttpPost("from-form")]
    public async Task<ActionResult<ClientPreferences>> CreatePreferencesFromForm([FromBody] AssessmentFormData formData)
    {
        try
        {
            var preferences = ConvertFormDataToPreferences(formData);
            var savedPreferences = await _preferencesRepository.SaveClientPreferencesAsync(preferences);

            _logger.LogInformation("Successfully created preferences from form data for {CustomerName}", formData.CustomerName);

            return CreatedAtAction(
                nameof(GetClientPreferences),
                new { customerId = savedPreferences.CustomerId },
                savedPreferences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create preferences from form data");
            return StatusCode(500, new { error = "Failed to create preferences from form" });
        }
    }

    private ClientPreferences ConvertFormDataToPreferences(AssessmentFormData formData)
    {
        return new ClientPreferences
        {
            CustomerId = Guid.NewGuid(),
            CustomerName = formData.CustomerName,
            CreatedDate = DateTime.UtcNow,
            IsActive = true,

            OrganizationalStructure = new OrganizationalStructurePreferences
            {
                EnvironmentSeparationMethods = formData.EnvironmentSeparationMethods ?? new List<string>(),
                EnvironmentIsolationLevel = formData.EnvironmentIsolationLevel ?? string.Empty,
                ComplianceRequirements = formData.ComplianceRequirements ?? new List<string>(),
                PrimaryResourceOrganizationMethod = formData.PrimaryResourceOrganizationMethod ?? string.Empty
            },

            NamingStrategy = new NamingConventionStrategy
            {
                PreferredNamingStyles = formData.PreferredNamingStyles ?? new List<string>(),
                RequiredNameElements = formData.RequiredNameElements ?? new List<string>(),
                HasAutomationRequirements = formData.HasAutomationRequirements,
                IncludeEnvironmentIndicator = formData.RequiredNameElements?.Contains("Environment indicator") ?? false
            },

            TaggingStrategy = new TaggingStrategy
            {
                RequiredTags = new List<string> { "Environment", "Owner", "Project" },
                EnforceTagCompliance = true
            },

            Governance = new GovernancePreferences
            {
                AccessControlGranularity = formData.AccessControlGranularity ?? string.Empty,
                ComplianceFrameworks = formData.ComplianceFrameworks ?? new List<string>()
            },

            Implementation = new ImplementationPreferences
            {
                MigrationStrategy = formData.MigrationStrategy ?? string.Empty,
                RiskTolerance = formData.RiskTolerance ?? string.Empty
            }
        };
    }
}

// Supporting DTOs
public class PreferenceBasedAssessmentRequest
{
    public string[] SubscriptionIds { get; set; } = Array.Empty<string>();
    public bool IncludeDependencyAnalysis { get; set; } = true;
}

public class PreferenceBasedAssessmentResult
{
    public Guid CustomerId { get; set; }
    public DateTime AssessmentDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool HasClientPreferences { get; set; }
}

public class AssessmentFormData
{
    public string CustomerName { get; set; } = string.Empty;
    public string CompletedBy { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // From form sections
    public List<string>? EnvironmentSeparationMethods { get; set; }
    public string? EnvironmentIsolationLevel { get; set; }
    public List<string>? ComplianceRequirements { get; set; }
    public string? PrimaryResourceOrganizationMethod { get; set; }

    public List<string>? PreferredNamingStyles { get; set; }
    public List<string>? RequiredNameElements { get; set; }
    public bool HasAutomationRequirements { get; set; }

    public string? AccessControlGranularity { get; set; }
    public List<string>? ComplianceFrameworks { get; set; }

    public string? MigrationStrategy { get; set; }
    public string? RiskTolerance { get; set; }
}