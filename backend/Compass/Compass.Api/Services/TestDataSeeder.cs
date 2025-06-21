// Create this file: Compass.Api/Services/TestDataSeeder.cs
using Compass.Data;
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Api.Services;

public class TestDataSeeder
{
    private readonly CompassDbContext _context;
    private readonly ILogger<TestDataSeeder> _logger;

    public TestDataSeeder(CompassDbContext context, ILogger<TestDataSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedTestDataAsync()
    {
        try
        {
            // Check if test customer already exists
            var testCustomerId = Guid.Parse("9bc034b0-852f-4618-9434-c040d13de712");
            var existingCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == testCustomerId);

            if (existingCustomer != null)
            {
                _logger.LogInformation("Test customer already exists");
                return;
            }

            _logger.LogInformation("Creating test customer and subscription...");

            // Create test customer
            var testCustomer = new Customer
            {
                CustomerId = testCustomerId,
                CompanyName = "Test Company",
                FirstName = "Test",
                LastName = "User",
                Email = "test@testcompany.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TestPassword123!"),
                EmailVerified = true,
                IsActive = true,
                IsTrialAccount = true,
                TrialStartDate = DateTime.UtcNow,
                TrialEndDate = DateTime.UtcNow.AddDays(30),
                CreatedDate = DateTime.UtcNow,
                ContactPhone = "555-0123",
                Country = "United States",
                CompanySize = "1-10",
                Industry = "Technology"
            };

            _context.Customers.Add(testCustomer);

            // Create test subscription
            var testSubscription = new Subscription
            {
                SubscriptionId = Guid.NewGuid(),
                CustomerId = testCustomerId,
                PlanType = "Trial",
                Status = "Active",
                BillingCycle = "Monthly",
                MonthlyPrice = 0,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(30),
                MaxSubscriptions = 10,
                MaxAssessmentsPerMonth = null, // Unlimited for testing
                IncludesAPI = true,
                IncludesWhiteLabel = false,
                IncludesCustomBranding = true,
                SupportLevel = "Email",
                CreatedDate = DateTime.UtcNow,
                ModifiedDate = DateTime.UtcNow
            };

            _context.Subscriptions.Add(testSubscription);

            // Create test Azure environment
            var testEnvironment = new AzureEnvironment
            {
                AzureEnvironmentId = Guid.Parse("00000000-0000-0000-0000-000000000000"),
                CustomerId = testCustomerId,
                Name = "Test Environment",
                Description = "Test environment for development",
                TenantId = "test-tenant-id",
                SubscriptionIds = new List<string> { "ac109ffa-33eb-4647-9652-556b8e52a3f3" },
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.AzureEnvironments.Add(testEnvironment);

            // Save all changes
            await _context.SaveChangesAsync();

            _logger.LogInformation("Test data seeded successfully!");
            _logger.LogInformation("Test Customer ID: {CustomerId}", testCustomerId);
            _logger.LogInformation("Test Email: test@testcompany.com");
            _logger.LogInformation("Test Password: TestPassword123!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed test data");
            throw;
        }
    }
}