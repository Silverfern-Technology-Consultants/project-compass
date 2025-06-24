using System.ComponentModel.DataAnnotations;

namespace Compass.Data.Entities;

public class TeamInvitation
{
    [Key]
    public Guid InvitationId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid OrganizationId { get; set; } // Links to actual Organization

    [Required]
    [MaxLength(255)]
    public string InvitedEmail { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string InvitedRole { get; set; } = string.Empty; // Admin, Member, Viewer

    [Required]
    [MaxLength(500)]
    public string InvitationToken { get; set; } = string.Empty;

    [Required]
    public Guid InvitedByCustomerId { get; set; }

    public DateTime InvitedDate { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Accepted, Expired

    public DateTime ExpirationDate { get; set; }

    public DateTime? AcceptedDate { get; set; }

    public Guid? AcceptedByCustomerId { get; set; }

    // Optional message from inviter
    [MaxLength(500)]
    public string? InvitationMessage { get; set; }

    // Navigation properties
    public virtual Organization Organization { get; set; } = null!;
    public virtual Customer InvitedBy { get; set; } = null!;
    public virtual Customer? AcceptedBy { get; set; }

    // Helper properties
    public bool IsExpired => DateTime.UtcNow > ExpirationDate;
    public bool IsValid => Status == "Pending" && !IsExpired;
}