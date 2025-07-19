using Compass.Core.Models;

namespace Compass.Core.Interfaces
{
    public interface IOAuthService
    {
        // Existing methods (unchanged for backward compatibility)
        Task<OAuthInitiateResponse> InitiateOAuthFlowAsync(OAuthInitiateRequest request, Guid organizationId);
        Task<OAuthProgressResponse?> GetOAuthProgressAsync(string progressId);
        Task<bool> HandleOAuthCallbackAsync(OAuthCallbackRequest request);
        Task<OAuthErrorInfo?> GetOAuthErrorAsync(string state);
        Task<StoredCredentials?> GetStoredCredentialsAsync(Guid clientId, Guid organizationId);
        Task<bool> RefreshTokensAsync(Guid clientId, Guid organizationId);
        Task<bool> TestCredentialsAsync(Guid clientId, Guid organizationId);
        Task<bool> RevokeCredentialsAsync(Guid clientId, Guid organizationId);

        // NEW: Microsoft Graph specific methods
        /// <summary>
        /// Initiates OAuth flow with specific scope types (Resource Manager, Graph, or Both)
        /// </summary>
        Task<OAuthInitiateResponse> InitiateOAuthFlowWithScopesAsync(
            OAuthInitiateRequest request,
            Guid organizationId,
            OAuthScopeTypes scopeTypes);

        /// <summary>
        /// Gets Microsoft Graph credentials for a client
        /// </summary>
        Task<GraphTokenCredentials?> GetGraphCredentialsAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Tests Microsoft Graph credentials by making a simple API call
        /// </summary>
        Task<bool> TestGraphCredentialsAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Refreshes Microsoft Graph tokens specifically
        /// </summary>
        Task<bool> RefreshGraphTokensAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Checks what OAuth scopes are available for a client
        /// </summary>
        Task<OAuthScopeTypes> GetAvailableScopesAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Upgrades existing OAuth setup to include Microsoft Graph scopes
        /// </summary>
        Task<OAuthInitiateResponse> UpgradeToGraphScopesAsync(
            Guid clientId,
            Guid organizationId,
            string clientName);

        /// <summary>
        /// Gets detailed information about granted permissions
        /// </summary>
        Task<List<string>> GetGrantedPermissionsAsync(Guid clientId, Guid organizationId);
    }
}