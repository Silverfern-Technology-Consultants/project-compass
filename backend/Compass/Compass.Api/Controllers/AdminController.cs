using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Compass.Data;
using Compass.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly CompassDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(CompassDbContext context, ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Delete a customer account by email (for testing purposes)
    /// </summary>
    [HttpDelete("customers/{email}")]
    public async Task<IActionResult> DeleteCustomerByEmail(string email)
    {
        try
        {
            // Find the customer by email
            var customer = await _context.Customers
                .Include(c => c.Assessments)
                .ThenInclude(a => a.Findings)
                .Include(c => c.Subscriptions)
                .Include(c => c.AzureEnvironments)
                .FirstOrDefaultAsync(c => c.Email == email);

            if (customer == null)
            {
                return NotFound(new { message = $"Customer with email {email} not found" });
            }

            // Delete related data in correct order (due to foreign key constraints)

            // 1. Delete assessment findings
            var findings = customer.Assessments.SelectMany(a => a.Findings).ToList();
            if (findings.Any())
            {
                _context.AssessmentFindings.RemoveRange(findings);
                _logger.LogInformation("Deleted {Count} assessment findings for customer {Email}", findings.Count, email);
            }

            // 2. Delete assessments
            if (customer.Assessments.Any())
            {
                _context.Assessments.RemoveRange(customer.Assessments);
                _logger.LogInformation("Deleted {Count} assessments for customer {Email}", customer.Assessments.Count, email);
            }

            // 3. Delete Azure environments
            if (customer.AzureEnvironments.Any())
            {
                _context.AzureEnvironments.RemoveRange(customer.AzureEnvironments);
                _logger.LogInformation("Deleted {Count} Azure environments for customer {Email}", customer.AzureEnvironments.Count, email);
            }

            // 4. Delete subscriptions
            if (customer.Subscriptions.Any())
            {
                _context.Subscriptions.RemoveRange(customer.Subscriptions);
                _logger.LogInformation("Deleted {Count} subscriptions for customer {Email}", customer.Subscriptions.Count, email);
            }

            // 5. Finally delete the customer
            _context.Customers.Remove(customer);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully deleted customer account: {Email}", email);

            return Ok(new
            {
                message = $"Customer account {email} and all related data deleted successfully",
                deletedCustomer = new
                {
                    customerId = customer.CustomerId,
                    email = customer.Email,
                    firstName = customer.FirstName,
                    lastName = customer.LastName,
                    companyName = customer.CompanyName
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer account: {Email}", email);
            return StatusCode(500, new { message = "An error occurred while deleting the account" });
        }
    }

    /// <summary>
    /// List all customers (for testing/admin purposes)
    /// </summary>
    [HttpGet("customers")]
    public async Task<IActionResult> GetAllCustomers()
    {
        try
        {
            var customers = await _context.Customers
                .Select(c => new
                {
                    customerId = c.CustomerId,
                    email = c.Email,
                    firstName = c.FirstName,
                    lastName = c.LastName,
                    companyName = c.CompanyName,
                    emailVerified = c.EmailVerified,
                    createdDate = c.CreatedDate,
                    lastLoginDate = c.LastLoginDate
                })
                .ToListAsync();

            return Ok(new { customers, count = customers.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customers list");
            return StatusCode(500, new { message = "An error occurred while retrieving customers" });
        }
    }

    /// <summary>
    /// Delete all test data (for testing purposes - use with caution!)
    /// </summary>
    [HttpDelete("reset-database")]
    public async Task<IActionResult> ResetDatabase()
    {
        try
        {
            // WARNING: This deletes ALL data!
            _logger.LogWarning("Database reset requested - deleting ALL data");

            // Delete in correct order due to foreign key constraints
            var findingsCount = await _context.AssessmentFindings.CountAsync();
            var assessmentsCount = await _context.Assessments.CountAsync();
            var environmentsCount = await _context.AzureEnvironments.CountAsync();
            var subscriptionsCount = await _context.Subscriptions.CountAsync();
            var customersCount = await _context.Customers.CountAsync();

            // 1. Delete all assessment findings
            if (findingsCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM AssessmentFindings");
                _logger.LogInformation("Deleted {Count} assessment findings", findingsCount);
            }

            // 2. Delete all assessments
            if (assessmentsCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Assessments");
                _logger.LogInformation("Deleted {Count} assessments", assessmentsCount);
            }

            // 3. Delete all Azure environments
            if (environmentsCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM AzureEnvironments");
                _logger.LogInformation("Deleted {Count} Azure environments", environmentsCount);
            }

            // 4. Delete all subscriptions
            if (subscriptionsCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Subscriptions");
                _logger.LogInformation("Deleted {Count} subscriptions", subscriptionsCount);
            }

            // 5. Delete all customers
            if (customersCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Customers");
                _logger.LogInformation("Deleted {Count} customers", customersCount);
            }

            // Reset identity columns (if using SQL Server)
            await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('AssessmentFindings', RESEED, 0)");
            await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Assessments', RESEED, 0)");

            await _context.SaveChangesAsync();

            _logger.LogWarning("Database reset completed successfully");

            return Ok(new
            {
                message = "Database reset completed successfully",
                deletedCounts = new
                {
                    customers = customersCount,
                    subscriptions = subscriptionsCount,
                    azureEnvironments = environmentsCount,
                    assessments = assessmentsCount,
                    assessmentFindings = findingsCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting database");
            return StatusCode(500, new { message = "An error occurred while resetting the database" });
        }
    }

    /// <summary>
    /// Get customer details by email (for testing/debugging)
    /// </summary>
    [HttpGet("customers/{email}")]
    public async Task<IActionResult> GetCustomerByEmail(string email)
    {
        try
        {
            var customer = await _context.Customers
                .Include(c => c.Assessments)
                .Include(c => c.AzureEnvironments)
                .Include(c => c.Subscriptions)
                .FirstOrDefaultAsync(c => c.Email == email);

            if (customer == null)
            {
                return NotFound(new { message = $"Customer with email {email} not found" });
            }

            return Ok(new
            {
                customer = new
                {
                    customerId = customer.CustomerId,
                    email = customer.Email,
                    firstName = customer.FirstName,
                    lastName = customer.LastName,
                    companyName = customer.CompanyName,
                    emailVerified = customer.EmailVerified,
                    emailVerificationToken = customer.EmailVerificationToken,
                    emailVerificationExpiry = customer.EmailVerificationExpiry,
                    createdDate = customer.CreatedDate,
                    lastLoginDate = customer.LastLoginDate
                },
                counts = new
                {
                    assessments = customer.Assessments.Count,
                    azureEnvironments = customer.AzureEnvironments.Count,
                    subscriptions = customer.Subscriptions.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving customer: {Email}", email);
            return StatusCode(500, new { message = "An error occurred while retrieving customer details" });
        }
    }

    /// <summary>
    /// Manually verify a customer's email (for testing purposes)
    /// </summary>
    [HttpPost("customers/{email}/verify")]
    public async Task<IActionResult> ManuallyVerifyCustomer(string email)
    {
        try
        {
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == email);

            if (customer == null)
            {
                return NotFound(new { message = $"Customer with email {email} not found" });
            }

            if (customer.EmailVerified)
            {
                return BadRequest(new { message = "Customer email is already verified" });
            }

            customer.EmailVerified = true;
            customer.EmailVerificationToken = null;
            customer.EmailVerificationExpiry = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Manually verified email for customer: {Email}", email);

            return Ok(new
            {
                message = $"Email verification completed for {email}",
                customer = new
                {
                    email = customer.Email,
                    emailVerified = customer.EmailVerified
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error manually verifying customer: {Email}", email);
            return StatusCode(500, new { message = "An error occurred while verifying the customer" });
        }
    }

    /// <summary>
    /// Get database statistics (for monitoring)
    /// </summary>
    [HttpGet("database-stats")]
    public async Task<IActionResult> GetDatabaseStats()
    {
        try
        {
            var stats = new
            {
                customers = await _context.Customers.CountAsync(),
                verifiedCustomers = await _context.Customers.CountAsync(c => c.EmailVerified),
                unverifiedCustomers = await _context.Customers.CountAsync(c => !c.EmailVerified),
                subscriptions = await _context.Subscriptions.CountAsync(),
                azureEnvironments = await _context.AzureEnvironments.CountAsync(),
                assessments = await _context.Assessments.CountAsync(),
                assessmentFindings = await _context.AssessmentFindings.CountAsync(),
                recentRegistrations = await _context.Customers
                    .Where(c => c.CreatedDate >= DateTime.UtcNow.AddDays(-7))
                    .CountAsync()
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database statistics");
            return StatusCode(500, new { message = "An error occurred while retrieving database statistics" });
        }
    }
}