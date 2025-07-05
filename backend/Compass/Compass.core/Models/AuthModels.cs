using System.ComponentModel.DataAnnotations;

namespace Compass.Core.Models;

public class RegisterRequest
{
    [Required]
    [StringLength(100)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    // NEW: Add invitation token support - THIS WAS MISSING!
    public string? InvitationToken { get; set; }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public CustomerInfo? Customer { get; set; }
}

public class CustomerInfo
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }

    // NEW: Organization-related properties
    public string Role { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }

    // UPDATED: Now organization-scoped instead of user-scoped
    public string SubscriptionStatus { get; set; } = string.Empty;
    public DateTime? TrialEndDate { get; set; }
}

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}

public class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

// NEW MODELS FOR MFA AND PASSWORD RESET
public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class CustomerDto
{
    public Guid CustomerId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; } // ✅ ADDED: Missing EmailVerified property
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}

public class LoginWithMfaRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    public string? MfaToken { get; set; }
    public bool IsBackupCode { get; set; } = false;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public bool RequiresMfa { get; set; }
    public bool RequiresMfaSetup { get; set; }
    public bool RequiresEmailVerification { get; set; } // ✅ ADDED: Missing RequiresEmailVerification property
    public string Message { get; set; } = string.Empty;
    public CustomerDto? Customer { get; set; }
    public string? SessionId { get; set; }
}