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
    /// Delete a user by ID (simplified version for current database schema)
    /// </summary>
    [HttpDelete("delete-user-and-organization/{userId}")]
    public async Task<IActionResult> DeleteUserAndOrganization(Guid userId)
    {
        try
        {
            _logger.LogInformation("Starting deletion process for user {UserId}", userId);

            // Find the user with all related data (without OrganizationId for now)
            var user = await _context.Customers
                .Include(c => c.Assessments)
                .ThenInclude(a => a.Findings)
                .Include(c => c.Subscriptions)
                .Include(c => c.AzureEnvironments)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found", userId);
                return NotFound(new { message = $"User with ID {userId} not found" });
            }

            _logger.LogInformation("Found user {Email} with role {Role}", user.Email, user.Role);

            // For now, just delete the user and their data since OrganizationId doesn't exist yet
            var userDeletionResult = await DeleteUserCascade(user);

            var deletionSummary = new
            {
                deletedUser = new
                {
                    customerId = user.CustomerId,
                    email = user.Email,
                    firstName = user.FirstName,
                    lastName = user.LastName,
                    companyName = user.CompanyName,
                    role = user.Role
                },
                assessmentsDeleted = userDeletionResult.assessmentsDeleted,
                findingsDeleted = userDeletionResult.findingsDeleted,
                subscriptionsDeleted = userDeletionResult.subscriptionsDeleted,
                azureEnvironmentsDeleted = userDeletionResult.azureEnvironmentsDeleted,
                invitationsDeleted = userDeletionResult.invitationsDeleted
            };

            _logger.LogInformation("Successfully deleted user {Email}", user.Email);
            return Ok(new
            {
                message = $"User {user.Email} deleted successfully",
                details = deletionSummary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user: {UserId}. Exception: {Message}", userId, ex.Message);
            _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
            return StatusCode(500, new
            {
                message = "An error occurred while deleting the user",
                error = ex.Message,
                details = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Delete an entire organization and all related data
    /// </summary>
    private async Task<OrganizationDeletionResult> DeleteOrganizationCascade(Guid organizationId)
    {
        var result = new OrganizationDeletionResult();

        // Get all members of the organization
        var organizationMembers = await _context.Customers
            .Include(c => c.Assessments)
            .ThenInclude(a => a.Findings)
            .Include(c => c.Subscriptions)
            .Include(c => c.AzureEnvironments)
            .Where(c => c.OrganizationId == organizationId)
            .ToListAsync();

        // Delete all team invitations for this organization
        var invitations = await _context.TeamInvitations
            .Where(ti => ti.OrganizationId == organizationId)
            .ToListAsync();

        if (invitations.Any())
        {
            _context.TeamInvitations.RemoveRange(invitations);
            result.invitationsDeleted = invitations.Count;
            _logger.LogInformation("Deleted {Count} team invitations for organization {OrganizationId}", invitations.Count, organizationId);
        }

        // Delete all data for each organization member
        foreach (var member in organizationMembers)
        {
            var memberDeletionResult = await DeleteUserCascade(member, saveChanges: false);
            result.assessmentsDeleted += memberDeletionResult.assessmentsDeleted;
            result.findingsDeleted += memberDeletionResult.findingsDeleted;
            result.subscriptionsDeleted += memberDeletionResult.subscriptionsDeleted;
            result.azureEnvironmentsDeleted += memberDeletionResult.azureEnvironmentsDeleted;
        }

        result.membersDeleted = organizationMembers.Count;

        // Delete the organization itself
        var organization = await _context.Organizations.FindAsync(organizationId);
        if (organization != null)
        {
            _context.Organizations.Remove(organization);
            _logger.LogInformation("Deleted organization {OrganizationId}", organizationId);
        }

        await _context.SaveChangesAsync();
        return result;
    }

    /// <summary>
    /// Delete a single user and all their related data (simplified for current schema)
    /// </summary>
    private async Task<UserDeletionResult> DeleteUserCascade(Customer user, bool saveChanges = true)
    {
        var result = new UserDeletionResult();

        // Delete assessment findings
        var findings = user.Assessments.SelectMany(a => a.Findings).ToList();
        if (findings.Any())
        {
            _context.AssessmentFindings.RemoveRange(findings);
            result.findingsDeleted = findings.Count;
            _logger.LogInformation("Deleted {Count} assessment findings for user {Email}", findings.Count, user.Email);
        }

        // Delete assessments
        if (user.Assessments.Any())
        {
            _context.Assessments.RemoveRange(user.Assessments);
            result.assessmentsDeleted = user.Assessments.Count;
            _logger.LogInformation("Deleted {Count} assessments for user {Email}", user.Assessments.Count, user.Email);
        }

        // Delete Azure environments
        if (user.AzureEnvironments.Any())
        {
            _context.AzureEnvironments.RemoveRange(user.AzureEnvironments);
            result.azureEnvironmentsDeleted = user.AzureEnvironments.Count;
            _logger.LogInformation("Deleted {Count} Azure environments for user {Email}", user.AzureEnvironments.Count, user.Email);
        }

        // Delete subscriptions
        if (user.Subscriptions.Any())
        {
            _context.Subscriptions.RemoveRange(user.Subscriptions);
            result.subscriptionsDeleted = user.Subscriptions.Count;
            _logger.LogInformation("Deleted {Count} subscriptions for user {Email}", user.Subscriptions.Count, user.Email);
        }

        // Delete any team invitations sent by this user (only if TeamInvitations table exists)
        try
        {
            var sentInvitations = await _context.TeamInvitations
                .Where(ti => ti.InvitedByCustomerId == user.CustomerId)
                .ToListAsync();

            if (sentInvitations.Any())
            {
                _context.TeamInvitations.RemoveRange(sentInvitations);
                result.invitationsDeleted = sentInvitations.Count;
                _logger.LogInformation("Deleted {Count} team invitations sent by user {Email}", sentInvitations.Count, user.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not delete team invitations (table may not exist): {Message}", ex.Message);
            // Continue without failing - TeamInvitations table might not exist yet
        }

        // Finally delete the user
        _context.Customers.Remove(user);

        if (saveChanges)
        {
            await _context.SaveChangesAsync();
        }

        return result;
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
                .FirstOrDefaultAsync(c => c.Email == email);

            if (customer == null)
            {
                return NotFound(new { message = $"Customer with email {email} not found" });
            }

            // Use the new delete user method
            return await DeleteUserAndOrganization(customer.CustomerId);
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
                    role = c.Role,
                    organizationId = c.OrganizationId,
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
    /// List all organizations (for admin purposes)
    /// </summary>
    [HttpGet("organizations")]
    public async Task<IActionResult> GetAllOrganizations()
    {
        try
        {
            var organizations = await _context.Organizations
                .Include(o => o.Members)
                .Select(o => new
                {
                    organizationId = o.OrganizationId,
                    name = o.Name,
                    description = o.Description,
                    createdDate = o.CreatedDate,
                    memberCount = o.Members.Count,
                    owners = o.Members.Where(m => m.Role == "Owner").Select(m => new
                    {
                        customerId = m.CustomerId,
                        email = m.Email,
                        name = $"{m.FirstName} {m.LastName}".Trim()
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new { organizations, count = organizations.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving organizations list");
            return StatusCode(500, new { message = "An error occurred while retrieving organizations" });
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
            var invitationsCount = await _context.TeamInvitations.CountAsync();
            var customersCount = await _context.Customers.CountAsync();
            var organizationsCount = await _context.Organizations.CountAsync();

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

            // 5. Delete all team invitations
            if (invitationsCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM TeamInvitations");
                _logger.LogInformation("Deleted {Count} team invitations", invitationsCount);
            }

            // 6. Delete all customers
            if (customersCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Customers");
                _logger.LogInformation("Deleted {Count} customers", customersCount);
            }

            // 7. Delete all organizations
            if (organizationsCount > 0)
            {
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM Organizations");
                _logger.LogInformation("Deleted {Count} organizations", organizationsCount);
            }

            await _context.SaveChangesAsync();

            _logger.LogWarning("Database reset completed successfully");

            return Ok(new
            {
                message = "Database reset completed successfully",
                deletedCounts = new
                {
                    customers = customersCount,
                    organizations = organizationsCount,
                    subscriptions = subscriptionsCount,
                    azureEnvironments = environmentsCount,
                    assessments = assessmentsCount,
                    assessmentFindings = findingsCount,
                    teamInvitations = invitationsCount
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
                    role = customer.Role,
                    organizationId = customer.OrganizationId,
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
                organizations = await _context.Organizations.CountAsync(),
                verifiedCustomers = await _context.Customers.CountAsync(c => c.EmailVerified),
                unverifiedCustomers = await _context.Customers.CountAsync(c => !c.EmailVerified),
                subscriptions = await _context.Subscriptions.CountAsync(),
                azureEnvironments = await _context.AzureEnvironments.CountAsync(),
                assessments = await _context.Assessments.CountAsync(),
                assessmentFindings = await _context.AssessmentFindings.CountAsync(),
                teamInvitations = await _context.TeamInvitations.CountAsync(),
                pendingInvitations = await _context.TeamInvitations.CountAsync(ti => ti.Status == "Pending"),
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

// Helper classes for deletion results
public class OrganizationDeletionResult
{
    public int membersDeleted { get; set; }
    public int assessmentsDeleted { get; set; }
    public int findingsDeleted { get; set; }
    public int subscriptionsDeleted { get; set; }
    public int azureEnvironmentsDeleted { get; set; }
    public int invitationsDeleted { get; set; }
}

public class UserDeletionResult
{
    public int assessmentsDeleted { get; set; }
    public int findingsDeleted { get; set; }
    public int subscriptionsDeleted { get; set; }
    public int azureEnvironmentsDeleted { get; set; }
    public int invitationsDeleted { get; set; }
}