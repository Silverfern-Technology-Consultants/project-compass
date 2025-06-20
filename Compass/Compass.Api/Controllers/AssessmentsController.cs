using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AssessmentsController : ControllerBase
{
    private readonly IAssessmentOrchestrator _assessmentOrchestrator;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly ILogger<AssessmentsController> _logger;

    public AssessmentsController(
        IAssessmentOrchestrator assessmentOrchestrator,
        IAssessmentRepository assessmentRepository,
        IAzureResourceGraphService resourceGraphService,
        ILogger<AssessmentsController> logger)
    {
        _assessmentOrchestrator = assessmentOrchestrator;
        _assessmentRepository = assessmentRepository;
        _resourceGraphService = resourceGraphService;
        _logger = logger;
    }

    /// <summary>
    /// Start a new Azure governance assessment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<AssessmentStartResponse>> StartAssessment([FromBody] AssessmentRequest request)
    {
        try
        {
            _logger.LogInformation("Starting assessment for environment {EnvironmentId}", request.EnvironmentId);

            // TODO: Temporarily bypass Azure connection check for testing
            // Remove comments when real Azure integration is implemented
            // Validate subscription access
            // var canConnect = await _resourceGraphService.TestConnectionAsync(request.SubscriptionIds);
            // if (!canConnect)
            // {
            //     return BadRequest(new { error = "Unable to connect to specified Azure subscriptions. Please check permissions." });
            // }

            var assessmentId = await _assessmentOrchestrator.StartAssessmentAsync(request);

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

    /// <summary>
    /// Get assessment status and basic information
    /// </summary>
    [HttpGet("{assessmentId}")]
    public async Task<ActionResult<AssessmentStatusResponse>> GetAssessmentStatus(Guid assessmentId)
    {
        try
        {
            var assessment = await _assessmentRepository.GetByIdAsync(assessmentId);
            if (assessment == null)
            {
                return NotFound(new { error = "Assessment not found" });
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

    /// <summary>
    /// Get detailed assessment results
    /// </summary>
    [HttpGet("{assessmentId}/results")]
    public async Task<ActionResult<AssessmentResult>> GetAssessmentResults(Guid assessmentId)
    {
        try
        {
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

    /// <summary>
    /// Get assessment findings (issues and violations)
    /// </summary>
    [HttpGet("{assessmentId}/findings")]
    public async Task<ActionResult<List<AssessmentFinding>>> GetAssessmentFindings(
        Guid assessmentId,
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var findings = await _assessmentRepository.GetFindingsByAssessmentIdAsync(assessmentId);
            if (!findings.Any())
            {
                return NotFound(new { error = "No findings found for this assessment" });
            }

            // Apply filters
            var filteredFindings = findings.AsEnumerable();

            if (!string.IsNullOrEmpty(category))
            {
                filteredFindings = filteredFindings.Where(f =>
                    f.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(severity))
            {
                filteredFindings = filteredFindings.Where(f =>
                    f.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));
            }

            // Apply pagination
            var pagedFindings = filteredFindings
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            Response.Headers["X-Total-Count"] = findings.Count.ToString();
            Response.Headers["X-Page"] = page.ToString();
            Response.Headers["X-Page-Size"] = pageSize.ToString();

            return Ok(pagedFindings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessment findings for {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to retrieve assessment findings" });
        }
    }

    /// <summary>
    /// Get assessments for a specific customer environment
    /// </summary>
    [HttpGet("environment/{environmentId}")]
    public async Task<ActionResult<List<AssessmentSummary>>> GetAssessmentsByEnvironment(
        Guid environmentId,
        [FromQuery] int limit = 10)
    {
        try
        {
            var assessments = await _assessmentRepository.GetByEnvironmentIdAsync(environmentId, limit);

            var summaries = assessments.Select(a => new AssessmentSummary
            {
                AssessmentId = a.Id,
                AssessmentType = a.AssessmentType,
                Status = a.Status,
                OverallScore = a.OverallScore,
                StartedDate = a.StartedDate,
                CompletedDate = a.CompletedDate
            }).ToList();

            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get assessments for environment {EnvironmentId}", environmentId);
            return StatusCode(500, new { error = "Failed to retrieve assessments" });
        }
    }

    /// <summary>
    /// Test Azure connection for given subscriptions
    /// </summary>
    [HttpPost("test-connection")]
    public async Task<ActionResult<ConnectionTestResult>> TestConnection([FromBody] string[] subscriptionIds)
    {
        try
        {
            var canConnect = await _resourceGraphService.TestConnectionAsync(subscriptionIds);

            return Ok(new ConnectionTestResult
            {
                Success = canConnect,
                SubscriptionIds = subscriptionIds,
                Message = canConnect
                    ? "Successfully connected to all subscriptions"
                    : "Failed to connect to one or more subscriptions",
                TestedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));
            return Ok(new ConnectionTestResult
            {
                Success = false,
                SubscriptionIds = subscriptionIds,
                Message = $"Connection test failed: {ex.Message}",
                TestedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Get assessment recommendations
    /// </summary>
    [HttpGet("{assessmentId}/recommendations")]
    public async Task<ActionResult<List<AssessmentRecommendation>>> GetRecommendations(Guid assessmentId)
    {
        try
        {
            var result = await _assessmentOrchestrator.GetAssessmentResultAsync(assessmentId);
            if (result == null)
            {
                return NotFound(new { error = "Assessment not found" });
            }

            return Ok(result.Recommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendations for {AssessmentId}", assessmentId);
            return StatusCode(500, new { error = "Failed to retrieve recommendations" });
        }
    }

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
}

// Response DTOs
public class AssessmentStartResponse
{
    public Guid AssessmentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
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
}

public class AssessmentSummary
{
    public Guid AssessmentId { get; set; }
    public string AssessmentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public DateTime StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string[] SubscriptionIds { get; set; } = Array.Empty<string>();
    public string Message { get; set; } = string.Empty;
    public DateTime TestedAt { get; set; }
}