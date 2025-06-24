using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Compass.Core.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamController : ControllerBase
{
    private readonly CompassDbContext _context;
    private readonly ICustomerRepository _customerRepository;
    private readonly IAssessmentRepository _assessmentRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<TeamController> _logger;

    public TeamController(
        CompassDbContext context,
        ICustomerRepository customerRepository,
        IAssessmentRepository assessmentRepository,
        IEmailService emailService,
        ILogger<TeamController> logger)
    {
        _context = context;
        _customerRepository = customerRepository;
        _assessmentRepository = assessmentRepository;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpGet("members")]
    public async Task<ActionResult<List<TeamMemberDto>>> GetTeamMembers()
    {
        try
        {
            var currentCustomerId = GetCurrentCustomerId();
            var currentOrganizationId = GetCurrentOrganizationId();

            if (currentCustomerId == null || currentOrganizationId == null)
                return Unauthorized("Invalid authentication or missing organization");

            var teamMembers = new List<TeamMemberDto>();

            // Get all organization members
            var organizationMembers = await _context.Customers
                .Where(c => c.OrganizationId == currentOrganizationId && c.IsActive)
                .ToListAsync();

            foreach (var member in organizationMembers)
            {
                var assessments = await _assessmentRepository.GetByCustomerIdAsync(member.CustomerId);
                teamMembers.Add(new TeamMemberDto
                {
                    Id = member.CustomerId,
                    Name = $"{member.FirstName} {member.LastName}".Trim(),
                    Email = member.Email,
                    Role = member.Role,
                    Status = "Active",
                    LastActive = GetRelativeTime(member.LastLoginDate ?? DateTime.UtcNow),
                    AssessmentsRun = assessments.Count(),
                    ReportsGenerated = assessments.Count(a => a.Status == "Completed"),
                    JoinedDate = GetMonthYear(member.CreatedDate)
                });
            }

            // Get pending invitations for this organization
            var pendingInvitations = await _context.TeamInvitations
                .Where(ti => ti.OrganizationId == currentOrganizationId && ti.Status == "Pending")
                .Select(ti => new TeamMemberDto
                {
                    Id = ti.InvitationId,
                    Name = "Pending User",
                    Email = ti.InvitedEmail,
                    Role = ti.InvitedRole,
                    Status = "Pending",
                    LastActive = "Never",
                    AssessmentsRun = 0,
                    ReportsGenerated = 0,
                    JoinedDate = ti.InvitedDate.ToString("MMM yyyy")
                })
                .ToListAsync();

            teamMembers.AddRange(pendingInvitations);

            return Ok(teamMembers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team members for customer {CustomerId}", GetCurrentCustomerId());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("invite")]
    public async Task<ActionResult<TeamMemberDto>> InviteTeamMember([FromBody] InviteTeamMemberRequest request)
    {
        try
        {
            _logger.LogInformation($"Inviting team member: {request.Email} with role: {request.Role}");

            var currentCustomerId = GetCurrentCustomerId();
            var currentOrganizationId = GetCurrentOrganizationId();

            if (currentCustomerId == null || currentOrganizationId == null)
                return Unauthorized("Invalid authentication or missing organization");

            // Check permissions
            if (!CanManageTeam())
                return Forbid("You don't have permission to invite team members");

            // Validate request
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Role))
            {
                return BadRequest("Email and role are required");
            }

            // Get current customer info for the invitation
            var currentCustomer = await _customerRepository.GetByIdAsync(currentCustomerId.Value);
            if (currentCustomer == null)
                return NotFound("Customer not found");

            // Check if email is already in use
            var existingCustomer = await _customerRepository.GetByEmailAsync(request.Email);
            if (existingCustomer != null)
            {
                return BadRequest("A user with this email already exists");
            }

            // Check if user already has pending invitation
            var existingInvitation = await _context.TeamInvitations
                .FirstOrDefaultAsync(ti => ti.InvitedEmail == request.Email &&
                                          ti.OrganizationId == currentOrganizationId &&
                                          ti.Status == "Pending");

            if (existingInvitation != null)
            {
                return BadRequest("User already has a pending invitation");
            }

            // Generate invitation token
            var invitationToken = GenerateInvitationToken();

            // Create invitation record in database
            var invitation = new TeamInvitation
            {
                OrganizationId = currentOrganizationId.Value,
                InvitedEmail = request.Email,
                InvitedRole = request.Role,
                InvitationToken = invitationToken,
                InvitedByCustomerId = currentCustomerId.Value,
                InvitationMessage = request.Message,
                ExpirationDate = DateTime.UtcNow.AddDays(7) // 7 days to accept
            };

            _context.TeamInvitations.Add(invitation);
            await _context.SaveChangesAsync();

            // Send invitation email
            try
            {
                await _emailService.SendTeamInvitationEmailAsync(
                    request.Email,
                    $"{currentCustomer.FirstName} {currentCustomer.LastName}",
                    currentCustomer.CompanyName,
                    invitationToken
                );

                _logger.LogInformation($"Team invitation sent successfully to {request.Email}");

                // Return the new team member response
                var newMember = new TeamMemberDto
                {
                    Id = invitation.InvitationId,
                    Name = "Pending User",
                    Email = request.Email,
                    Role = request.Role,
                    Status = "Pending",
                    LastActive = "Never",
                    AssessmentsRun = 0,
                    ReportsGenerated = 0,
                    JoinedDate = "Pending"
                };

                return Ok(newMember);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send invitation email to {Email}", request.Email);

                // Remove the invitation record if email failed
                _context.TeamInvitations.Remove(invitation);
                await _context.SaveChangesAsync();

                return StatusCode(500, "Failed to send invitation email");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inviting team member {Email}", request.Email);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("validate-invite/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> ValidateInvitation(string token)
    {
        try
        {
            _logger.LogInformation($"Validating invitation token: {token}");

            if (string.IsNullOrEmpty(token))
                return BadRequest("Invalid token");

            var invitation = await _context.TeamInvitations
                .Include(ti => ti.InvitedBy)
                .Include(ti => ti.Organization)
                .FirstOrDefaultAsync(ti => ti.InvitationToken == token && ti.Status == "Pending");

            if (invitation == null)
                return NotFound("Invitation not found or already used");

            if (invitation.ExpirationDate < DateTime.UtcNow)
                return BadRequest("Invitation has expired");

            var inviteInfo = new
            {
                email = invitation.InvitedEmail,
                role = invitation.InvitedRole,
                organizationName = invitation.Organization.Name,
                inviterName = $"{invitation.InvitedBy.FirstName} {invitation.InvitedBy.LastName}".Trim(),
                expirationDate = invitation.ExpirationDate,
                message = invitation.InvitationMessage
            };

            return Ok(inviteInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invitation token: {Token}", token);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<TeamStatsDto>> GetTeamStats()
    {
        try
        {
            var currentOrganizationId = GetCurrentOrganizationId();

            if (currentOrganizationId == null)
                return Unauthorized("Missing organization information");

            var organizationMembers = await _context.Customers
                .Where(c => c.OrganizationId == currentOrganizationId && c.IsActive)
                .ToListAsync();

            var pendingInvitations = await _context.TeamInvitations
                .CountAsync(ti => ti.OrganizationId == currentOrganizationId && ti.Status == "Pending");

            var roleDistribution = organizationMembers
                .GroupBy(c => c.Role)
                .ToDictionary(g => g.Key, g => g.Count());

            var stats = new TeamStatsDto
            {
                TotalMembers = organizationMembers.Count,
                ActiveMembers = organizationMembers.Count,
                PendingInvitations = pendingInvitations,
                AdminCount = roleDistribution.GetValueOrDefault("Admin", 0),
                RoleDistribution = roleDistribution
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team stats for customer {CustomerId}", GetCurrentCustomerId());
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("members/{memberId}")]
    public async Task<ActionResult<TeamMemberDto>> UpdateTeamMember(Guid memberId, [FromBody] UpdateTeamMemberRequest request)
    {
        try
        {
            var currentCustomerId = GetCurrentCustomerId();
            var currentOrganizationId = GetCurrentOrganizationId();

            if (currentCustomerId == null || currentOrganizationId == null)
                return Unauthorized("Invalid authentication or missing organization");

            if (!CanManageTeam())
                return Forbid("You don't have permission to update team members");

            var memberToUpdate = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == memberId &&
                                         c.OrganizationId == currentOrganizationId);

            if (memberToUpdate == null)
                return NotFound("Team member not found");

            // Don't allow changing the owner role
            if (memberToUpdate.Role == "Owner")
                return BadRequest("Cannot change the role of the organization owner");

            // Don't allow setting multiple owners
            if (request.Role == "Owner")
                return BadRequest("Cannot set multiple owners for an organization");

            memberToUpdate.Role = request.Role;
            await _context.SaveChangesAsync();

            var assessments = await _assessmentRepository.GetByCustomerIdAsync(memberId);
            var updatedMember = new TeamMemberDto
            {
                Id = memberToUpdate.CustomerId,
                Name = $"{memberToUpdate.FirstName} {memberToUpdate.LastName}".Trim(),
                Email = memberToUpdate.Email,
                Role = memberToUpdate.Role,
                Status = "Active",
                LastActive = GetRelativeTime(memberToUpdate.LastLoginDate ?? DateTime.UtcNow),
                AssessmentsRun = assessments.Count(),
                ReportsGenerated = assessments.Count(a => a.Status == "Completed"),
                JoinedDate = GetMonthYear(memberToUpdate.CreatedDate)
            };

            return Ok(updatedMember);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating team member {MemberId}", memberId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("members/{memberId}")]
    public async Task<IActionResult> RemoveTeamMember(Guid memberId)
    {
        try
        {
            var currentCustomerId = GetCurrentCustomerId();
            var currentOrganizationId = GetCurrentOrganizationId();

            if (currentCustomerId == null || currentOrganizationId == null)
                return Unauthorized("Invalid authentication or missing organization");

            if (!CanManageTeam())
                return Forbid("You don't have permission to remove team members");

            // Don't allow removing self
            if (memberId == currentCustomerId)
                return BadRequest("Cannot remove yourself from the team");

            // Check if it's a pending invitation
            var invitation = await _context.TeamInvitations
                .FirstOrDefaultAsync(ti => ti.InvitationId == memberId &&
                                          ti.OrganizationId == currentOrganizationId);

            if (invitation != null)
            {
                _context.TeamInvitations.Remove(invitation);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Team invitation removed: {memberId} by {currentCustomerId}");
                return Ok(new { message = "Invitation removed successfully" });
            }

            // Check if it's an existing team member
            var teamMember = await _context.Customers
                .FirstOrDefaultAsync(c => c.CustomerId == memberId &&
                                         c.OrganizationId == currentOrganizationId);

            if (teamMember != null)
            {
                // Don't allow removing the owner
                if (teamMember.Role == "Owner")
                    return BadRequest("Cannot remove the organization owner");

                // Remove from organization (soft delete by setting OrganizationId to null)
                teamMember.OrganizationId = null;
                teamMember.Role = "Owner"; // Reset to default role
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Team member removed: {memberId} by {currentCustomerId}");
                return Ok(new { message = "Team member removed successfully" });
            }

            return NotFound("Team member not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing team member {MemberId}", memberId);
            return StatusCode(500, "Internal server error");
        }
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

    // NEW: Helper method to get OrganizationId from JWT claims
    private Guid? GetCurrentOrganizationId()
    {
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        if (Guid.TryParse(orgIdClaim, out var organizationId))
        {
            return organizationId;
        }
        return null;
    }

    // NEW: Helper method to get User Role from JWT claims
    private string? GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }

    // NEW: Helper method to check if user can manage team
    private bool CanManageTeam()
    {
        var role = GetCurrentUserRole();
        return role == "Owner" || role == "Admin";
    }

    private static string GetRelativeTime(DateTime dateTime)
    {
        var timeSpan = DateTime.UtcNow - dateTime;

        if (timeSpan.TotalMinutes < 1)
            return "Just now";
        if (timeSpan.TotalMinutes < 60)
            return $"{(int)timeSpan.TotalMinutes} minutes ago";
        if (timeSpan.TotalHours < 24)
            return $"{(int)timeSpan.TotalHours} hours ago";
        if (timeSpan.TotalDays < 7)
            return $"{(int)timeSpan.TotalDays} days ago";

        return dateTime.ToString("MMM dd, yyyy");
    }

    private static string GetMonthYear(DateTime? dateTime)
    {
        return dateTime?.ToString("MMM yyyy") ?? "Unknown";
    }

    private string GenerateInvitationToken()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[32];
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }
}

// DTOs
public class TeamMemberDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastActive { get; set; } = string.Empty;
    public int AssessmentsRun { get; set; }
    public int ReportsGenerated { get; set; }
    public string JoinedDate { get; set; } = string.Empty;
}

public class InviteTeamMemberRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}

public class UpdateTeamMemberRequest
{
    [Required]
    public string Role { get; set; } = string.Empty;
}

public class TeamStatsDto
{
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int PendingInvitations { get; set; }
    public int AdminCount { get; set; }
    public Dictionary<string, int> RoleDistribution { get; set; } = new();
}