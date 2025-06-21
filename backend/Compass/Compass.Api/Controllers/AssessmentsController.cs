using Microsoft.AspNetCore.Mvc;
// using Microsoft.AspNetCore.Authorization; // COMMENTED OUT TEMPORARILY
using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data.Entities;
using Compass.Data.Repositories;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize] // COMMENTED OUT FOR TESTING - TODO: Re-enable after auth flow is working
public class AssessmentsController : ControllerBase
{
    private readonly IAssessmentOrchestrator _assessmentOrchestrator;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly ILicenseValidationService _licenseValidationService;
    private readonly IUsageTrackingService _usageTrackingService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(
        IAssessmentOrchestrator assessmentOrchestrator,
        IAssessmentRepository assessmentRepository,
        IAzureResourceGraphService resourceGraphService,
        ILicenseValidationService licenseValidationService,
        IUsageTrackingService usageTrackingService,
        ICurrentUserService currentUserService,
        ILogger<AssessmentsController> logger)
    {
        _assessmentOrchestrator = assessmentOrchestrator;
        _assessmentRepository = assessmentRepository;
        _resourceGraphService = resourceGraphService;
        _licenseValidationService = licenseValidationService;
        _usageTrackingService = usageTrackingService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<AssessmentStartResponse>> StartAssessment([FromBody] AssessmentRequest request)
    {
        try
        {
            // TODO: Remove this hardcoded customer ID after auth is working
            var customerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

            // Check licensing BEFORE creating assessment
            var licenseValidation = await _licenseValidationService.CanCreateAssessmentAsync(customerId);
            if (!licenseValidation.HasAccess)
            {
                return StatusCode(402, new // Payment Required
                {
                    error = "Assessment limit reached",
                    message = licenseValidation.Message,
                    reasonCode = licenseValidation.ReasonCode,
                    currentUsage = licenseValidation.CurrentUsage,
                    maxAllowed = licenseValidation.MaxAllowed
                });
            }

            _logger.LogInformation("Starting assessment for customer {CustomerId}, environment {EnvironmentId}",
                customerId, request.EnvironmentId);

            // Set the customer ID in the request
            request.CustomerId = customerId;

            var assessmentId = await _assessmentOrchestrator.StartAssessmentAsync(request);

            // Track usage
            await _usageTrackingService.TrackAssessmentRun(customerId);

            return Ok(new AssessmentStartResponse
            {
                AssessmentId = assessmentId,
                Status = "Started",
                Message = "Assessment has been queued for processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start assessment");
            return StatusCode(500, new { error = "Failed to start assessment", details = ex.Message });
        }
    }

    [HttpGet("{assessmentId}")]
    public async Task<ActionResult<AssessmentStatusResponse>> GetAssessmentStatus(Guid assessmentId)
    {
        try
        {
            // TODO: Remove this hardcoded customer ID after auth is working
            var customerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

            var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // Ensure customer owns this assessment
            if (assessment.CustomerId != customerId)
            {
                return Forbid();
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
                Progress = CalculateProgress(assessment.Status)
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
            // TODO: Remove this hardcoded customer ID after auth is working
            var customerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

            var result = await _assessmentOrchestrator.GetAssessmentResultAsync(assessmentId);
            if (result == null)
            {
                return NotFound(new { error = "Assessment not found" });
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
            // TODO: Remove this hardcoded customer ID after auth is working
            var customerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

            var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // Ensure customer owns this assessment
            if (assessment.CustomerId != customerId)
            {
                return Forbid();
            }

            var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);

            // Convert to DTOs to avoid circular reference
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

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<List<AssessmentSummary>>> GetAssessmentsByCustomer(
        Guid customerId,
        [FromQuery] int limit = 50)
    {
        try
        {
            // TODO: Remove this hardcoded validation after auth is working
            var expectedCustomerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

            // For now, always use the hardcoded customer ID
            customerId = expectedCustomerId;

            var assessments = await _assessmentRepository.GetByCustomerIdAsync(customerId, limit);

            var summaries = new List<AssessmentSummary>();
            foreach (var assessment in assessments)
            {
                var summary = new AssessmentSummary
                {
                    AssessmentId = assessment.Id,
                    AssessmentType = assessment.AssessmentType,
                    Status = assessment.Status,
                    OverallScore = assessment.OverallScore,
                    StartedDate = assessment.StartedDate,
                    CompletedDate = assessment.CompletedDate,
                    CustomerName = assessment.CustomerName,
                    TotalResourcesAnalyzed = await GetResourceCountAsync(assessment.Id),
                    IssuesFound = await GetIssuesCountAsync(assessment.Id)
                };
                summaries.Add(summary);
            }

            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessments for customer {CustomerId}", customerId);
            return StatusCode(500, new { error = "Failed to retrieve assessments" });
        }
    }

    [HttpDelete("{assessmentId}")]
    public async Task<IActionResult> DeleteAssessment(Guid assessmentId)
    {
        try
        {
            // TODO: Remove this hardcoded customer ID after auth is working
            var customerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

            var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            // Ensure customer owns this assessment
            if (assessment.CustomerId != customerId)
            {
                return Forbid();
            }

            await _assessmentRepository.DeleteAsync(assessmentId);

            _logger.LogInformation("Successfully deleted assessment {AssessmentId} for customer {CustomerId}",
                assessmentId, customerId);

            return Ok(new
            {
                message = "Assessment deleted successfully",
                assessmentId = assessmentId
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
            // TODO: Remove this hardcoded customer ID after auth is working
            var customerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");

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

// DTO to avoid circular reference issues
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