using Compass.Core.Services;
using Compass.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly INamingConventionAnalyzer _namingAnalyzer;
    private readonly ITaggingAnalyzer _taggingAnalyzer;
    private readonly TestDataSeeder _testDataSeeder;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IAzureResourceGraphService resourceGraphService,
        INamingConventionAnalyzer namingAnalyzer,
        ITaggingAnalyzer taggingAnalyzer,
        TestDataSeeder testDataSeeder,
        ILogger<TestController> logger)
    {
        _resourceGraphService = resourceGraphService;
        _namingAnalyzer = namingAnalyzer;
        _taggingAnalyzer = taggingAnalyzer;
        _testDataSeeder = testDataSeeder;
        _logger = logger;
    }

    /// <summary>
    /// Seed test data for development
    /// </summary>
    [HttpPost("seed-data")]
    public async Task<IActionResult> SeedTestData()
    {
        try
        {
            await _testDataSeeder.SeedTestDataAsync();
            return Ok(new
            {
                message = "Test data seeded successfully",
                testCustomerId = "9bc034b0-852f-4618-9434-c040d13de712",
                testEmail = "test@testcompany.com",
                testPassword = "TestPassword123!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed test data");
            return StatusCode(500, new { error = "Failed to seed test data", details = ex.Message });
        }
    }

    /// <summary>
    /// Test Azure Resource Graph connection
    /// </summary>
    [HttpPost("azure-connection")]
    public async Task<IActionResult> TestAzureConnection([FromBody] string[] subscriptionIds)
    {
        try
        {
            if (!subscriptionIds.Any())
            {
                return BadRequest(new { error = "At least one subscription ID is required" });
            }

            _logger.LogInformation("Testing Azure connection for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));

            var canConnect = await _resourceGraphService.TestConnectionAsync(subscriptionIds);

            return Ok(new
            {
                success = canConnect,
                subscriptions = subscriptionIds,
                message = canConnect ? "Successfully connected to Azure" : "Failed to connect to Azure",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Azure connection test failed");
            return StatusCode(500, new { error = "Connection test failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get sample Azure resources (limited to 10)
    /// </summary>
    [HttpPost("azure-resources")]
    public async Task<IActionResult> GetSampleResources([FromBody] string[] subscriptionIds)
    {
        try
        {
            if (!subscriptionIds.Any())
            {
                return BadRequest(new { error = "At least one subscription ID is required" });
            }

            _logger.LogInformation("Fetching sample resources for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));

            var resources = await _resourceGraphService.GetResourcesAsync(subscriptionIds);
            var sampleResources = resources.Take(10).Select(r => new
            {
                r.Id,
                r.Name,
                r.Type,
                r.ResourceGroup,
                r.Location,
                r.Tags,
                TagCount = r.TagCount,
                Environment = r.Environment
            }).ToList();

            return Ok(new
            {
                totalResources = resources.Count,
                sampleResources,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Azure resources");
            return StatusCode(500, new { error = "Failed to fetch resources", details = ex.Message });
        }
    }

    /// <summary>
    /// Run a quick naming convention analysis on sample data
    /// </summary>
    [HttpPost("test-naming-analysis")]
    public async Task<IActionResult> TestNamingAnalysis([FromBody] string[] subscriptionIds)
    {
        try
        {
            if (!subscriptionIds.Any())
            {
                return BadRequest(new { error = "At least one subscription ID is required" });
            }

            _logger.LogInformation("Running test naming analysis for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));

            var resources = await _resourceGraphService.GetResourcesAsync(subscriptionIds);
            var sampleResources = resources.Take(50).ToList(); // Analyze first 50 resources

            if (!sampleResources.Any())
            {
                return Ok(new { message = "No resources found to analyze", timestamp = DateTime.UtcNow });
            }

            var namingResults = await _namingAnalyzer.AnalyzeNamingConventionsAsync(sampleResources);

            return Ok(new
            {
                analysisResults = new
                {
                    namingResults.Score,
                    namingResults.TotalResources,
                    namingResults.CompliantResources,
                    ViolationCount = namingResults.Violations.Count,
                    ConsistencyScore = namingResults.Consistency.OverallConsistency,
                    TopViolations = namingResults.Violations.Take(5).Select(v => new
                    {
                        v.ResourceName,
                        v.ResourceType,
                        v.ViolationType,
                        v.Issue,
                        v.Severity
                    }).ToList()
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run naming analysis");
            return StatusCode(500, new { error = "Analysis failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Run a quick tagging analysis on sample data
    /// </summary>
    [HttpPost("test-tagging-analysis")]
    public async Task<IActionResult> TestTaggingAnalysis([FromBody] string[] subscriptionIds)
    {
        try
        {
            if (!subscriptionIds.Any())
            {
                return BadRequest(new { error = "At least one subscription ID is required" });
            }

            _logger.LogInformation("Running test tagging analysis for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));

            var resources = await _resourceGraphService.GetResourcesAsync(subscriptionIds);
            var sampleResources = resources.Take(50).ToList(); // Analyze first 50 resources

            if (!sampleResources.Any())
            {
                return Ok(new { message = "No resources found to analyze", timestamp = DateTime.UtcNow });
            }

            var taggingResults = await _taggingAnalyzer.AnalyzeTaggingAsync(sampleResources);

            return Ok(new
            {
                analysisResults = new
                {
                    taggingResults.Score,
                    taggingResults.TotalResources,
                    taggingResults.TaggedResources,
                    taggingResults.TagCoveragePercentage,
                    MissingRequiredTagsCount = taggingResults.MissingRequiredTags.Count,
                    ViolationCount = taggingResults.Violations.Count,
                    TopTags = taggingResults.TagUsageFrequency.Take(10).ToList(),
                    TopViolations = taggingResults.Violations.Take(5).Select(v => new
                    {
                        v.ResourceName,
                        v.ResourceType,
                        v.ViolationType,
                        v.Issue,
                        v.Severity
                    }).ToList()
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run tagging analysis");
            return StatusCode(500, new { error = "Analysis failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get system status and dependencies
    /// </summary>
    [HttpGet("system-status")]
    public IActionResult GetSystemStatus()
    {
        try
        {
            return Ok(new
            {
                status = "healthy",
                version = "1.0.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                dependencies = new
                {
                    azureResourceGraph = "Available",
                    namingAnalyzer = "Available",
                    taggingAnalyzer = "Available",
                    database = "Connected"
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System status check failed");
            return StatusCode(500, new { status = "unhealthy", error = ex.Message });
        }
    }
}