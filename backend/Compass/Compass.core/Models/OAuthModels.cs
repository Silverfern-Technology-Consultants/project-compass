using System.ComponentModel.DataAnnotations;

namespace Compass.Core.Models
{
    // Enhanced OAuth request with scope selection
    public class OAuthInitiateRequest
    {
        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public string ClientName { get; set; } = string.Empty;

        public string? Description { get; set; }

        // NEW: Specify which OAuth scopes are needed
        public OAuthScopeTypes ScopeTypes { get; set; } = OAuthScopeTypes.ResourceManager;
    }

    // NEW: Define available OAuth scope types
    [Flags]
    public enum OAuthScopeTypes
    {
        ResourceManager = 1,      // https://management.azure.com/user_impersonation
        MicrosoftGraph = 2,       // Microsoft Graph permissions
        Both = ResourceManager | MicrosoftGraph
    }

    // Enhanced stored credentials to support multiple token types
    public class StoredCredentials
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; } = string.Empty;
        public DateTime StoredAt { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;

        // NEW: Multiple token support
        public string? GraphAccessToken { get; set; }
        public string? GraphRefreshToken { get; set; }
        public DateTime? GraphExpiresAt { get; set; }
        public string? GraphScope { get; set; }
        public OAuthScopeTypes AvailableScopes { get; set; } = OAuthScopeTypes.ResourceManager;
    }

    // NEW: Microsoft Graph specific models
    public class GraphTokenCredentials
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string Scope { get; set; } = string.Empty;
        public List<string> GrantedPermissions { get; set; } = new();
    }

    // Enhanced OAuth response with scope information
    public class OAuthInitiateResponse
    {
        public string? AuthorizationUrl { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool RequiresKeyVaultCreation { get; set; }
        public string? ProgressId { get; set; }

        // NEW: Scope information
        public OAuthScopeTypes RequestedScopes { get; set; }
        public List<string> RequestedPermissions { get; set; } = new();
    }

    // Existing models remain unchanged for backward compatibility
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

    public class OAuthStateData
    {
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public Guid OrganizationId { get; set; }
        public string RedirectUri { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? Description { get; set; }

        // NEW: Track requested scopes
        public OAuthScopeTypes RequestedScopes { get; set; } = OAuthScopeTypes.ResourceManager;
    }

    public class OAuthErrorInfo
    {
        public string Error { get; set; } = string.Empty;
        public string? ErrorDescription { get; set; }
        public bool IsUserError { get; set; }
        public string UserMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public class OAuthProgressResponse
    {
        public string ProgressId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ProgressPercentage { get; set; }
        public string? AuthorizationUrl { get; set; }
        public string? State { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    // NEW: Microsoft Graph API Configuration - CORRECTED to only include delegated permissions
    public static class MicrosoftGraphScopes
    {
        // Directory and User Analysis
        public const string DirectoryReadAll = "https://graph.microsoft.com/Directory.Read.All";
        public const string UserReadAll = "https://graph.microsoft.com/User.Read.All";
        public const string GroupReadAll = "https://graph.microsoft.com/Group.Read.All";

        // Application Analysis (Service Principal requires application permissions - not available for delegated)
        public const string ApplicationReadAll = "https://graph.microsoft.com/Application.Read.All";

        // Conditional Access and Security Policies
        public const string PolicyReadAll = "https://graph.microsoft.com/Policy.Read.All";
        public const string ConditionalAccessReadAll = "https://graph.microsoft.com/Policy.Read.ConditionalAccess";

        // RBAC and Role Management
        public const string RoleManagementReadDirectory = "https://graph.microsoft.com/RoleManagement.Read.Directory";

        // Device Management (only basic device read - managed devices requires application permissions)
        public const string DeviceReadAll = "https://graph.microsoft.com/Device.Read.All";

        // Security and Risk Assessment
        public const string SecurityEventsReadAll = "https://graph.microsoft.com/SecurityEvents.Read.All";
        public const string IdentityRiskyUserReadAll = "https://graph.microsoft.com/IdentityRiskyUser.Read.All";

        // Audit Logs and Activity
        public const string AuditLogReadAll = "https://graph.microsoft.com/AuditLog.Read.All";

        // Get all required scopes for identity assessment (CORRECTED - removed invalid delegated permissions)
        public static List<string> GetIdentityAssessmentScopes()
        {
            return new List<string>
            {
                DirectoryReadAll,
                UserReadAll,
                GroupReadAll,
                ApplicationReadAll,
                // REMOVED: ServicePrincipalReadAll - not available as delegated permission
                PolicyReadAll,
                ConditionalAccessReadAll,
                RoleManagementReadDirectory,
                DeviceReadAll,
                // REMOVED: DeviceManagementReadAll - not available as delegated permission
                SecurityEventsReadAll,
                IdentityRiskyUserReadAll,
                AuditLogReadAll
            };
        }

        // Get minimal scopes for basic identity analysis
        public static List<string> GetBasicIdentityScopes()
        {
            return new List<string>
            {
                DirectoryReadAll,
                UserReadAll,
                ApplicationReadAll,
                PolicyReadAll
            };
        }

        public static List<string> GetEnhancedSecurityScopes()
        {
            return new List<string>
            {
                DirectoryReadAll,
                UserReadAll,
                GroupReadAll,
                ApplicationReadAll,
                PolicyReadAll,
                ConditionalAccessReadAll,
                RoleManagementReadDirectory,
                AuditLogReadAll,
                SecurityEventsReadAll,
                IdentityRiskyUserReadAll
            };
        }
    }
}