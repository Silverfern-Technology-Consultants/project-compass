using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Web;
namespace Compass.Core.Services
{
    public class OAuthService : IOAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<OAuthService> _logger;
        private readonly IMemoryCache _cache;
        private readonly SecretClient _secretClient;
        private readonly HttpClient _httpClient;
        private readonly TokenCredential _credential;
        // OAuth state cache duration (10 minutes)
        private readonly TimeSpan _stateCacheDuration = TimeSpan.FromMinutes(10);
        public OAuthService(
            IConfiguration configuration,
            ILogger<OAuthService> logger,
            IMemoryCache cache,
            SecretClient secretClient,
            HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _cache = cache;
            _secretClient = secretClient;
            _httpClient = httpClient;
            _credential = new DefaultAzureCredential();
        }
        public async Task<OAuthInitiateResponse> InitiateOAuthFlowAsync(OAuthInitiateRequest request, Guid organizationId)
        {
            try
            {
                // Read OAuth secrets directly from Key Vault
                var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
                var tenantIdSecret = await _secretClient.GetSecretAsync("oauth-tenant-id");
                var clientId = clientIdSecret.Value.Value;
                var tenantId = tenantIdSecret.Value.Value;
                var redirectUri = _configuration["OAuth:RedirectUri"]; // This one works from config
                var scopesSection = _configuration.GetSection("OAuth:Scopes");
                var scopes = scopesSection.GetChildren().Select(x => x.Value).Where(x => x != null).ToArray();
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
                {
                    throw new InvalidOperationException("OAuth configuration is missing");
                }
                // Generate unique state for this OAuth flow
                var state = Guid.NewGuid().ToString();
                var stateData = new OAuthStateData
                {
                    ClientId = request.ClientId,
                    ClientName = request.ClientName,
                    OrganizationId = organizationId,
                    RedirectUri = redirectUri!,
                    CreatedAt = DateTime.UtcNow,
                    Description = request.Description
                };
                // Cache the state data
                var stateKey = $"oauth_state_{state}";
                _cache.Set(stateKey, stateData, _stateCacheDuration);
                // Build authorization URL
                var scopeString = string.Join(" ", scopes.Length > 0 ? scopes : new[] { "https://management.azure.com/user_impersonation" });
                var authUrl = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/authorize" +
                             $"?client_id={clientId}" +
                             $"&response_type=code" +
                             $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                             $"&scope={HttpUtility.UrlEncode(scopeString)}" +
                             $"&state={state}" +
                             $"&response_mode=query";
                _logger.LogInformation("OAuth flow initiated for client {ClientName} (ID: {ClientId}) in organization {OrganizationId}",
                    request.ClientName, request.ClientId, organizationId);
                return new OAuthInitiateResponse
                {
                    AuthorizationUrl = authUrl,
                    State = state,
                    ExpiresAt = DateTime.UtcNow.Add(_stateCacheDuration)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate OAuth flow for client {ClientId}", request.ClientId);
                throw;
            }
        }
        public async Task<bool> HandleOAuthCallbackAsync(OAuthCallbackRequest request)
        {
            try
            {
                _logger.LogInformation("OAuth callback received - Code: {HasCode}, State: {State}, Error: {Error}",
                    !string.IsNullOrEmpty(request.Code), request.State, request.Error);
                if (!string.IsNullOrEmpty(request.Error))
                {
                    _logger.LogWarning("OAuth callback received error: {Error} - {ErrorDescription}",
                        request.Error, request.ErrorDescription);
                    return false;
                }
                // Retrieve state data
                var stateKey = $"oauth_state_{request.State}";
                _logger.LogDebug("Looking for cached state with key: {StateKey}", stateKey);
                if (!_cache.TryGetValue(stateKey, out OAuthStateData? stateData) || stateData == null)
                {
                    _logger.LogWarning("Invalid or expired OAuth state: {State}. Checking cache keys...", request.State);
                    // Debug: List all cache keys to see what's actually cached
                    try
                    {
                        var field = typeof(MemoryCache).GetField("_coherentState",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field?.GetValue(_cache) is System.Collections.IDictionary dictionary)
                        {
                            var keys = new List<string>();
                            foreach (var key in dictionary.Keys)
                            {
                                keys.Add(key?.ToString() ?? "null");
                                if (keys.Count >= 5) break;
                            }
                            _logger.LogDebug("Current cache keys: {CacheKeys}", string.Join(", ", keys));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Could not enumerate cache keys: {Error}", ex.Message);
                    }
                    return false;
                }
                _logger.LogInformation("Found cached state for client {ClientId} in organization {OrganizationId}",
                    stateData.ClientId, stateData.OrganizationId);
                // Remove state from cache (one-time use)
                _cache.Remove(stateKey);
                // Exchange authorization code for tokens
                _logger.LogInformation("Exchanging authorization code for tokens...");
                var tokenResponse = await ExchangeCodeForTokensAsync(request.Code, stateData.RedirectUri);
                if (tokenResponse == null)
                {
                    _logger.LogError("Token exchange failed for state: {State}", request.State);
                    return false;
                }
                _logger.LogInformation("Token exchange successful, storing credentials...");
                // Store credentials securely
                await StoreCredentialsAsync(stateData, tokenResponse);
                _logger.LogInformation("OAuth tokens successfully stored for client {ClientName} (ID: {ClientId})",
                    stateData.ClientName, stateData.ClientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle OAuth callback for state: {State}", request.State);
                return false;
            }
        }
        public async Task<StoredCredentials?> GetStoredCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var secretName = $"client-{clientId}-oauth-tokens";
                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(organizationId);
                _logger.LogInformation("Retrieving credentials for client {ClientId} from MSP Key Vault", clientId);
                var secret = await mspKeyVaultClient.GetSecretAsync(secretName);
                var credentialsJson = secret.Value.Value;
                var credentials = JsonSerializer.Deserialize<StoredCredentials>(credentialsJson);
                // Check if token is expired (with 5 minute buffer)
                if (credentials != null && credentials.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Access token expired for client {ClientId}, attempting refresh", clientId);
                    // Try to refresh the token
                    var refreshed = await RefreshTokensAsync(clientId, organizationId);
                    if (refreshed)
                    {
                        // Retrieve the refreshed credentials
                        var refreshedSecret = await mspKeyVaultClient.GetSecretAsync(secretName);
                        credentials = JsonSerializer.Deserialize<StoredCredentials>(refreshedSecret.Value.Value);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh tokens for client {ClientId}", clientId);
                        return null;
                    }
                }
                return credentials;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("No stored credentials found for client {ClientId}", clientId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve stored credentials for client {ClientId}", clientId);
                return null;
            }
        }
        public async Task<bool> RefreshTokensAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var secretName = $"client-{clientId}-oauth-tokens";
                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(organizationId);
                // Get current credentials
                var secret = await mspKeyVaultClient.GetSecretAsync(secretName);
                var credentials = JsonSerializer.Deserialize<StoredCredentials>(secret.Value.Value);
                if (credentials == null)
                {
                    return false;
                }
                // Exchange refresh token for new access token
                var tokenResponse = await RefreshAccessTokenAsync(credentials.RefreshToken);
                if (tokenResponse == null)
                {
                    return false;
                }
                // Update stored credentials
                var stateData = new OAuthStateData
                {
                    ClientId = clientId,
                    ClientName = credentials.ClientName,
                    OrganizationId = organizationId
                };
                await StoreCredentialsAsync(stateData, tokenResponse);
                _logger.LogInformation("Successfully refreshed tokens for client {ClientId}", clientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh tokens for client {ClientId}", clientId);
                return false;
            }
        }
        public async Task<bool> TestCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var credentials = await GetStoredCredentialsAsync(clientId, organizationId);
                if (credentials == null)
                {
                    return false;
                }
                // Test credentials by calling Azure Resource Manager API
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");
                var response = await _httpClient.GetAsync("https://management.azure.com/subscriptions?api-version=2020-01-01");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test credentials for client {ClientId}", clientId);
                return false;
            }
        }
        public async Task<bool> RevokeCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var secretName = $"client-{clientId}-oauth-tokens";
                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(organizationId);
                // Start delete operation (marks for deletion, then purges after retention period)
                await mspKeyVaultClient.StartDeleteSecretAsync(secretName);
                _logger.LogInformation("OAuth credentials revoked for client {ClientId}", clientId);
                return true;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Secret doesn't exist, consider it already revoked
                _logger.LogInformation("No credentials found to revoke for client {ClientId}", clientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to revoke credentials for client {ClientId}", clientId);
                return false;
            }
        }
        private async Task<OAuthTokenResponse?> ExchangeCodeForTokensAsync(string code, string redirectUri)
        {
            try
            {
                // Read OAuth secrets directly from Key Vault
                var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
                var clientSecretSecret = await _secretClient.GetSecretAsync("oauth-client-secret");
                var tenantIdSecret = await _secretClient.GetSecretAsync("oauth-tenant-id");
                var clientId = clientIdSecret.Value.Value;
                var clientSecret = clientSecretSecret.Value.Value;
                var tenantId = tenantIdSecret.Value.Value;
                var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
                var formData = new Dictionary<string, string>
                {
                    {"client_id", clientId},
                    {"client_secret", clientSecret},
                    {"code", code},
                    {"grant_type", "authorization_code"},
                    {"redirect_uri", redirectUri},
                    {"scope", "https://management.azure.com/user_impersonation openid profile email"}
                };
                var formContent = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(tokenEndpoint, formContent);
                var jsonContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Token response: {TokenResponse}", jsonContent);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Token exchange failed: {StatusCode} - {Content}",
                        response.StatusCode, jsonContent);
                    return null;
                }
                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                // Check if required properties exist
                if (!tokenData.TryGetProperty("access_token", out var accessTokenElement))
                {
                    _logger.LogError("Missing 'access_token' in token response: {Response}", jsonContent);
                    return null;
                }
                if (!tokenData.TryGetProperty("expires_in", out var expiresInElement))
                {
                    _logger.LogError("Missing 'expires_in' in token response: {Response}", jsonContent);
                    return null;
                }
                var accessToken = accessTokenElement.GetString();
                var tokenType = tokenData.TryGetProperty("token_type", out var tokenTypeElement)
                    ? tokenTypeElement.GetString() ?? "Bearer" : "Bearer";
                var refreshToken = tokenData.TryGetProperty("refresh_token", out var refreshTokenElement)
                    ? refreshTokenElement.GetString() ?? "" : "";
                var scope = tokenData.TryGetProperty("scope", out var scopeElement)
                    ? scopeElement.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Access token is null or empty in response: {Response}", jsonContent);
                    return null;
                }
                _logger.LogInformation("Token exchange successful - AccessToken length: {Length}, HasRefreshToken: {HasRefresh}",
                    accessToken.Length, !string.IsNullOrEmpty(refreshToken));
                return new OAuthTokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInElement.GetInt32()),
                    TokenType = tokenType,
                    Scope = scope
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to exchange authorization code for tokens");
                return null;
            }
        }
        private async Task<OAuthTokenResponse?> RefreshAccessTokenAsync(string refreshToken)
        {
            try
            {
                // Read OAuth secrets directly from Key Vault
                var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
                var clientSecretSecret = await _secretClient.GetSecretAsync("oauth-client-secret");
                var tenantIdSecret = await _secretClient.GetSecretAsync("oauth-tenant-id");
                var clientId = clientIdSecret.Value.Value;
                var clientSecret = clientSecretSecret.Value.Value;
                var tenantId = tenantIdSecret.Value.Value;
                var tokenEndpoint = $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token";
                var formData = new Dictionary<string, string>
                {
                    {"client_id", clientId},
                    {"client_secret", clientSecret},
                    {"refresh_token", refreshToken},
                    {"grant_type", "refresh_token"},
                    {"scope", "https://management.azure.com/user_impersonation"}
                };
                var formContent = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(tokenEndpoint, formContent);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token refresh failed: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return null;
                }
                var jsonContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);
                return new OAuthTokenResponse
                {
                    AccessToken = tokenData.GetProperty("access_token").GetString()!,
                    RefreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString()! : refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
                    TokenType = tokenData.GetProperty("token_type").GetString()!,
                    Scope = tokenData.TryGetProperty("scope", out var scope) ? scope.GetString()! : ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh access token");
                return null;
            }
        }
        private async Task StoreCredentialsAsync(OAuthStateData stateData, OAuthTokenResponse tokenResponse)
        {
            try
            {
                var credentials = new StoredCredentials
                {
                    AccessToken = tokenResponse.AccessToken,
                    RefreshToken = tokenResponse.RefreshToken,
                    ExpiresAt = tokenResponse.ExpiresAt,
                    Scope = tokenResponse.Scope,
                    StoredAt = DateTime.UtcNow,
                    ClientId = stateData.ClientId,
                    ClientName = stateData.ClientName
                };
                var credentialsJson = JsonSerializer.Serialize(credentials);
                var secretName = $"client-{stateData.ClientId}-oauth-tokens";
                _logger.LogInformation("Storing credentials for client {ClientId} in organization {OrganizationId}",
                    stateData.ClientId, stateData.OrganizationId);
                // Get MSP-specific Key Vault client
                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(stateData.OrganizationId);
                // Store credentials in MSP Key Vault
                await mspKeyVaultClient.SetSecretAsync(secretName, credentialsJson);
                _logger.LogInformation("Credentials stored for client {ClientName} (ID: {ClientId}) in MSP Key Vault",
                    stateData.ClientName, stateData.ClientId);
            }
            catch (Exception ex)
            {
                var environment = _configuration["Environment"] ?? "dev";
                var uniqueSuffix = _configuration["UniqueSuffix"] ?? "cmp001";
                var orgShort = stateData.OrganizationId.ToString("N").Substring(0, 8).ToLowerInvariant();
                var expectedKeyVault = $"kv-{environment}-{orgShort}-{uniqueSuffix}";
                _logger.LogError(ex, "Failed to store credentials for client {ClientId} in organization {OrganizationId}. " +
                    "Expected Key Vault: {ExpectedKeyVault}. You may need to run: ./deploy-msp-keyvault.sh \"{OrgShort}\" \"{Environment}\" \"rg-compass-dev\"",
                    stateData.ClientId, stateData.OrganizationId, expectedKeyVault, orgShort, environment);
                throw;
            }
        }
        private async Task<SecretClient> GetMspKeyVaultClientAsync(Guid organizationId)
        {
            // Build Key Vault name using organization ID
            var environment = _configuration["Environment"] ?? "dev";
            var uniqueSuffix = _configuration["UniqueSuffix"] ?? "cmp001";
            // Create short organization identifier (8 chars max, alphanumeric only)
            var orgShort = organizationId.ToString("N").Substring(0, 8).ToLowerInvariant();
            var mspKeyVaultName = $"kv-{environment}-{orgShort}-{uniqueSuffix}";
            var keyVaultUri = $"https://{mspKeyVaultName}.vault.azure.net/";
            _logger.LogInformation("Connecting to MSP Key Vault: {KeyVaultName} ({KeyVaultUri}) for organization {OrganizationId}",
                mspKeyVaultName, keyVaultUri, organizationId);
            return new SecretClient(new Uri(keyVaultUri), _credential);
        }
    }
}