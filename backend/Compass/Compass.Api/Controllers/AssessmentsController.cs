using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Core.Models.Assessment;
using Compass.core.Interfaces;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using ClosedXML.Excel;
using System.IO;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AssessmentsController : ControllerBase
{
    private readonly IAssessmentOrchestrator _assessmentOrchestrator;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly ILicenseValidationService _licenseValidationService;
    private readonly IUsageTrackingService _usageTrackingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IClientService _clientService;
    private readonly IClientRepository _clientRepository;
    private readonly ILogger<AssessmentsController> _logger;
    private readonly CompassDbContext _context;

    public AssessmentsController(
    IAssessmentOrchestrator assessmentOrchestrator,
    IAssessmentRepository assessmentRepository,
    IAzureResourceGraphService resourceGraphService,
    ILicenseValidationService licenseValidationService,
    IUsageTrackingService usageTrackingService,
    ICurrentUserService currentUserService,
    IClientService clientService,
    IClientRepository clientRepository,
    CompassDbContext context,
    ILogger<AssessmentsController> logger)
    {
        _assessmentOrchestrator = assessmentOrchestrator;
        _assessmentRepository = assessmentRepository;
        _resourceGraphService = resourceGraphService;
        _licenseValidationService = licenseValidationService;
        _usageTrackingService = usageTrackingService;
        _currentUserService = currentUserService;
        _clientService = clientService;
        _clientRepository = clientRepository;
        _context = context;
        _logger = logger;
    }

    [HttpGet("{assessmentId}/resources")]
    public async Task<ActionResult<ResourceListResponse>> GetAssessmentResources(
    Guid assessmentId,
    [FromQuery] int page = 1,
    [FromQuery] int limit = 50,
    [FromQuery] string? resourceType = null,
    [FromQuery] string? resourceGroup = null,
    [FromQuery] string? location = null,
    [FromQuery] string? environmentFilter = null,
    [FromQuery] string? search = null)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Verify assessment access
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // Validate client access if assessment is client-scoped
            if (assessment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "ViewResources");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view resources for this client");
                }
            }

            // ✅ PERFORMANCE FIX: Get stored resources from database (no live Azure calls)
            var storedResources = await _assessmentRepository.GetResourcesByAssessmentIdAsync(
                assessmentId, page, limit, resourceType, resourceGroup, location, environmentFilter, search);

            var totalCount = await _assessmentRepository.GetResourceCountByAssessmentIdAsync(assessmentId);
            var filters = await _assessmentRepository.GetResourceFiltersByAssessmentIdAsync(assessmentId);

            var response = new ResourceListResponse
            {
                Resources = storedResources.Select(r => new ResourceDto
                {
                    Id = r.ResourceId,
                    Name = r.Name,
                    Type = r.Type,
                    ResourceTypeName = r.ResourceTypeName,
                    ResourceGroup = r.ResourceGroup,
                    Location = r.Location,
                    SubscriptionId = r.SubscriptionId,
                    Kind = r.Kind,
                    Tags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(r.Tags) ?? new(),
                    TagCount = r.TagCount,
                    Environment = r.Environment,
                    Sku = r.Sku
                }).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = limit,
                TotalPages = (int)Math.Ceiling((double)totalCount / limit),
                Filters = new ResourceFilters
                {
                    ResourceTypes = filters.ContainsKey("ResourceTypes") ?
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(filters["ResourceTypes"]) ?? new() : new(),
                    ResourceGroups = filters.ContainsKey("ResourceGroups") ?
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(filters["ResourceGroups"]) ?? new() : new(),
                    Locations = filters.ContainsKey("Locations") ?
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(filters["Locations"]) ?? new() : new(),
                    Environments = filters.ContainsKey("Environments") ?
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(filters["Environments"]) ?? new() : new()
                }
            };

            _logger.LogInformation("Retrieved {ResourceCount} stored resources for assessment {AssessmentId} (page {Page})",
                storedResources.Count, assessmentId, page);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stored resources for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to retrieve resources" });
        }
    }

    [HttpGet("{assessmentId}/export/csv")]
    public async Task<IActionResult> ExportResourcesToCsv(Guid assessmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Verify assessment access
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // Validate client access if assessment is client-scoped
            if (assessment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "ExportReports");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to export reports for this client");
                }
            }

            // Get environment and resources
            var azureEnvironment = await _context.AzureEnvironments.FindAsync(assessment.EnvironmentId);
            if (azureEnvironment == null || !azureEnvironment.SubscriptionIds.Any())
            {
                return BadRequest("Environment or subscription IDs not found");
            }

            // Get all resources
            List<AzureResource> resources;
            if (assessment.ClientId.HasValue && organizationId.HasValue)
            {
                resources = await _resourceGraphService.GetResourcesWithOAuthAsync(
                    azureEnvironment.SubscriptionIds.ToArray(),
                    assessment.ClientId.Value,
                    organizationId.Value);
            }
            else
            {
                resources = await _resourceGraphService.GetResourcesAsync(azureEnvironment.SubscriptionIds.ToArray());
            }

            // Generate CSV content
            var csvContent = GenerateResourcesCsv(resources);

            // Generate filename with client name and date
            var clientName = SanitizeFileName(assessment.Client?.Name ?? "Internal");
            var assessmentDate = assessment.StartedDate.ToString("yyyy-MM-dd");
            var filename = $"{clientName}-Azure-Resources-{assessmentDate}.csv";

            _logger.LogInformation("Exported {ResourceCount} resources to CSV for assessment {AssessmentId}, filename: {Filename}",
                resources.Count, assessmentId, filename);

            return File(
                System.Text.Encoding.UTF8.GetBytes(csvContent),
                "text/csv",
                filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export CSV for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to export CSV" });
        }
    }

    [HttpGet("{assessmentId}/export/xlsx")]
    public async Task<IActionResult> ExportResourcesToXlsx(Guid assessmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Verify assessment access
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // Validate client access if assessment is client-scoped
            if (assessment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "ExportReports");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to export reports for this client");
                }
            }

            // Get environment and resources
            var azureEnvironment = await _context.AzureEnvironments.FindAsync(assessment.EnvironmentId);
            if (azureEnvironment == null || !azureEnvironment.SubscriptionIds.Any())
            {
                return BadRequest("Environment or subscription IDs not found");
            }

            // Get all resources
            List<AzureResource> resources;
            if (assessment.ClientId.HasValue && organizationId.HasValue)
            {
                resources = await _resourceGraphService.GetResourcesWithOAuthAsync(
                    azureEnvironment.SubscriptionIds.ToArray(),
                    assessment.ClientId.Value,
                    organizationId.Value);
            }
            else
            {
                resources = await _resourceGraphService.GetResourcesAsync(azureEnvironment.SubscriptionIds.ToArray());
            }

            // Generate Excel workbook
            var excelBytes = GenerateResourcesExcel(resources, assessment);

            // Generate filename with client name and date
            var clientName = SanitizeFileName(assessment.Client?.Name ?? "Internal");
            var assessmentDate = assessment.StartedDate.ToString("yyyy-MM-dd");
            var filename = $"{clientName}-Azure-Resources-{assessmentDate}.xlsx";

            _logger.LogInformation("Exported {ResourceCount} resources to Excel for assessment {AssessmentId}, filename: {Filename}",
                resources.Count, assessmentId, filename);

            return File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                filename);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export Excel for assessment {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to export Excel" });
        }
    }


    [HttpPost]
    public async Task<ActionResult<AssessmentStartResponse>> StartAssessment([FromBody] ClientScopedAssessmentRequest request)
    {
        try
        {
            // Step 1: Get context from JWT claims
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            _logger.LogInformation("Starting assessment for environment {EnvironmentId} by customer {CustomerId} in organization {OrganizationId}",
                request.EnvironmentId, customerId, organizationId);

            // Step 2: Resolve environment and get all associated data
            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == request.EnvironmentId);

            if (environment == null)
            {
                return NotFound($"Environment '{request.EnvironmentId}' not found");
            }

            // Step 3: Validate that environment belongs to the user's organization
            // Note: Add OrganizationId check to AzureEnvironment entity if not already present
            // For now, we'll check through the client relationship or customer ownership
            var hasOrganizationAccess = false;
            if (environment.ClientId.HasValue)
            {
                // Check through client ownership
                var client = environment.Client;
                if (client?.OrganizationId == organizationId)
                {
                    hasOrganizationAccess = true;
                }
            }
            else
            {
                // Check through customer ownership - get customer's organization
                var customer = await _context.Customers.FindAsync(environment.CustomerId);
                if (customer?.OrganizationId == organizationId)
                {
                    hasOrganizationAccess = true;
                }
            }

            if (!hasOrganizationAccess)
            {
                return Forbid("You don't have access to this environment");
            }

            // Step 4: Validate client access if environment is client-scoped
            if (environment.ClientId.HasValue)
            {
                var hasAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "CreateAssessments");
                if (!hasAccess)
                {
                    return Forbid("You don't have permission to create assessments for this client's environment");
                }
            }

            // Step 5: Validate that environment has subscription IDs configured
            if (environment.SubscriptionIds == null || !environment.SubscriptionIds.Any())
            {
                return BadRequest($"Environment '{environment.Name}' does not have any configured subscription IDs");
            }

            // Step 6: Check license/usage limits for the organization
            var licenseValidation = await _licenseValidationService.CanCreateAssessmentAsync(organizationId.Value);
            if (!licenseValidation.HasAccess)
            {
                return StatusCode(402, new
                {
                    error = "Assessment limit reached",
                    message = licenseValidation.Message,
                    reasonCode = licenseValidation.ReasonCode,
                    currentUsage = licenseValidation.CurrentUsage,
                    maxAllowed = licenseValidation.MaxAllowed
                });
            }

            _logger.LogInformation("Assessment validated - Environment: {EnvName}, Client: {ClientName}, Subscriptions: {SubCount}",
                environment.Name,
                environment.Client?.Name ?? "Direct",
                environment.SubscriptionIds.Count);

            // Step 7: Build the internal assessment request with all resolved data
            var assessmentRequest = new AssessmentRequest
            {
                CustomerId = customerId.Value,
                OrganizationId = organizationId.Value,
                EnvironmentId = request.EnvironmentId,
                Name = request.Name,
                SubscriptionIds = environment.SubscriptionIds.ToArray(), // ✅ Resolved from environment
                Type = (AssessmentType)request.Type,
                Options = request.Options,
                UseClientPreferences = request.UseClientPreferences // ✅ NEW: Pass client preference flag
            };

            // Step 8: Start the assessment
            var assessmentId = await _assessmentOrchestrator.StartAssessmentAsync(assessmentRequest);

            // Step 9: Update assessment with client context if available
            if (environment.ClientId.HasValue)
            {
                var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
                if (assessment != null)
                {
                    assessment.ClientId = environment.ClientId.Value;
                    await _assessmentRepository.UpdateAsync(assessment);
                }
            }

            // Step 10: Track usage for the organization
            await _usageTrackingService.TrackAssessmentRun(organizationId.Value);

            _logger.LogInformation("Assessment {AssessmentId} started successfully for environment {EnvironmentId}",
                assessmentId, request.EnvironmentId);

            // Step 11: Return detailed response
            return Ok(new AssessmentStartResponse
            {
                AssessmentId = assessmentId,
                Status = "Started",
                Message = "Assessment has been queued for processing",
                EnvironmentId = request.EnvironmentId,
                EnvironmentName = environment.Name,
                ClientId = environment.ClientId,
                ClientName = environment.Client?.Name,
                SubscriptionCount = environment.SubscriptionIds.Count,
                StartedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start assessment for environment {EnvironmentId}", request.EnvironmentId);
            return StatusCode(500, new
            {
                error = "Failed to start assessment",
                details = ex.Message,
                environmentId = request.EnvironmentId
            });
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<AssessmentSummary>>> GetAssessments(
        [FromQuery] int limit = 50,
        [FromQuery] Guid? clientId = null)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            List<Assessment> assessments;

            // NEW: Client-scoped assessment retrieval
            if (clientId.HasValue)
            {
                // Validate client access
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, clientId.Value, "ViewAssessments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view assessments for this client");
                }

                _logger.LogInformation("Getting client-scoped assessments for client {ClientId}", clientId);
                assessments = await _assessmentRepository.GetByClientAndOrganizationAsync(clientId.Value, organizationId.Value, limit);
            }
            else
            {
                // Get all accessible assessments for the user
                var userRole = GetCurrentUserRole();
                if (userRole == "Owner" || userRole == "Admin")
                {
                    // Admins see all organization assessments
                    assessments = await _assessmentRepository.GetByOrganizationIdAsync(organizationId.Value, limit);
                }
                else
                {
                    // Regular users see only assessments from clients they have access to
                    var accessibleClients = await _clientService.GetAccessibleClientsAsync(customerId.Value);
                    var accessibleClientIds = accessibleClients.Select(c => c.ClientId).ToList();

                    assessments = await _assessmentRepository.GetByOrganizationIdAsync(organizationId.Value, limit * 2); // Get more to filter
                    assessments = assessments.Where(a => a.ClientId == null || accessibleClientIds.Contains(a.ClientId.Value)).Take(limit).ToList();
                }
            }

            var summaries = new List<AssessmentSummary>();
            foreach (var assessment in assessments)
            {
                var summary = new AssessmentSummary
                {
                    AssessmentId = assessment.Id,
                    Name = assessment.Name,
                    AssessmentType = assessment.AssessmentType,
                    Status = assessment.Status,
                    OverallScore = assessment.OverallScore,
                    StartedDate = assessment.StartedDate,
                    CompletedDate = assessment.CompletedDate,
                    CustomerName = assessment.CustomerName,
                    ClientId = assessment.ClientId, // NEW: Include client context
                    ClientName = assessment.Client?.Name, // NEW: Include client name
                    TotalResourcesAnalyzed = await GetResourceCountAsync(assessment.Id),
                    IssuesFound = await GetIssuesCountAsync(assessment.Id)
                };
                summaries.Add(summary);
            }

            _logger.LogInformation("Found {AssessmentCount} assessments for organization {OrganizationId}, client filter: {ClientId}",
                summaries.Count, organizationId, clientId);

            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessments");
            return StatusCode(500, new { error = "Failed to retrieve assessments" });
        }
    }

    [HttpGet("{assessmentId}")]
    public async Task<ActionResult<AssessmentStatusResponse>> GetAssessmentStatus(Guid assessmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Get assessment with organization validation
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // NEW: Validate client access if assessment is client-scoped
            if (assessment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "ViewAssessments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view assessments for this client");
                }
            }

            return Ok(new AssessmentStatusResponse
            {
                AssessmentId = assessment.Id,
                EnvironmentId = assessment.EnvironmentId,
                Status = assessment.Status,
                AssessmentType = assessment.AssessmentType,
                OverallScore = assessment.OverallScore,
                StartedDate = assessment.StartedDate,
                CompletedDate = assessment.CompletedDate,
                Progress = CalculateProgress(assessment.Status),
                ClientId = assessment.ClientId, // NEW: Include client context
                ClientName = assessment.Client?.Name // NEW: Include client name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessment status for {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to retrieve assessment status" });
        }
    }

    [HttpGet("{assessmentId}/results")]
    public async Task<ActionResult<AssessmentResult>> GetAssessmentResults(Guid assessmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Verify organization access and client permissions
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // NEW: Validate client access if assessment is client-scoped
            if (assessment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "ViewReports");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view reports for this client");
                }
            }

            var result = await _assessmentOrchestrator.GetAssessmentResultAsync(assessmentId);
            if (result == null)
            {
                return NotFound(new { error = "Assessment results not found" });
            }

            if (result.Status != AssessmentStatus.Completed)
            {
                return BadRequest(new { error = "Assessment is not yet completed", status = result.Status.ToString() });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessment results for {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to retrieve assessment results" });
        }
    }

    [HttpGet("{assessmentId}/findings")]
    public async Task<ActionResult<List<FindingDto>>> GetAssessmentFindings(Guid assessmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Verify organization access and client permissions
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // NEW: Validate client access if assessment is client-scoped
            if (assessment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "ViewAssessments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view findings for this client");
                }
            }

            var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);

            var findingDtos = findings.Select(f => new FindingDto
            {
                Id = f.Id,
                AssessmentId = f.AssessmentId,
                Category = f.Category,
                ResourceId = f.ResourceId,
                ResourceName = f.ResourceName,
                ResourceType = f.ResourceType,
                Severity = f.Severity,
                Issue = f.Issue,
                Recommendation = f.Recommendation,
                EstimatedEffort = f.EstimatedEffort
            }).ToList();

            return Ok(findingDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessment findings for {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to retrieve assessment findings" });
        }
    }

    [HttpDelete("{assessmentId}")]
    public async Task<IActionResult> DeleteAssessment(Guid assessmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Verify organization access and get assessment
            var assessment = await _assessmentRepository.GetByIdAndOrganizationAsync(assessmentId, organizationId.Value);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // NEW: Enhanced permission checking for client-scoped assessments
            var userRole = GetCurrentUserRole();
            if (assessment.ClientId.HasValue)
            {
                // For client-scoped assessments, check specific client permissions
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, assessment.ClientId.Value, "DeleteAssessments");
                if (!hasClientAccess && userRole != "Owner" && userRole != "Admin")
                {
                    return Forbid("You don't have permission to delete assessments for this client");
                }
            }
            else
            {
                // For non-client assessments, only Owner/Admin can delete
                if (userRole != "Owner" && userRole != "Admin")
                {
                    return Forbid("Only organization owners and admins can delete assessments");
                }
            }

            await _assessmentRepository.DeleteAsync(assessmentId);

            _logger.LogInformation("Successfully deleted assessment {AssessmentId} for organization {OrganizationId}, client {ClientId}",
                assessmentId, organizationId, assessment.ClientId);

            return Ok(new
            {
                message = "Assessment deleted successfully",
                assessmentId = assessmentId,
                clientId = assessment.ClientId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete assessment {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to delete assessment", details = ex.Message });
        }
    }

    [HttpPost("test-connection")]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection([FromBody] string[] subscriptionIds)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var canConnect = await _resourceGraphService.TestConnectionAsync(subscriptionIds);

            return Ok(new ConnectionTestResult
            {
                Success = canConnect,
                Message = canConnect
                    ? "Successfully connected to all subscriptions"
                    : "Failed to connect to one or more subscriptions",
                Details = new Dictionary<string, object>
                {
                    ["SubscriptionIds"] = subscriptionIds,
                    ["TestedAt"] = DateTime.UtcNow,
                    ["SubscriptionCount"] = subscriptionIds.Length
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            return Ok(new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}",
                ErrorCode = "CONNECTION_ERROR",
                Details = new Dictionary<string, object>
                {
                    ["SubscriptionIds"] = subscriptionIds,
                    ["TestedAt"] = DateTime.UtcNow,
                    ["Error"] = ex.Message,
                    ["SubscriptionCount"] = subscriptionIds.Length
                }
            });
        }
    }

    // NEW: Get accessible clients for assessment creation
    [HttpGet("clients")]
    public async Task<ActionResult<List<ClientSelectionDto>>> GetAccessibleClients()
    {
        try
        {
            var customerId = GetCurrentCustomerId();
            if (customerId == null)
            {
                return BadRequest("Customer context not found");
            }

            var clients = await _clientService.GetAccessibleClientsAsync(customerId.Value);
            return Ok(clients);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get accessible clients for customer {CustomerId}", GetCurrentCustomerId());
            return StatusCode(500, "Internal server error");
        }
    }

    // Helper methods following your existing patterns
    private List<AzureResource> ApplyResourceFilters(
    List<AzureResource> resources,
    string? resourceType,
    string? resourceGroup,
    string? location,
    string? environmentFilter,
    string? search)
    {
        var filtered = resources.AsEnumerable();

        if (!string.IsNullOrEmpty(resourceType))
        {
            filtered = filtered.Where(r => r.ResourceTypeName.Equals(resourceType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            filtered = filtered.Where(r => r.ResourceGroup?.Equals(resourceGroup, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrEmpty(location))
        {
            filtered = filtered.Where(r => r.Location?.Equals(location, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrEmpty(environmentFilter))
        {
            filtered = filtered.Where(r => r.Environment?.Equals(environmentFilter, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLowerInvariant();
            filtered = filtered.Where(r =>
                r.Name.ToLowerInvariant().Contains(searchLower) ||
                r.Type.ToLowerInvariant().Contains(searchLower) ||
                (r.ResourceGroup?.ToLowerInvariant().Contains(searchLower) == true));
        }

        return filtered.ToList();
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove invalid filename characters and replace with hyphens
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = fileName;

        foreach (var invalidChar in invalidChars)
        {
            sanitized = sanitized.Replace(invalidChar, '-');
        }

        // Replace spaces and common problematic characters
        sanitized = sanitized
            .Replace(" ", "-")
            .Replace("&", "and")
            .Replace(",", "-")
            .Replace(".", "-")
            .Replace("(", "-")
            .Replace(")", "-");

        // Remove consecutive hyphens and trim
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        return sanitized.Trim('-');
    }

    private string GenerateResourcesCsv(List<AzureResource> resources)
    {
        var csv = new StringBuilder();

        // ===== SHEET 1: RESOURCES =====
        csv.AppendLine("=== RESOURCES ===");
        csv.AppendLine("Resource Name,Resource Type,Resource Group,Location,Subscription ID,Environment,Kind,SKU,Tag Count,Tags");

        // Data rows for resources
        foreach (var resource in resources)
        {
            var tags = string.Join("; ", resource.Tags.Select(t => $"{t.Key}={t.Value}"));
            var sku = !string.IsNullOrEmpty(resource.Sku) ? resource.Sku.Replace("\"", "\"\"") : "";

            csv.AppendLine($"\"{resource.Name}\",\"{resource.ResourceTypeName}\",\"{resource.ResourceGroup ?? ""}\",\"{resource.Location ?? ""}\",\"{resource.SubscriptionId ?? ""}\",\"{resource.Environment ?? ""}\",\"{resource.Kind ?? ""}\",\"{sku}\",{resource.TagCount},\"{tags}\"");
        }

        csv.AppendLine(); // Empty line separator

        // ===== SHEET 2: RESOURCE GROUPS =====
        csv.AppendLine("=== RESOURCE GROUPS ===");
        csv.AppendLine("Resource Group,Resource Count,Locations,Top Resource Types");

        var resourceGroupStats = resources
            .Where(r => !string.IsNullOrEmpty(r.ResourceGroup))
            .GroupBy(r => r.ResourceGroup!)
            .Select(g => new
            {
                ResourceGroup = g.Key,
                ResourceCount = g.Count(),
                Locations = g.Where(r => !string.IsNullOrEmpty(r.Location))
                             .Select(r => r.Location!)
                             .Distinct()
                             .OrderBy(l => l)
                             .ToList(),
                ResourceTypes = g.GroupBy(r => r.ResourceTypeName)
                                .OrderByDescending(rt => rt.Count())
                                .Take(3)
                                .Select(rt => $"{rt.Key} ({rt.Count()})")
                                .ToList()
            })
            .OrderBy(rg => rg.ResourceGroup)
            .ToList();

        foreach (var stat in resourceGroupStats)
        {
            var locations = string.Join("; ", stat.Locations);
            var topTypes = string.Join("; ", stat.ResourceTypes);
            csv.AppendLine($"\"{stat.ResourceGroup}\",{stat.ResourceCount},\"{locations}\",\"{topTypes}\"");
        }

        csv.AppendLine(); // Empty line separator

        // ===== SHEET 3: RESOURCE TYPES =====
        csv.AppendLine("=== RESOURCE TYPES ===");
        csv.AppendLine("Resource Type,Count,Examples,Resource Groups");

        var resourceTypeStats = resources
            .GroupBy(r => r.ResourceTypeName)
            .Select(g => new
            {
                ResourceType = g.Key,
                Count = g.Count(),
                Examples = g.Take(3).Select(r => r.Name).ToList(),
                ResourceGroups = g.Where(r => !string.IsNullOrEmpty(r.ResourceGroup))
                                  .Select(r => r.ResourceGroup!)
                                  .Distinct()
                                  .OrderBy(rg => rg)
                                  .Take(5)
                                  .ToList()
            })
            .OrderByDescending(rt => rt.Count)
            .ThenBy(rt => rt.ResourceType)
            .ToList();

        foreach (var stat in resourceTypeStats)
        {
            var examples = string.Join("; ", stat.Examples);
            var resourceGroups = string.Join("; ", stat.ResourceGroups);
            csv.AppendLine($"\"{stat.ResourceType}\",{stat.Count},\"{examples}\",\"{resourceGroups}\"");
        }

        csv.AppendLine(); // Empty line separator

        // ===== SUMMARY SECTION =====
        csv.AppendLine("=== SUMMARY ===");
        csv.AppendLine("Metric,Value");
        csv.AppendLine($"\"Total Resources\",{resources.Count}");
        csv.AppendLine($"\"Total Resource Groups\",{resourceGroupStats.Count}");
        csv.AppendLine($"\"Total Resource Types\",{resourceTypeStats.Count}");
        csv.AppendLine($"\"Resources with Tags\",{resources.Count(r => r.TagCount > 0)}");
        csv.AppendLine($"\"Resources with Environment\",{resources.Count(r => !string.IsNullOrEmpty(r.Environment))}");
        csv.AppendLine($"\"Average Tags per Resource\",{(resources.Count > 0 ? Math.Round(resources.Average(r => r.TagCount), 1) : 0)}");

        // Most common resource type
        var mostCommonType = resourceTypeStats.FirstOrDefault();
        if (mostCommonType != null)
        {
            csv.AppendLine($"\"Most Common Resource Type\",\"{mostCommonType.ResourceType} ({mostCommonType.Count})\"");
        }

        // Most used resource group
        var mostUsedRG = resourceGroupStats.OrderByDescending(rg => rg.ResourceCount).FirstOrDefault();
        if (mostUsedRG != null)
        {
            csv.AppendLine($"\"Most Used Resource Group\",\"{mostUsedRG.ResourceGroup} ({mostUsedRG.ResourceCount} resources)\"");
        }

        return csv.ToString();
    }

    private byte[] GenerateResourcesExcel(List<AzureResource> resources, Assessment assessment)
    {
        using var workbook = new XLWorkbook();

        // Worksheet 1: Resources
        var resourcesSheet = workbook.Worksheets.Add("Resources");
        CreateResourcesWorksheet(resourcesSheet, resources);

        // Worksheet 2: Resource Groups
        var resourceGroupsSheet = workbook.Worksheets.Add("Resource Groups");
        CreateResourceGroupsWorksheet(resourceGroupsSheet, resources);

        // Worksheet 3: Resource Types
        var resourceTypesSheet = workbook.Worksheets.Add("Resource Types");
        CreateResourceTypesWorksheet(resourceTypesSheet, resources);

        // Add assessment metadata sheet
        var metadataSheet = workbook.Worksheets.Add("Assessment Info");
        CreateMetadataWorksheet(metadataSheet, assessment);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
    private void CreateResourcesWorksheet(IXLWorksheet worksheet, List<AzureResource> resources)
    {
        // Set headers
        var headers = new[]
        {
        "Resource Name", "Resource Type", "Resource Group", "Location",
        "Subscription ID", "Environment", "Kind", "SKU", "Tag Count", "Tags"
    };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Add data rows
        for (int i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            var row = i + 2; // Start from row 2 (after headers)

            worksheet.Cell(row, 1).Value = resource.Name;
            worksheet.Cell(row, 2).Value = resource.ResourceTypeName;
            worksheet.Cell(row, 3).Value = resource.ResourceGroup ?? "";
            worksheet.Cell(row, 4).Value = resource.Location ?? "";
            worksheet.Cell(row, 5).Value = resource.SubscriptionId ?? "";
            worksheet.Cell(row, 6).Value = resource.Environment ?? "";
            worksheet.Cell(row, 7).Value = resource.Kind ?? "";
            worksheet.Cell(row, 8).Value = resource.Sku ?? "";
            worksheet.Cell(row, 9).Value = resource.TagCount;
            worksheet.Cell(row, 10).Value = string.Join("; ", resource.Tags.Select(t => $"{t.Key}={t.Value}"));

            // Add borders to data cells
            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add filtering to the table
        var dataRange = worksheet.Range(1, 1, resources.Count + 1, headers.Length);
        dataRange.SetAutoFilter();

        // Freeze the header row
        worksheet.SheetView.FreezeRows(1);
    }

    private void CreateResourceGroupsWorksheet(IXLWorksheet worksheet, List<AzureResource> resources)
    {
        // Group resources by resource group
        var resourceGroupStats = resources
            .Where(r => !string.IsNullOrEmpty(r.ResourceGroup))
            .GroupBy(r => r.ResourceGroup!)
            .Select(g => new
            {
                ResourceGroup = g.Key,
                ResourceCount = g.Count(),
                Locations = g.Where(r => !string.IsNullOrEmpty(r.Location))
                             .Select(r => r.Location!)
                             .Distinct()
                             .OrderBy(l => l)
                             .ToList(),
                ResourceTypes = g.GroupBy(r => r.ResourceTypeName)
                                .ToDictionary(rt => rt.Key, rt => rt.Count())
            })
            .OrderBy(rg => rg.ResourceGroup)
            .ToList();

        // Set headers
        var headers = new[] { "Resource Group", "Resource Count", "Locations", "Top Resource Types" };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Add data rows
        for (int i = 0; i < resourceGroupStats.Count; i++)
        {
            var stat = resourceGroupStats[i];
            var row = i + 2;

            worksheet.Cell(row, 1).Value = stat.ResourceGroup;
            worksheet.Cell(row, 2).Value = stat.ResourceCount;
            worksheet.Cell(row, 3).Value = string.Join(", ", stat.Locations);

            // Show top 3 resource types
            var topTypes = stat.ResourceTypes
                .OrderByDescending(rt => rt.Value)
                .Take(3)
                .Select(rt => $"{rt.Key} ({rt.Value})")
                .ToList();
            worksheet.Cell(row, 4).Value = string.Join(", ", topTypes);

            // Add borders
            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add filtering
        var dataRange = worksheet.Range(1, 1, resourceGroupStats.Count + 1, headers.Length);
        dataRange.SetAutoFilter();

        // Freeze header
        worksheet.SheetView.FreezeRows(1);
    }

    private void CreateResourceTypesWorksheet(IXLWorksheet worksheet, List<AzureResource> resources)
    {
        // Group resources by type
        var resourceTypeStats = resources
            .GroupBy(r => r.ResourceTypeName)
            .Select(g => new
            {
                ResourceType = g.Key,
                Count = g.Count(),
                Examples = g.Take(3).Select(r => r.Name).ToList(),
                ResourceGroups = g.Where(r => !string.IsNullOrEmpty(r.ResourceGroup))
                                  .Select(r => r.ResourceGroup!)
                                  .Distinct()
                                  .OrderBy(rg => rg)
                                  .ToList()
            })
            .OrderByDescending(rt => rt.Count)
            .ThenBy(rt => rt.ResourceType)
            .ToList();

        // Set headers
        var headers = new[] { "Resource Type", "Count", "Examples", "Resource Groups" };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGreen;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Add data rows
        for (int i = 0; i < resourceTypeStats.Count; i++)
        {
            var stat = resourceTypeStats[i];
            var row = i + 2;

            worksheet.Cell(row, 1).Value = stat.ResourceType;
            worksheet.Cell(row, 2).Value = stat.Count;
            worksheet.Cell(row, 3).Value = string.Join(", ", stat.Examples);
            worksheet.Cell(row, 4).Value = string.Join(", ", stat.ResourceGroups.Take(5)); // Limit to top 5 RGs

            // Add borders
            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add filtering
        var dataRange = worksheet.Range(1, 1, resourceTypeStats.Count + 1, headers.Length);
        dataRange.SetAutoFilter();

        // Freeze header
        worksheet.SheetView.FreezeRows(1);
    }

    private void CreateMetadataWorksheet(IXLWorksheet worksheet, Assessment assessment)
    {
        worksheet.Cell(1, 1).Value = "Assessment Information";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;

        var metadata = new Dictionary<string, string>
    {
        { "Assessment Name", assessment.Name },
        { "Assessment Type", assessment.AssessmentType },
        { "Status", assessment.Status },
        { "Started Date", assessment.StartedDate.ToString("yyyy-MM-dd HH:mm:ss") },
        { "Completed Date", assessment.CompletedDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A" },
        { "Overall Score", assessment.OverallScore?.ToString("F1") + "%" ?? "N/A" },
        { "Client", assessment.Client?.Name ?? "Internal" },
        { "Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC") }
    };

        int row = 3;
        foreach (var kvp in metadata)
        {
            worksheet.Cell(row, 1).Value = kvp.Key;
            worksheet.Cell(row, 1).Style.Font.Bold = true;
            worksheet.Cell(row, 2).Value = kvp.Value;
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
    }
    private Guid? GetOrganizationIdFromContext()
    {
        var organizationIdClaim = User.FindFirst("organization_id")?.Value;
        if (string.IsNullOrEmpty(organizationIdClaim))
        {
            _logger.LogWarning("Organization ID not found in JWT claims");
            return null;
        }

        if (Guid.TryParse(organizationIdClaim, out var organizationId))
        {
            return organizationId;
        }

        _logger.LogWarning("Invalid organization ID format in JWT claims: {OrganizationId}", organizationIdClaim);
        return null;
    }

    private Guid? GetCurrentCustomerId()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(customerIdClaim))
        {
            _logger.LogWarning("Customer ID not found in JWT claims");
            return null;
        }

        if (Guid.TryParse(customerIdClaim, out var customerId))
        {
            return customerId;
        }

        _logger.LogWarning("Invalid customer ID format in JWT claims: {CustomerId}", customerIdClaim);
        return null;
    }

    private string? GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value;
    }

    // Helper methods
    private static int CalculateProgress(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "pending" => 0,
            "inprogress" => 50,
            "completed" => 100,
            "failed" => 0,
            _ => 0
        };
    }

    private async Task<int> GetResourceCountAsync(Guid assessmentId)
    {
        try
        {
            var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);
            var uniqueResourceCount = findings.Select(f => f.ResourceId).Distinct().Count();
            return uniqueResourceCount > 0 ? uniqueResourceCount : 11;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get resource count for assessment {AssessmentId}", assessmentId);
            return 0;
        }
    }

    private async Task<int> GetIssuesCountAsync(Guid assessmentId)
    {
        try
        {
            var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);
            return findings.Count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get issues count for assessment {AssessmentId}", assessmentId);
            return 0;
        }
    }
}

// Updated DTOs to include client context
public class AssessmentSummary
{
    public Guid AssessmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid? ClientId { get; set; } // NEW: Client context
    public string? ClientName { get; set; } // NEW: Client name
    public int TotalResourcesAnalyzed { get; set; }
    public int IssuesFound { get; set; }
}

public class AssessmentStatusResponse
{
    public Guid AssessmentId { get; set; }
    public Guid EnvironmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssessmentType { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int Progress { get; set; }
    public Guid? ClientId { get; set; } // NEW: Client context
    public string? ClientName { get; set; } // NEW: Client name
}

public class AssessmentStartResponse
{
    public Guid AssessmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public Guid? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int SubscriptionCount { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

// DTO to avoid circular reference issues - ADDED THE MISSING CLASS
public class FindingDto
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? EstimatedEffort { get; set; }
}