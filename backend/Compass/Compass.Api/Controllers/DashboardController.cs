using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Compass.Data;
using Compass.Data.Repositories;
using System.Security.Claims;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly CompassDbContext _context;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IClientRepository _clientRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        CompassDbContext context,
        IAssessmentRepository assessmentRepository,
        IClientRepository clientRepository,
        ILogger<DashboardController> logger)
    {
        _context = context;
        _assessmentRepository = assessmentRepository;
        _clientRepository = clientRepository;
        _logger = logger;
    }

    [HttpGet("company")]
    public async Task<ActionResult<CompanyDashboardData>> GetCompanyMetrics()
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            _logger.LogInformation("Getting company dashboard metrics for organization {OrganizationId}", organizationId);

            // Get total clients
            var totalClients = await _context.Clients
                .CountAsync(c => c.OrganizationId == organizationId && c.IsActive);

            // Get total assessments across organization
            var totalAssessments = await _context.Assessments
                .CountAsync(a => a.OrganizationId == organizationId);

            // Get team members
            var teamMembers = await _context.Customers
                .CountAsync(c => c.OrganizationId == organizationId && c.IsActive);

            // Calculate average client score from completed assessments
            var completedAssessments = await _context.Assessments
                .Where(a => a.OrganizationId == organizationId &&
                           a.Status == "Completed" &&
                           a.OverallScore.HasValue)
                .ToListAsync();

            var averageClientScore = completedAssessments.Any()
                ? (int)Math.Round(completedAssessments.Average(a => a.OverallScore!.Value))
                : 0;

            // Get recent client activity (top 5 clients by recent assessments)
            var recentClients = await GetRecentClientActivity(organizationId.Value);

            // Calculate growth metrics (mock data for now - implement with historical tracking)
            var companyData = new CompanyDashboardData
            {
                TotalClients = totalClients,
                TotalAssessments = totalAssessments,
                TeamMembers = teamMembers,
                AverageClientScore = averageClientScore,
                ClientsGrowth = new GrowthMetric { Positive = true, Value = 15 },
                AssessmentsGrowth = new GrowthMetric { Positive = true, Value = 23 },
                TeamGrowth = new GrowthMetric { Positive = true, Value = 25 },
                ScoreImprovement = new GrowthMetric { Positive = true, Value = 8 },
                RecentClients = recentClients
            };

            return Ok(companyData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving company dashboard metrics for organization {OrganizationId}", GetOrganizationIdFromContext());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<ClientDashboardData>> GetClientMetrics(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            _logger.LogInformation("Getting client dashboard metrics for client {ClientId} in organization {OrganizationId}",
                clientId, organizationId);

            // Verify client belongs to organization
            var client = await _clientRepository.GetByIdAndOrganizationAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            // Get client assessments
            var clientAssessments = await _context.Assessments
                .Where(a => a.ClientId == clientId)
                .OrderByDescending(a => a.StartedDate)
                .ToListAsync();

            // Get client subscriptions/environments
            var subscriptionsCount = await _context.AzureEnvironments
                .CountAsync(e => e.ClientId == clientId && e.IsActive);

            // Calculate current score (latest completed assessment)
            var latestCompletedAssessment = clientAssessments
                .Where(a => a.Status == "Completed" && a.OverallScore.HasValue)
                .FirstOrDefault();

            var currentScore = latestCompletedAssessment?.OverallScore != null
                ? (int)Math.Round(latestCompletedAssessment.OverallScore.Value)
                : 0;

            // Count active issues (from latest assessment)
            var activeIssues = latestCompletedAssessment != null
                ? await _context.AssessmentFindings
                    .CountAsync(f => f.AssessmentId == latestCompletedAssessment.Id &&
                               (f.Severity == "Critical" || f.Severity == "High"))
                : 0;

            // Get recent assessments for display
            var recentAssessments = clientAssessments
                .Take(5)
                .Select(a => new RecentAssessmentDto
                {
                    AssessmentId = a.Id.ToString(),
                    Name = a.Name,
                    Status = a.Status,
                    OverallScore = a.OverallScore,
                    IssuesCount = GetAssessmentIssuesCount(a.Id).Result,
                    Environment = "Azure", // Default for now
                    Date = a.StartedDate.ToString("yyyy-MM-dd")
                }).ToList();

            var clientData = new ClientDashboardData
            {
                AssessmentsCount = clientAssessments.Count,
                CurrentScore = currentScore,
                ActiveIssues = activeIssues,
                SubscriptionsCount = subscriptionsCount,
                LastAssessmentDate = latestCompletedAssessment?.CompletedDate?.ToString("MMM dd, yyyy") ?? "Never",
                AssessmentsGrowth = new GrowthMetric { Positive = true, Value = 33 },
                ScoreChange = new GrowthMetric { Positive = true, Value = 12 },
                IssuesChange = new GrowthMetric { Positive = false, Value = 8 },
                SubscriptionsChange = new GrowthMetric { Positive = true, Value = 50 },
                RecentAssessments = recentAssessments
            };

            return Ok(clientData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving client dashboard metrics for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("internal")]
    public async Task<ActionResult<InternalDashboardData>> GetInternalMetrics()
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            _logger.LogInformation("Getting internal dashboard metrics for organization {OrganizationId}", organizationId);

            // Get internal Azure environments (not assigned to clients)
            var internalEnvironments = await _context.AzureEnvironments
                .Where(e => e.ClientId == null && e.IsActive)
                .ToListAsync();

            var subscriptionsCount = internalEnvironments
                .SelectMany(e => e.SubscriptionIds ?? new List<string>())
                .Distinct()
                .Count();

            // Get internal assessments (not client-scoped)
            var internalAssessments = await _context.Assessments
                .Where(a => a.OrganizationId == organizationId && a.ClientId == null)
                .OrderByDescending(a => a.StartedDate)
                .ToListAsync();

            // Calculate infrastructure score (latest internal assessment)
            var latestInternalAssessment = internalAssessments
                .Where(a => a.Status == "Completed" && a.OverallScore.HasValue)
                .FirstOrDefault();

            var infraScore = latestInternalAssessment?.OverallScore != null
                ? (int)Math.Round(latestInternalAssessment.OverallScore.Value)
                : 0;

            // Count security issues
            var securityIssues = latestInternalAssessment != null
                ? await _context.AssessmentFindings
                    .CountAsync(f => f.AssessmentId == latestInternalAssessment.Id &&
                               f.Category.Contains("Security"))
                : 0;

            // Mock monthly cost - would come from Azure Cost Management API
            var monthlyCost = 2450;

            // Get recent internal assessments
            var recentAssessments = internalAssessments
                .Take(5)
                .Select(a => new RecentAssessmentDto
                {
                    AssessmentId = a.Id.ToString(),
                    Name = a.Name,
                    Status = a.Status,
                    OverallScore = a.OverallScore,
                    IssuesCount = GetAssessmentIssuesCount(a.Id).Result,
                    Environment = "Internal Azure",
                    Date = a.StartedDate.ToString("yyyy-MM-dd")
                }).ToList();

            var internalData = new InternalDashboardData
            {
                SubscriptionsCount = subscriptionsCount,
                InfraScore = infraScore,
                SecurityIssues = securityIssues,
                MonthlyCost = monthlyCost,
                LastAssessmentDate = latestInternalAssessment?.CompletedDate?.ToString("MMM dd, yyyy") ?? "Never",
                SubscriptionsGrowth = new GrowthMetric { Positive = false, Value = 0 },
                ScoreChange = new GrowthMetric { Positive = true, Value = 5 },
                SecurityIssuesChange = new GrowthMetric { Positive = false, Value = 50 },
                CostChange = new GrowthMetric { Positive = false, Value = 12 },
                TotalAssessments = internalAssessments.Count,
                RecentAssessments = recentAssessments
            };

            return Ok(internalData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving internal dashboard metrics for organization {OrganizationId}", GetOrganizationIdFromContext());
            return StatusCode(500, "Internal server error");
        }
    }

    // Helper methods
    private async Task<List<RecentClientDto>> GetRecentClientActivity(Guid organizationId)
    {
        var clients = await _context.Clients
            .Where(c => c.OrganizationId == organizationId && c.IsActive)
            .OrderByDescending(c => c.CreatedDate)
            .Take(5)
            .ToListAsync();

        var recentClients = new List<RecentClientDto>();

        foreach (var client in clients)
        {
            var assessmentsCount = await _context.Assessments
                .CountAsync(a => a.ClientId == client.ClientId);

            var subscriptionsCount = await _context.AzureEnvironments
                .CountAsync(e => e.ClientId == client.ClientId && e.IsActive);

            // Get last assessment score
            var lastAssessment = await _context.Assessments
                .Where(a => a.ClientId == client.ClientId &&
                           a.Status == "Completed" &&
                           a.OverallScore.HasValue)
                .OrderByDescending(a => a.CompletedDate)
                .FirstOrDefaultAsync();

            var lastScore = lastAssessment?.OverallScore != null
                ? (int)Math.Round(lastAssessment.OverallScore.Value)
                : 0;

            recentClients.Add(new RecentClientDto
            {
                Id = client.ClientId,
                Name = client.Name,
                AssessmentsCount = assessmentsCount,
                SubscriptionsCount = subscriptionsCount,
                LastScore = lastScore,
                LastAssessment = lastAssessment?.CompletedDate?.ToString("yyyy-MM-dd") ?? "Never"
            });
        }

        return recentClients;
    }

    private async Task<int> GetAssessmentIssuesCount(Guid assessmentId)
    {
        return await _context.AssessmentFindings
            .CountAsync(f => f.AssessmentId == assessmentId);
    }

    private Guid? GetOrganizationIdFromContext()
    {
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        if (Guid.TryParse(orgIdClaim, out var organizationId))
        {
            return organizationId;
        }
        return null;
    }

    private Guid? GetCurrentCustomerId()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(customerIdClaim, out var customerId))
        {
            return customerId;
        }
        return null;
    }
}

