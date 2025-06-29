using System.ComponentModel.DataAnnotations;

namespace Compass.Core.Models
{
    public class OAuthInitiateRequest
    {
        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public string ClientName { get; set; } = string.Empty;

        public string? Description { get; set; }
    }

    public class OAuthProgressResponse
    {
        public string ProgressId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Creating", "Completed", "Failed"
        public string Message { get; set; } = string.Empty;
        public int ProgressPercentage { get; set; }
        public string? AuthorizationUrl { get; set; } // Available when completed
        public string? State { get; set; } // Available when completed
        public DateTime? ExpiresAt { get; set; } // Available when completed
    }

    public class OAuthInitiateResponse
    {
        public string? AuthorizationUrl { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool RequiresKeyVaultCreation { get; set; } // NEW
        public string? ProgressId { get; set; } // NEW - for tracking creation progress
    }

    public class OAuthCallbackRequest
    {
        public string Code { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Error { get; set; }
        public string? ErrorDescription { get; set; }
    }

    public class OAuthTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string TokenType { get; set; } = "Bearer";
        public string Scope { get; set; } = string.Empty;
    }

    public class AzureEnvironmentRequest
    {
        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public string EnvironmentName { get; set; } = string.Empty;

        [Required]
        public List<string> SubscriptionIds { get; set; } = new();

        public string? Description { get; set; }
    }

    public class OAuthStateData
    {
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public Guid OrganizationId { get; set; }
        public string RedirectUri { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }
    }

    public class StoredCredentials
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; } = string.Empty;
        public DateTime StoredAt { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
    }
}