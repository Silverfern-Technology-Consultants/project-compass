using Compass.Core.Services;
using Compass.Api.Services;
using Compass.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly INamingConventionAnalyzer _namingAnalyzer;
    private readonly ITaggingAnalyzer _taggingAnalyzer;
    private readonly TestDataSeeder _testDataSeeder;
    private readonly CompassDbContext _context;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IAzureResourceGraphService resourceGraphService,
        INamingConventionAnalyzer namingAnalyzer,
        ITaggingAnalyzer taggingAnalyzer,
        TestDataSeeder testDataSeeder,
        CompassDbContext context,
        ILogger<TestController> logger)
    {
        _resourceGraphService = resourceGraphService;
        _namingAnalyzer = namingAnalyzer;
        _taggingAnalyzer = taggingAnalyzer;
        _testDataSeeder = testDataSeeder;
        _context = context;
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
    /// Clear all customers and reseed test data (DANGEROUS - Dev only)
    /// </summary>
    [HttpPost("reset-database")]
    public async Task<IActionResult> ResetDatabase()
    {
        try
        {
            _logger.LogWarning("RESETTING DATABASE - Deleting all customers and related data");

            // Delete all customers and related data (cascading)
            var customers = await _context.Customers.ToListAsync();
            var subscriptions = await _context.Subscriptions.ToListAsync();
            var assessments = await _context.Assessments.ToListAsync();
            var findings = await _context.AssessmentFindings.ToListAsync();
            var azureEnvironments = await _context.AzureEnvironments.ToListAsync();
            var usageMetrics = await _context.UsageMetrics.ToListAsync();

            _logger.LogInformation("Deleting {CustomerCount} customers, {SubCount} subscriptions, {AssessmentCount} assessments",
                customers.Count, subscriptions.Count, assessments.Count);

            // Remove in order to respect foreign key constraints
            _context.UsageMetrics.RemoveRange(usageMetrics);
            _context.AssessmentFindings.RemoveRange(findings);
            _context.Assessments.RemoveRange(assessments);
            _context.AzureEnvironments.RemoveRange(azureEnvironments);
            _context.Subscriptions.RemoveRange(subscriptions);
            _context.Customers.RemoveRange(customers);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Database cleared successfully. Now reseeding test data...");

            // Reseed test data
            await _testDataSeeder.SeedTestDataAsync();

            return Ok(new
            {
                message = "Database reset and reseeded successfully",
                deletedCustomers = customers.Count,
                deletedSubscriptions = subscriptions.Count,
                deletedAssessments = assessments.Count,
                testCustomerId = "9bc034b0-852f-4618-9434-c040d13de712",
                testEmail = "test@testcompany.com",
                testPassword = "TestPassword123!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset database");
            return StatusCode(500, new { error = "Failed to reset database", details = ex.Message });
        }
    }

    /// <summary>
    /// Reset test user password (for debugging)
    /// </summary>
    [HttpPost("reset-test-password")]
    public async Task<IActionResult> ResetTestPassword()
    {
        try
        {
            // First, let's see what customers exist
            var allCustomers = await _context.Customers
                .Select(c => new { c.Email, c.CustomerId, c.FirstName, c.LastName })
                .ToListAsync();

            _logger.LogInformation("Found {CustomerCount} customers in database:", allCustomers.Count);
            foreach (var c in allCustomers)
            {
                _logger.LogInformation("Customer: {Email} - {Id} - {Name}", c.Email, c.CustomerId, $"{c.FirstName} {c.LastName}");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == "test@testcompany.com");

            if (customer == null)
            {
                // Try case-insensitive search
                customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email.ToLower() == "test@testcompany.com");
            }

            if (customer == null)
            {
                return NotFound(new
                {
                    message = "Test customer not found",
                    searchedEmail = "test@testcompany.com",
                    existingCustomers = allCustomers
                });
            }

            // Generate a fresh password hash
            var newPasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!");

            _logger.LogInformation("Old password hash: {OldHash}", customer.PasswordHash);
            _logger.LogInformation("New password hash: {NewHash}", newPasswordHash);

            // Update the password
            customer.PasswordHash = newPasswordHash;
            await _context.SaveChangesAsync();

            // Test the verification immediately
            var verificationTest = BCrypt.Net.BCrypt.Verify("TestPassword123!", newPasswordHash);

            return Ok(new
            {
                message = "Test password reset successfully",
                email = customer.Email,
                password = "TestPassword123!",
                verificationTest = verificationTest,
                customerId = customer.CustomerId.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset test password");
            return StatusCode(500, new { error = "Failed to reset test password", details = ex.Message });
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
            _logger.LogError(ex, "Failed to fetch sample resources");
            return StatusCode(500, new { error = "Failed to fetch resources", details = ex.Message });
        }
    }

    /// <summary>
    /// Test naming convention analysis
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

            _logger.LogInformation("Testing naming convention analysis for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));

            var resources = await _resourceGraphService.GetResourcesAsync(subscriptionIds);
            var namingResults = await _namingAnalyzer.AnalyzeNamingConventionsAsync(resources);

            return Ok(new
            {
                resourceCount = resources.Count,
                overallScore = namingResults.Score,
                violations = namingResults.Violations.Take(5), // First 5 violations
                patterns = namingResults.PatternDistribution,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Naming analysis test failed");
            return StatusCode(500, new { error = "Naming analysis failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test tagging analysis
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

            _logger.LogInformation("Testing tagging analysis for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));

            var resources = await _resourceGraphService.GetResourcesAsync(subscriptionIds);
            var taggingResults = await _taggingAnalyzer.AnalyzeTaggingAsync(resources);

            return Ok(new
            {
                resourceCount = resources.Count,
                overallScore = taggingResults.Score,
                coverage = taggingResults.TagCoveragePercentage,
                violations = taggingResults.Violations.Take(5), // First 5 violations
                tagUsage = taggingResults.TagUsageFrequency.Take(10), // Top 10 tags
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tagging analysis test failed");
            return StatusCode(500, new { error = "Tagging analysis failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Get system status and health
    /// </summary>
    [HttpGet("system-status")]
    public async Task<IActionResult> GetSystemStatus()
    {
        try
        {
            // Test database connection
            var dbHealthy = false;
            try
            {
                dbHealthy = await _context.Database.CanConnectAsync();
            }
            catch
            {
                dbHealthy = false;
            }

            // Test Azure Resource Graph connection
            var azureHealthy = false;
            try
            {
                azureHealthy = await _resourceGraphService.TestConnectionAsync(new[] { "test-subscription" });
            }
            catch
            {
                azureHealthy = false;
            }

            return Ok(new
            {
                status = "online",
                database = dbHealthy ? "healthy" : "unhealthy",
                azureConnection = azureHealthy ? "healthy" : "unhealthy",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "System status check failed");
            return StatusCode(500, new { error = "System status check failed", details = ex.Message });
        }
    }
}