// DTOs for dashboard responses
public class CompanyDashboardData
{
    public int TotalClients { get; set; }
    public int TotalAssessments { get; set; }
    public int TeamMembers { get; set; }
    public int AverageClientScore { get; set; }
    public GrowthMetric ClientsGrowth { get; set; } = new();
    public GrowthMetric AssessmentsGrowth { get; set; } = new();
    public GrowthMetric TeamGrowth { get; set; } = new();
    public GrowthMetric ScoreImprovement { get; set; } = new();
    public List<RecentClientDto> RecentClients { get; set; } = new();
}

public class ClientDashboardData
{
    public int AssessmentsCount { get; set; }
    public int CurrentScore { get; set; }
    public int ActiveIssues { get; set; }
    public int SubscriptionsCount { get; set; }
    public string LastAssessmentDate { get; set; } = string.Empty;
    public GrowthMetric AssessmentsGrowth { get; set; } = new();
    public GrowthMetric ScoreChange { get; set; } = new();
    public GrowthMetric IssuesChange { get; set; } = new();
    public GrowthMetric SubscriptionsChange { get; set; } = new();
    public List<RecentAssessmentDto> RecentAssessments { get; set; } = new();
}

public class InternalDashboardData
{
    public int SubscriptionsCount { get; set; }
    public int InfraScore { get; set; }
    public int SecurityIssues { get; set; }
    public int MonthlyCost { get; set; }
    public string LastAssessmentDate { get; set; } = string.Empty;
    public GrowthMetric SubscriptionsGrowth { get; set; } = new();
    public GrowthMetric ScoreChange { get; set; } = new();
    public GrowthMetric SecurityIssuesChange { get; set; } = new();
    public GrowthMetric CostChange { get; set; } = new();
    public int TotalAssessments { get; set; }
    public List<RecentAssessmentDto> RecentAssessments { get; set; } = new();
}

public class GrowthMetric
{
    public bool Positive { get; set; }
    public int Value { get; set; }
}

public class RecentClientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AssessmentsCount { get; set; }
    public int SubscriptionsCount { get; set; }
    public int LastScore { get; set; }
    public string LastAssessment { get; set; } = string.Empty;
}

public class RecentAssessmentDto
{
    public string AssessmentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? OverallScore { get; set; }
    public int IssuesCount { get; set; }
    public string Environment { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
}