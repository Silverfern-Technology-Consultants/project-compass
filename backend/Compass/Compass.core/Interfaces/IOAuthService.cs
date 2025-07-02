using Compass.Core.Models;

namespace Compass.Core.Interfaces
{
    public interface IOAuthService
    {
        /// <summary>
        /// Initiates OAuth flow for a client environment
        /// </summary>
        Task<OAuthInitiateResponse> InitiateOAuthFlowAsync(OAuthInitiateRequest request, Guid organizationId);

        /// <summary>
        /// Gets OAuth progress for Key Vault creation
        /// </summary>
        Task<OAuthProgressResponse?> GetOAuthProgressAsync(string progressId);

        /// <summary>
        /// Handles OAuth callback and exchanges code for tokens
        /// </summary>
        Task<bool> HandleOAuthCallbackAsync(OAuthCallbackRequest request);

        /// <summary>
        /// Retrieves OAuth error information for failed authentication attempts
        /// </summary>
        Task<OAuthErrorInfo?> GetOAuthErrorAsync(string state);

        /// <summary>
        /// Retrieves stored credentials for a client
        /// </summary>
        Task<StoredCredentials?> GetStoredCredentialsAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Refreshes expired OAuth tokens
        /// </summary>
        Task<bool> RefreshTokensAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Tests OAuth credentials by making a simple API call
        /// </summary>
        Task<bool> TestCredentialsAsync(Guid clientId, Guid organizationId);

        /// <summary>
        /// Revokes stored OAuth credentials
        /// </summary>
        Task<bool> RevokeCredentialsAsync(Guid clientId, Guid organizationId);
    }
}