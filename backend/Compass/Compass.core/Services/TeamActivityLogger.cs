// Compass.Core/Services/TeamActivityLogger.cs
using Compass.Data;
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services;

public interface ITeamActivityLogger
{
    Task LogMemberInvitedAsync(Guid organizationId, Guid invitedByCustomerId, string invitedEmail, string role, string? message = null);
    Task LogMemberJoinedAsync(Guid organizationId, Guid memberId, string role);
    Task LogRoleChangedAsync(Guid organizationId, Guid changedByCustomerId, Guid targetMemberId, string oldRole, string newRole);
    Task LogMemberRemovedAsync(Guid organizationId, Guid removedByCustomerId, Guid targetMemberId, string removalType, string? reason = null);
    Task LogInvitationCancelledAsync(Guid organizationId, Guid cancelledByCustomerId, string invitedEmail);
    Task<List<TeamActivityEntry>> GetActivityHistoryAsync(Guid organizationId, int page = 1, int pageSize = 50);
}

public class TeamActivityLogger : ITeamActivityLogger
{
    private readonly CompassDbContext _context;
    private readonly ILogger<TeamActivityLogger> _logger;

    public TeamActivityLogger(CompassDbContext context, ILogger<TeamActivityLogger> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogMemberInvitedAsync(Guid organizationId, Guid invitedByCustomerId, string invitedEmail, string role, string? message = null)
    {
        await CreateActivityEntry(
            organizationId,
            "member_invited",
            invitedByCustomerId,
            null,
            $"Invited {invitedEmail} as {role}",
            new
            {
                invitedEmail,
                role,
                message,
                timestamp = DateTime.UtcNow
            }
        );
    }

    public async Task LogMemberJoinedAsync(Guid organizationId, Guid memberId, string role)
    {
        var member = await _context.Customers.FindAsync(memberId);
        if (member == null) return;

        await CreateActivityEntry(
            organizationId,
            "member_joined",
            memberId,
            null,
            $"{member.FirstName} {member.LastName} joined as {role}",
            new
            {
                memberEmail = member.Email,
                role,
                joinedDate = DateTime.UtcNow
            }
        );
    }

    public async Task LogRoleChangedAsync(Guid organizationId, Guid changedByCustomerId, Guid targetMemberId, string oldRole, string newRole)
    {
        var changedBy = await _context.Customers.FindAsync(changedByCustomerId);
        var targetMember = await _context.Customers.FindAsync(targetMemberId);

        if (changedBy == null || targetMember == null) return;

        await CreateActivityEntry(
            organizationId,
            "role_changed",
            changedByCustomerId,
            targetMemberId,
            $"{changedBy.FirstName} {changedBy.LastName} changed {targetMember.FirstName} {targetMember.LastName}'s role from {oldRole} to {newRole}",
            new
            {
                targetEmail = targetMember.Email,
                oldRole,
                newRole,
                changedBy = new { changedBy.Email, changedBy.FirstName, changedBy.LastName },
                timestamp = DateTime.UtcNow
            }
        );

        _logger.LogInformation(
            "Role changed: Organization {OrganizationId}, Member {TargetMemberId} ({Email}) role changed from {OldRole} to {NewRole} by {ChangedByCustomerId}",
            organizationId, targetMemberId, targetMember.Email, oldRole, newRole, changedByCustomerId);
    }

    public async Task LogMemberRemovedAsync(Guid organizationId, Guid removedByCustomerId, Guid targetMemberId, string removalType, string? reason = null)
    {
        var removedBy = await _context.Customers.FindAsync(removedByCustomerId);
        var targetMember = await _context.Customers.FindAsync(targetMemberId);

        if (removedBy == null || targetMember == null) return;

        var description = removalType switch
        {
            "DeactivateAccount" => $"{removedBy.FirstName} {removedBy.LastName} deactivated {targetMember.FirstName} {targetMember.LastName}'s account",
            "FullDeletion" => $"{removedBy.FirstName} {removedBy.LastName} removed {targetMember.FirstName} {targetMember.LastName} from the system",
            _ => $"{removedBy.FirstName} {removedBy.LastName} removed {targetMember.FirstName} {targetMember.LastName} from the organization"
        };

        await CreateActivityEntry(
            organizationId,
            "member_removed",
            removedByCustomerId,
            targetMemberId,
            description,
            new
            {
                targetEmail = targetMember.Email,
                removalType,
                reason,
                removedBy = new { removedBy.Email, removedBy.FirstName, removedBy.LastName },
                timestamp = DateTime.UtcNow
            }
        );

        _logger.LogWarning(
            "Member removed: Organization {OrganizationId}, Member {TargetMemberId} ({Email}) removed by {RemovedByCustomerId} - Type: {RemovalType}",
            organizationId, targetMemberId, targetMember.Email, removedByCustomerId, removalType);
    }

    public async Task LogInvitationCancelledAsync(Guid organizationId, Guid cancelledByCustomerId, string invitedEmail)
    {
        var cancelledBy = await _context.Customers.FindAsync(cancelledByCustomerId);
        if (cancelledBy == null) return;

        await CreateActivityEntry(
            organizationId,
            "invitation_cancelled",
            cancelledByCustomerId,
            null,
            $"{cancelledBy.FirstName} {cancelledBy.LastName} cancelled invitation for {invitedEmail}",
            new
            {
                invitedEmail,
                cancelledBy = new { cancelledBy.Email, cancelledBy.FirstName, cancelledBy.LastName },
                timestamp = DateTime.UtcNow
            }
        );
    }

    public async Task<List<TeamActivityEntry>> GetActivityHistoryAsync(Guid organizationId, int page = 1, int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;

        // For now, we'll aggregate activity from various sources
        // In a production system, you'd have a dedicated ActivityLog table
        var activities = new List<TeamActivityEntry>();

        // Get team invitation activities
        var invitations = await _context.TeamInvitations
            .Where(ti => ti.OrganizationId == organizationId)
            .Include(ti => ti.InvitedBy)
            .Include(ti => ti.AcceptedBy)
            .OrderByDescending(ti => ti.InvitedDate)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        foreach (var invitation in invitations)
        {
            // Invitation sent
            activities.Add(new TeamActivityEntry
            {
                Id = invitation.InvitationId,
                Type = "member_invited",
                Actor = $"{invitation.InvitedBy.FirstName} {invitation.InvitedBy.LastName}",
                ActorEmail = invitation.InvitedBy.Email,
                Target = invitation.InvitedEmail,
                Description = $"Invited {invitation.InvitedEmail} as {invitation.InvitedRole}",
                Timestamp = invitation.InvitedDate,
                Metadata = new
                {
                    role = invitation.InvitedRole,
                    status = invitation.Status,
                    message = invitation.InvitationMessage,
                    expirationDate = invitation.ExpirationDate
                }
            });

            // If accepted, add joined activity
            if (invitation.Status == "Accepted" && invitation.AcceptedDate.HasValue && invitation.AcceptedBy != null)
            {
                activities.Add(new TeamActivityEntry
                {
                    Id = Guid.NewGuid(),
                    Type = "member_joined",
                    Actor = $"{invitation.AcceptedBy.FirstName} {invitation.AcceptedBy.LastName}",
                    ActorEmail = invitation.AcceptedBy.Email,
                    Target = invitation.InvitedEmail,
                    Description = $"{invitation.AcceptedBy.FirstName} {invitation.AcceptedBy.LastName} joined the team as {invitation.InvitedRole}",
                    Timestamp = invitation.AcceptedDate.Value,
                    Metadata = new
                    {
                        role = invitation.InvitedRole,
                        invitationId = invitation.InvitationId
                    }
                });
            }

            // If cancelled
            if (invitation.Status == "Cancelled")
            {
                activities.Add(new TeamActivityEntry
                {
                    Id = Guid.NewGuid(),
                    Type = "invitation_cancelled",
                    Actor = $"{invitation.InvitedBy.FirstName} {invitation.InvitedBy.LastName}",
                    ActorEmail = invitation.InvitedBy.Email,
                    Target = invitation.InvitedEmail,
                    Description = $"Cancelled invitation for {invitation.InvitedEmail}",
                    Timestamp = DateTime.UtcNow, // We don't track when it was cancelled
                    Metadata = new
                    {
                        originalRole = invitation.InvitedRole,
                        invitationId = invitation.InvitationId
                    }
                });
            }
        }

        return activities
            .OrderByDescending(a => a.Timestamp)
            .Take(pageSize)
            .ToList();
    }

    private async Task CreateActivityEntry(
        Guid organizationId,
        string activityType,
        Guid actorCustomerId,
        Guid? targetCustomerId,
        string description,
        object metadata)
    {
        // In a production system, you would save this to a dedicated ActivityLog table
        // For now, we'll just log it for audit purposes

        var metadataJson = JsonSerializer.Serialize(metadata);

        _logger.LogInformation(
            "Team Activity: {ActivityType} in Organization {OrganizationId} by Customer {ActorCustomerId} - {Description} | Metadata: {Metadata}",
            activityType, organizationId, actorCustomerId, description, metadataJson);

        // TODO: Save to ActivityLog table when implemented
        // var activityLog = new ActivityLog
        // {
        //     OrganizationId = organizationId,
        //     ActivityType = activityType,
        //     ActorCustomerId = actorCustomerId,
        //     TargetCustomerId = targetCustomerId,
        //     Description = description,
        //     Metadata = metadataJson,
        //     Timestamp = DateTime.UtcNow
        // };
        // _context.ActivityLogs.Add(activityLog);
        // await _context.SaveChangesAsync();
    }
}

// Activity entry model for API responses
public class TeamActivityEntry
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // member_invited, member_joined, role_changed, member_removed, invitation_cancelled
    public string Actor { get; set; } = string.Empty; // Who performed the action
    public string ActorEmail { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty; // Who was affected
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Metadata { get; set; } // Additional data
}

// Future ActivityLog entity (to be added to DbContext when ready)
public class ActivityLog
{
    public Guid ActivityLogId { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public Guid ActorCustomerId { get; set; }
    public Guid? TargetCustomerId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Metadata { get; set; } = string.Empty; // JSON
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }

    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual Customer Actor { get; set; } = null!;
    public virtual Customer? Target { get; set; }
}