using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Core.Services;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Compass.Data.Interfaces;
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
                organizationName = invitation.Organization?.Name?? "Unknown Organization",
                inviterName = $"{invitation.InvitedBy?.FirstName ?? ""} {invitation.InvitedBy?.LastName ?? ""}".Trim(),
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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var memberToUpdate = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == memberId &&
                                             c.OrganizationId == currentOrganizationId &&
                                             c.IsActive);
                if (memberToUpdate == null)
                    return NotFound("Team member not found");
                // Don't allow changing the owner role
                if (memberToUpdate.Role == "Owner")
                    return BadRequest("Cannot change the role of the organization owner");
                // Don't allow setting multiple owners
                if (request.Role == "Owner")
                    return BadRequest("Cannot set multiple owners for an organization");
                var oldRole = memberToUpdate.Role;
                memberToUpdate.Role = request.Role;
                _context.Customers.Update(memberToUpdate);
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Team member role updated: {MemberId} ({Email}) changed from {OldRole} to {NewRole} by {CurrentUserId}",
                    memberId, memberToUpdate.Email, oldRole, request.Role, currentCustomerId);
                await transaction.CommitAsync();
                // Return updated member data
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
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Check if it's a pending invitation first
                var invitation = await _context.TeamInvitations
                    .FirstOrDefaultAsync(ti => ti.InvitationId == memberId &&
                                              ti.OrganizationId == currentOrganizationId &&
                                              ti.Status == "Pending");
                if (invitation != null)
                {
                    // Remove pending invitation
                    invitation.Status = "Cancelled";
                    _context.TeamInvitations.Update(invitation);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    _logger.LogInformation("Team invitation cancelled: {InvitationId} ({Email}) by {CurrentUserId}",
                        memberId, invitation.InvitedEmail, currentCustomerId);
                    return Ok(new { message = "Invitation cancelled successfully" });
                }
                // Check if it's an existing team member
                var teamMember = await _context.Customers
                    .FirstOrDefaultAsync(c => c.CustomerId == memberId &&
                                             c.OrganizationId == currentOrganizationId &&
                                             c.IsActive);
                if (teamMember != null)
                {
                    // Don't allow removing the owner
                    if (teamMember.Role == "Owner")
                        return BadRequest("Cannot remove the organization owner");
                    // ENHANCED: Determine removal strategy
                    var removalType = await DetermineRemovalType(teamMember);
                    _logger.LogInformation(
                        "Determined removal type for {MemberId} ({Email}): {RemovalType}",
                        memberId, teamMember.Email, removalType);
                    switch (removalType)
                    {
                        case TeamMemberRemovalType.RemoveFromOrganization:
                            // User has other organizations - just remove from this org
                            teamMember.OrganizationId = null;
                            teamMember.Role = "Owner"; // Reset to default
                            _context.Customers.Update(teamMember);
                            break;
                        case TeamMemberRemovalType.DeactivateAccount:
                            // User only belongs to this org - deactivate account
                            teamMember.IsActive = false;
                            teamMember.OrganizationId = null;
                            teamMember.Role = "Owner";
                            _context.Customers.Update(teamMember);
                            break;
                        case TeamMemberRemovalType.FullDeletion:
                            // Complete removal - handle related data first
                            await HandleCompleteUserRemoval(teamMember);
                            break;
                    }
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    _logger.LogInformation(
                        "Team member removed: {MemberId} ({Email}) by {CurrentUserId} - Type: {RemovalType}",
                        memberId, teamMember.Email, currentCustomerId, removalType);
                    var message = removalType switch
                    {
                        TeamMemberRemovalType.DeactivateAccount => "Team member account deactivated successfully",
                        TeamMemberRemovalType.FullDeletion => "Team member completely removed from system",
                        _ => "Team member removed from organization successfully"
                    };
                    return Ok(new { message, removalType = removalType.ToString() });
                }
                return NotFound("Team member not found");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing team member {MemberId}", memberId);
            return StatusCode(500, "Internal server error");
        }
    }
    [HttpGet("activity")]
    public async Task<ActionResult<List<TeamActivityDto>>> GetTeamActivity(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var currentOrganizationId = GetCurrentOrganizationId();
            if (currentOrganizationId == null)
                return Unauthorized("Missing organization information");
            var skip = (page - 1) * pageSize;
            // Get recent team invitations as activity
            var activities = new List<TeamActivityDto>();
            var invitations = await _context.TeamInvitations
                .Where(ti => ti.OrganizationId == currentOrganizationId)
                .Include(ti => ti.InvitedBy)
                .Include(ti => ti.AcceptedBy)
                .OrderByDescending(ti => ti.InvitedDate)
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();
            foreach (var invitation in invitations)
            {
                // Invitation sent activity
                activities.Add(new TeamActivityDto
                {
                    Id = invitation.InvitationId,
                    Type = "member_invited",
                    Actor = $"{invitation.InvitedBy?.FirstName ?? ""} {invitation.InvitedBy?.LastName ?? ""}".Trim(),
                    Target = invitation.InvitedEmail,
                    Description = $"Invited {invitation.InvitedEmail} as {invitation.InvitedRole}",
                    Timestamp = invitation.InvitedDate,
                    Metadata = new { role = invitation.InvitedRole, status = invitation.Status }
                });
                // If accepted, add joined activity
                if (invitation.Status == "Accepted" && invitation.AcceptedDate.HasValue && invitation.AcceptedBy != null)
                {
                    activities.Add(new TeamActivityDto
                    {
                        Id = Guid.NewGuid(),
                        Type = "member_joined",
                        Actor = $"{invitation.AcceptedBy.FirstName} {invitation.AcceptedBy.LastName}",
                        Target = invitation.InvitedEmail,
                        Description = $"{invitation.AcceptedBy.FirstName} {invitation.AcceptedBy.LastName} joined as {invitation.InvitedRole}",
                        Timestamp = invitation.AcceptedDate.Value,
                        Metadata = new { role = invitation.InvitedRole }
                    });
                }
            }
            return Ok(activities.OrderByDescending(a => a.Timestamp).Take(pageSize).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team activity");
            return StatusCode(500, "Internal server error");
        }
    }
    // HELPER METHODS
    private async Task<TeamMemberRemovalType> DetermineRemovalType(Customer member)
    {
        // Check if user has any other organization memberships or invitations
        var hasOtherOrganizations = await _context.Customers
            .AnyAsync(c => c.Email == member.Email &&
                          c.CustomerId != member.CustomerId &&
                          c.OrganizationId != null &&
                          c.IsActive);
        var hasOtherInvitations = await _context.TeamInvitations
            .AnyAsync(ti => ti.InvitedEmail == member.Email &&
                           ti.OrganizationId != member.OrganizationId &&
                           ti.Status == "Pending");
        _logger.LogInformation(
            "Removal analysis for {Email}: hasOtherOrganizations={HasOtherOrgs}, hasOtherInvitations={HasOtherInvites}",
            member.Email, hasOtherOrganizations, hasOtherInvitations);
        if (hasOtherOrganizations || hasOtherInvitations)
        {
            return TeamMemberRemovalType.RemoveFromOrganization;
        }
        // Check if user has created any content that should be preserved
        var hasAssessments = await _context.Assessments
            .AnyAsync(a => a.CustomerId == member.CustomerId);
        _logger.LogInformation(
            "Content analysis for {Email}: hasAssessments={HasAssessments}",
            member.Email, hasAssessments);
        if (hasAssessments)
        {
            // Keep account but deactivate if they have created content
            return TeamMemberRemovalType.DeactivateAccount;
        }
        // Safe to completely remove if no other ties
        return TeamMemberRemovalType.FullDeletion;
    }
    private async Task HandleCompleteUserRemoval(Customer member)
    {
        _logger.LogInformation("Starting complete user removal for {Email}", member.Email);
        // STEP 1: Clean up TeamInvitations foreign key references FIRST
        await CleanupTeamInvitationReferences(member.CustomerId, member.Email);
        // STEP 2: Handle user's assessments if they exist
        var assessments = await _context.Assessments
            .Where(a => a.CustomerId == member.CustomerId)
            .ToListAsync();
        if (assessments.Any())
        {
            _logger.LogInformation("Found {AssessmentCount} assessments for user {Email}, transferring ownership",
                assessments.Count, member.Email);
            // Transfer ownership to organization owner
            var orgOwner = await _context.Customers
                .FirstOrDefaultAsync(c => c.OrganizationId == member.OrganizationId && c.Role == "Owner");
            if (orgOwner != null)
            {
                foreach (var assessment in assessments)
                {
                    assessment.CustomerId = orgOwner.CustomerId;
                }
                _context.Assessments.UpdateRange(assessments);
                _logger.LogInformation("Transferred {AssessmentCount} assessments to owner {OwnerEmail}",
                    assessments.Count, orgOwner.Email);
            }
            else
            {
                _logger.LogWarning("No organization owner found for assessment transfer, assessments will be deleted");
            }
        }
        // STEP 3: Remove user account completely
        _context.Customers.Remove(member);
        _logger.LogInformation("Completed full removal process for {Email}", member.Email);
    }
    private async Task CleanupTeamInvitationReferences(Guid customerId, string email)
    {
        _logger.LogInformation("Cleaning up TeamInvitation references for user {CustomerId} ({Email})", customerId, email);
        // Find all invitations where this user is the inviter
        var invitedByUser = await _context.TeamInvitations
            .Where(ti => ti.InvitedByCustomerId == customerId)
            .ToListAsync();
        // Find all invitations where this user accepted
        var acceptedByUser = await _context.TeamInvitations
            .Where(ti => ti.AcceptedByCustomerId == customerId)
            .ToListAsync();
        _logger.LogInformation("Found {InvitedCount} invitations sent by user and {AcceptedCount} accepted by user",
            invitedByUser.Count, acceptedByUser.Count);
        // For invitations sent by this user, we'll mark them as "System" rather than deleting
        foreach (var invitation in invitedByUser)
        {
            invitation.InvitedByCustomerId = null; // Allow null to break FK constraint
            _logger.LogInformation("Cleared InvitedByCustomerId for invitation {InvitationId}", invitation.InvitationId);
        }
        // For invitations accepted by this user, we need to handle differently
        foreach (var invitation in acceptedByUser)
        {
            // If invitation is already accepted, we can't really "unaccept" it without data loss
            // Best approach is to clear the foreign key and add a note
            invitation.AcceptedByCustomerId = null; // Allow null to break FK constraint
            _logger.LogInformation("Cleared AcceptedByCustomerId for invitation {InvitationId}", invitation.InvitationId);
        }
        // Also clean up any pending invitations FOR this user's email
        var pendingInvitationsForUser = await _context.TeamInvitations
            .Where(ti => ti.InvitedEmail == email && ti.Status == "Pending")
            .ToListAsync();
        foreach (var invitation in pendingInvitationsForUser)
        {
            invitation.Status = "Cancelled";
            _logger.LogInformation("Cancelled pending invitation {InvitationId} for email {Email}",
                invitation.InvitationId, email);
        }
        // Update all changes
        if (invitedByUser.Any() || acceptedByUser.Any() || pendingInvitationsForUser.Any())
        {
            var allUpdates = invitedByUser.Concat(acceptedByUser).Concat(pendingInvitationsForUser).ToList();
            _context.TeamInvitations.UpdateRange(allUpdates);
            _logger.LogInformation("Updated {Count} TeamInvitation records to clear foreign key references",
                allUpdates.Count);
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
    private Guid? GetCurrentOrganizationId()
    {
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        if (Guid.TryParse(orgIdClaim, out var organizationId))
        {
            return organizationId;
        }
        return null;
    }
    private string? GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value;
    }
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
// ENUMS AND DTOS
public enum TeamMemberRemovalType
{
    RemoveFromOrganization,  // Just remove from this org, keep account
    DeactivateAccount,       // Deactivate account but preserve data
    FullDeletion            // Complete removal from system
}
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
// Activity logging DTO (basic version)
public class TeamActivityDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Metadata { get; set; }
}