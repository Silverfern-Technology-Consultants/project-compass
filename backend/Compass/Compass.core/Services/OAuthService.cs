using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.Security.KeyVault.Secrets;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
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
        private readonly ArmClient _armClient;

        // OAuth state cache duration (10 minutes)
        private readonly TimeSpan _stateCacheDuration = TimeSpan.FromMinutes(10);
        
        // NEW: Token cache duration (5 minutes to balance performance and freshness)
        private readonly TimeSpan _tokenCacheDuration = TimeSpan.FromMinutes(5);

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
            _armClient = new ArmClient(_credential);
        }
        public async Task<OAuthInitiateResponse> InitiateOAuthFlowWithScopesAsync(
    OAuthInitiateRequest request,
    Guid organizationId,
    OAuthScopeTypes scopeTypes)
        {
            try
            {
                // Update the request with scope types
                request.ScopeTypes = scopeTypes;

                // Check if MSP Key Vault exists
                var keyVaultExists = await CheckMspKeyVaultExistsAsync(organizationId);

                if (!keyVaultExists)
                {
                    var progressId = Guid.NewGuid().ToString();
                    _cache.Set($"oauth_progress_{progressId}", new OAuthProgressResponse
                    {
                        ProgressId = progressId,
                        Status = "Creating",
                        Message = "Setting up secure storage for your organization...",
                        ProgressPercentage = 0
                    }, TimeSpan.FromMinutes(10));

                    _ = Task.Run(async () => await CreateKeyVaultWithProgressAsync(progressId, request, organizationId));

                    return new OAuthInitiateResponse
                    {
                        RequiresKeyVaultCreation = true,
                        ProgressId = progressId,
                        State = "pending",
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                        RequestedScopes = scopeTypes
                    };
                }

                return await CreateOAuthUrlWithScopesAsync(request, organizationId, scopeTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate OAuth flow with scopes {ScopeTypes} for client {ClientId}",
                    scopeTypes, request.ClientId);
                throw;
            }
        }

        // NEW: Create OAuth URL with specific scopes
        private async Task<OAuthInitiateResponse> CreateOAuthUrlWithScopesAsync(
            OAuthInitiateRequest request,
            Guid organizationId,
            OAuthScopeTypes scopeTypes)
        {
            var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
            var tenantIdSecret = await _secretClient.GetSecretAsync("oauth-tenant-id");
            var clientId = clientIdSecret.Value.Value;
            var tenantId = tenantIdSecret.Value.Value;
            var redirectUri = _configuration["OAuth:RedirectUri"];

            // Select appropriate scopes based on scope type
            var scopes = GetScopesForType(scopeTypes);
            var permissions = GetPermissionsForType(scopeTypes);

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
            {
                throw new InvalidOperationException("OAuth configuration is missing");
            }

            // Generate unique state
            var state = Guid.NewGuid().ToString();
            var stateData = new OAuthStateData
            {
                ClientId = request.ClientId,
                ClientName = request.ClientName,
                OrganizationId = organizationId,
                RedirectUri = redirectUri!,
                CreatedAt = DateTime.UtcNow,
                Description = request.Description,
                RequestedScopes = scopeTypes // NEW: Track requested scopes
            };

            _cache.Set($"oauth_state_{state}", stateData, _stateCacheDuration);

            // Build authorization URL with selected scopes
            var scopeString = string.Join(" ", scopes);
            var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
                         $"?client_id={clientId}" +
                         $"&response_type=code" +
                         $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                         $"&scope={HttpUtility.UrlEncode(scopeString)}" +
                         $"&state={state}" +
                         $"&response_mode=query";

            return new OAuthInitiateResponse
            {
                AuthorizationUrl = authUrl,
                State = state,
                ExpiresAt = DateTime.UtcNow.Add(_stateCacheDuration),
                RequiresKeyVaultCreation = false,
                RequestedScopes = scopeTypes,
                RequestedPermissions = permissions
            };
        }

        // NEW: Get scopes for specific type
        private List<string> GetScopesForType(OAuthScopeTypes scopeTypes)
        {
            var configSection = _configuration.GetSection("OAuth");

            _logger.LogInformation("Getting scopes for type: {ScopeTypes}", scopeTypes);

            var result = scopeTypes switch
            {
                OAuthScopeTypes.ResourceManager => configSection.GetSection("AzureResourceManagerScopes")
                    .GetChildren().Select(x => x.Value).Where(x => x != null).ToList()!,
                OAuthScopeTypes.MicrosoftGraph => configSection.GetSection("MicrosoftGraphScopes")
                    .GetChildren().Select(x => x.Value).Where(x => x != null).ToList()!,
                OAuthScopeTypes.Both => configSection.GetSection("CombinedScopes")
                    .GetChildren().Select(x => x.Value).Where(x => x != null).ToList()!,
                _ => new List<string> { "https://management.azure.com/user_impersonation", "offline_access", "openid", "profile", "email" }
            };

            _logger.LogInformation("Resolved {Count} scopes: {Scopes}", result.Count, string.Join(", ", result));

            return result;
        }

        // NEW: Get human-readable permissions for scope type
        private List<string> GetPermissionsForType(OAuthScopeTypes scopeTypes)
        {
            return scopeTypes switch
            {
                OAuthScopeTypes.ResourceManager => new List<string>
        {
            "Read Azure subscriptions and resources",
            "Access Azure Resource Manager"
        },
                OAuthScopeTypes.MicrosoftGraph => new List<string>
        {
            "Read directory information",
            "Read user and group information",
            "Read application registrations",
            "Read security policies",
            "Read conditional access policies",        // ENHANCED
            "Read role assignments and definitions",   // NEW
            "Read audit logs and sign-in activity"    // ENHANCED
        },
                OAuthScopeTypes.Both => new List<string>
        {
            "Read Azure subscriptions and resources",
            "Access Azure Resource Manager",
            "Read directory information",
            "Read user and group information",
            "Read application registrations",
            "Read security policies",
            "Read conditional access policies",        // ENHANCED
            "Read role assignments and definitions",   // NEW
            "Read audit logs and sign-in activity"    // ENHANCED
        },
                _ => new List<string> { "Basic Azure access" }
            };
        }

        // NEW: Get Microsoft Graph credentials with caching
        public async Task<GraphTokenCredentials?> GetGraphCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                // Check cache first
                var cacheKey = $"graph_credentials_{organizationId}_{clientId}";
                if (_cache.TryGetValue(cacheKey, out GraphTokenCredentials? cachedCredentials))
                {
                    // Check if cached credentials are still valid (not expired within 5 minutes)
                    if (cachedCredentials.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
                    {
                        _logger.LogDebug("Using cached Graph credentials for client {ClientId}", clientId);
                        return cachedCredentials;
                    }
                    else
                    {
                        _logger.LogDebug("Cached Graph credentials expired for client {ClientId}, fetching fresh ones", clientId);
                        _cache.Remove(cacheKey);
                    }
                }

                await EnsureMspKeyVaultExistsAsync(organizationId);

                var secretName = $"client-{clientId}-oauth-tokens";
                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(organizationId);

                _logger.LogInformation("Retrieving Graph credentials for client {ClientId}", clientId);

                var secret = await mspKeyVaultClient.GetSecretAsync(secretName);
                var credentialsJson = secret.Value.Value;
                var credentials = JsonSerializer.Deserialize<StoredCredentials>(credentialsJson);

                if (credentials == null || string.IsNullOrEmpty(credentials.GraphAccessToken))
                {
                    _logger.LogInformation("No Graph credentials found for client {ClientId} - OAuth re-consent required", clientId);
                    return null;
                }

                // Check if Graph token is expired (with 5 minute buffer)
                if (credentials.GraphExpiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Graph token expired for client {ClientId}, attempting refresh", clientId);

                    var refreshed = await RefreshGraphTokensAsync(clientId, organizationId);
                    if (!refreshed)
                    {
                        _logger.LogWarning("Failed to refresh Graph tokens for client {ClientId} - OAuth re-consent may be required", clientId);
                        return null;
                    }

                    // Get refreshed credentials
                    var refreshedSecret = await mspKeyVaultClient.GetSecretAsync(secretName);
                    credentials = JsonSerializer.Deserialize<StoredCredentials>(refreshedSecret.Value.Value);
                }

                var result = new GraphTokenCredentials
                {
                    AccessToken = credentials.GraphAccessToken!,
                    RefreshToken = credentials.GraphRefreshToken ?? "",
                    ExpiresAt = credentials.GraphExpiresAt ?? DateTime.UtcNow,
                    Scope = credentials.GraphScope ?? "",
                    GrantedPermissions = ParseGrantedPermissions(credentials.GraphScope)
                };

                // Cache the credentials for performance
                _cache.Set(cacheKey, result, _tokenCacheDuration);
                _logger.LogDebug("Cached Graph credentials for client {ClientId}", clientId);

                return result;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("No stored Graph credentials found for client {ClientId}", clientId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Graph credentials for client {ClientId}", clientId);
                return null;
            }
        }

        // NEW: Test Microsoft Graph credentials
        public async Task<bool> TestGraphCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var credentials = await GetGraphCredentialsAsync(clientId, organizationId);
                if (credentials == null)
                {
                    return false;
                }

                // Test credentials by calling Microsoft Graph API
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

                var response = await _httpClient.GetAsync("https://graph.microsoft.com/v1.0/me");
                var isValid = response.IsSuccessStatusCode;

                _logger.LogInformation("Graph credentials test for client {ClientId}: {IsValid}", clientId, isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test Graph credentials for client {ClientId}", clientId);
                return false;
            }
        }

        // NEW: Refresh Graph tokens specifically
        public async Task<bool> RefreshGraphTokensAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var secretName = $"client-{clientId}-oauth-tokens";
                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(organizationId);

                var secret = await mspKeyVaultClient.GetSecretAsync(secretName);
                var credentials = JsonSerializer.Deserialize<StoredCredentials>(secret.Value.Value);

                if (credentials == null || string.IsNullOrEmpty(credentials.GraphRefreshToken))
                {
                    _logger.LogWarning("No Graph refresh token found for client {ClientId}", clientId);
                    return false;
                }

                // Refresh Graph token using Graph-specific scopes
                var tokenResponse = await RefreshAccessTokenWithScopesAsync(
                    credentials.GraphRefreshToken,
                    OAuthScopeTypes.MicrosoftGraph);

                if (tokenResponse == null)
                {
                    return false;
                }

                // Update only Graph tokens, preserve ARM tokens
                credentials.GraphAccessToken = tokenResponse.AccessToken;
                credentials.GraphRefreshToken = tokenResponse.RefreshToken;
                credentials.GraphExpiresAt = tokenResponse.ExpiresAt;
                credentials.GraphScope = tokenResponse.Scope;

                var updatedJson = JsonSerializer.Serialize(credentials);
                await mspKeyVaultClient.SetSecretAsync(secretName, updatedJson);

                // Invalidate cache after updating credentials
                var cacheKey = $"graph_credentials_{organizationId}_{clientId}";
                _cache.Remove(cacheKey);

                _logger.LogInformation("Successfully refreshed Graph tokens for client {ClientId}", clientId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh Graph tokens for client {ClientId}", clientId);
                return false;
            }
        }

        // NEW: Check what scopes are available for a client
        public async Task<OAuthScopeTypes> GetAvailableScopesAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var credentials = await GetStoredCredentialsAsync(clientId, organizationId);

                if (credentials == null)
                {
                    return OAuthScopeTypes.ResourceManager; // Default fallback
                }

                return credentials.AvailableScopes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available scopes for client {ClientId}", clientId);
                return OAuthScopeTypes.ResourceManager;
            }
        }

        // NEW: Upgrade existing OAuth to include Graph scopes
        public async Task<OAuthInitiateResponse> UpgradeToGraphScopesAsync(
            Guid clientId,
            Guid organizationId,
            string clientName)
        {
            try
            {
                _logger.LogInformation("Upgrading OAuth for client {ClientId} to include Graph scopes", clientId);

                var request = new OAuthInitiateRequest
                {
                    ClientId = clientId,
                    ClientName = clientName,
                    Description = "Upgrading to include Microsoft Graph permissions",
                    ScopeTypes = OAuthScopeTypes.Both
                };

                return await InitiateOAuthFlowWithScopesAsync(request, organizationId, OAuthScopeTypes.Both);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upgrade OAuth scopes for client {ClientId}", clientId);
                throw;
            }
        }

        // NEW: Get detailed granted permissions
        public async Task<List<string>> GetGrantedPermissionsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                var credentials = await GetStoredCredentialsAsync(clientId, organizationId);
                var graphCredentials = await GetGraphCredentialsAsync(clientId, organizationId);

                var permissions = new List<string>();

                if (credentials != null && !string.IsNullOrEmpty(credentials.AccessToken))
                {
                    permissions.AddRange(ParseGrantedPermissions(credentials.Scope));
                }

                if (graphCredentials != null)
                {
                    permissions.AddRange(graphCredentials.GrantedPermissions);
                }

                return permissions.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get granted permissions for client {ClientId}", clientId);
                return new List<string>();
            }
        }

        // NEW: Parse granted permissions from scope string
        private List<string> ParseGrantedPermissions(string? scope)
        {
            if (string.IsNullOrEmpty(scope))
                return new List<string>();

            var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var permissions = new List<string>();

            foreach (var s in scopes)
            {
                var permission = s switch
                {
                    // Azure Resource Manager
                    "https://management.azure.com/user_impersonation" => "Azure Resource Manager access",

                    // Directory and Users
                    "https://graph.microsoft.com/Directory.Read.All" => "Read directory data",
                    "https://graph.microsoft.com/User.Read.All" => "Read all users",
                    "https://graph.microsoft.com/Group.Read.All" => "Read all groups",

                    // Applications and Security
                    "https://graph.microsoft.com/Application.Read.All" => "Read applications",

                    // Policies and Conditional Access (ENHANCED)
                    "https://graph.microsoft.com/Policy.Read.All" => "Read security policies",
                    "https://graph.microsoft.com/Policy.Read.ConditionalAccess" => "Read conditional access policies",

                    // RBAC and Role Management (NEW)
                    "https://graph.microsoft.com/RoleManagement.Read.Directory" => "Read role assignments and definitions",

                    // Devices and Compliance
                    "https://graph.microsoft.com/Device.Read.All" => "Read device information",
                    "https://graph.microsoft.com/DeviceManagementManagedDevices.Read.All" => "Read managed devices",

                    // Security Events and Risk Assessment
                    "https://graph.microsoft.com/SecurityEvents.Read.All" => "Read security events",
                    "https://graph.microsoft.com/IdentityRiskyUser.Read.All" => "Read risky user data",
                    "https://graph.microsoft.com/IdentityProvider.Read.All" => "Read identity providers",

                    // Audit Logs and Activity (ENHANCED)
                    "https://graph.microsoft.com/AuditLog.Read.All" => "Read audit logs and sign-in activity",
                    "https://graph.microsoft.com/Directory.AccessAsUser.All" => "Access directory as signed-in user",

                    // Standard OAuth scopes
                    "offline_access" => "Maintain access when offline",
                    "openid" => "OpenID Connect sign-in",
                    "profile" => "View basic profile",
                    "email" => "View email address",

                    _ => null
                };

                if (permission != null)
                    permissions.Add(permission);
            }

            return permissions;
        }

        // NEW: Enhanced token refresh with scope support
        private async Task<OAuthTokenResponse?> RefreshAccessTokenWithScopesAsync(string refreshToken, OAuthScopeTypes scopeTypes)
        {
            try
            {
                var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
                var clientSecretSecret = await _secretClient.GetSecretAsync("oauth-client-secret");
                var clientId = clientIdSecret.Value.Value;
                var clientSecret = clientSecretSecret.Value.Value;
                var tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
                var scopes = GetScopesForType(scopeTypes);
                var scopeString = string.Join(" ", scopes);

                _logger.LogInformation("Attempting token refresh for scopes: {ScopeTypes} - {Scopes}", scopeTypes, scopeString);

                var formData = new Dictionary<string, string>
        {
            {"client_id", clientId},
            {"client_secret", clientSecret},
            {"refresh_token", refreshToken},
            {"grant_type", "refresh_token"},
            {"scope", scopeString}
        };

                var formContent = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(tokenEndpoint, formContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Token refresh failed for scope {ScopeTypes}: {StatusCode} - {Content}",
                        scopeTypes, response.StatusCode, errorContent);
                    return null;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                var returnedScope = tokenData.TryGetProperty("scope", out var scope) ? scope.GetString() : "";

                // CRITICAL: Validate that we got the scopes we requested
                var isGraphRequest = scopeTypes.HasFlag(OAuthScopeTypes.MicrosoftGraph);
                var gotGraphScope = !string.IsNullOrEmpty(returnedScope) && returnedScope.Contains("graph.microsoft.com");

                if (isGraphRequest && !gotGraphScope)
                {
                    _logger.LogWarning("Requested Graph scopes but only received: {ReturnedScope}. This indicates the refresh token doesn't have Graph consent.", returnedScope);
                    return null; // Don't return ARM tokens when Graph was requested
                }

                _logger.LogInformation("Token refresh successful - Requested: {RequestedScopes}, Received: {ReturnedScope}",
                    scopeString, returnedScope);

                return new OAuthTokenResponse
                {
                    AccessToken = tokenData.GetProperty("access_token").GetString()!,
                    RefreshToken = tokenData.TryGetProperty("refresh_token", out var rt) ? rt.GetString()! : refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(tokenData.GetProperty("expires_in").GetInt32()),
                    TokenType = tokenData.GetProperty("token_type").GetString()!,
                    Scope = returnedScope ?? scopeString
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh access token with scopes {ScopeTypes}", scopeTypes);
                return null;
            }
        }
        public async Task<OAuthInitiateResponse> InitiateOAuthFlowAsync(OAuthInitiateRequest request, Guid organizationId)
        {
            try
            {
                // Quick check if MSP Key Vault exists
                var keyVaultExists = await CheckMspKeyVaultExistsAsync(organizationId);

                if (!keyVaultExists)
                {
                    // Key Vault needs to be created - return progress tracking info
                    var progressId = Guid.NewGuid().ToString();

                    // Store progress in cache
                    _cache.Set($"oauth_progress_{progressId}", new OAuthProgressResponse
                    {
                        ProgressId = progressId,
                        Status = "Creating",
                        Message = "Setting up secure storage for your organization...",
                        ProgressPercentage = 0
                    }, TimeSpan.FromMinutes(10));

                    // Start Key Vault creation in background
                    _ = Task.Run(async () => await CreateKeyVaultWithProgressAsync(progressId, request, organizationId));

                    return new OAuthInitiateResponse
                    {
                        RequiresKeyVaultCreation = true,
                        ProgressId = progressId,
                        State = "pending",
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    };
                }

                // Key Vault exists - proceed normally
                return await CreateOAuthUrlAsync(request, organizationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initiate OAuth flow for client {ClientId}", request.ClientId);
                throw;
            }
        }

        public Task<OAuthProgressResponse?> GetOAuthProgressAsync(string progressId)
        {
            try
            {
                var progressKey = $"oauth_progress_{progressId}";
                var progress = _cache.Get<OAuthProgressResponse>(progressKey);
                return Task.FromResult(progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get OAuth progress for {ProgressId}", progressId);
                return Task.FromResult<OAuthProgressResponse?>(null);
            }
        }

        private async Task CreateKeyVaultWithProgressAsync(string progressId, OAuthInitiateRequest request, Guid organizationId)
        {
            try
            {
                var progressKey = $"oauth_progress_{progressId}";

                // Update progress: Starting creation
                UpdateProgress(progressKey, "Creating", "Creating secure Key Vault for your organization...", 10);

                // Create the Key Vault
                await CreateMspKeyVaultAsync(organizationId, (status, message, percentage) =>
                {
                    UpdateProgress(progressKey, "Creating", message, percentage);
                });

                // Update progress: Key Vault created, generating OAuth URL
                UpdateProgress(progressKey, "Creating", "Finalizing OAuth setup...", 90);

                // Generate OAuth URL
                var oauthResponse = await CreateOAuthUrlAsync(request, organizationId);

                // Update progress: Completed
                _cache.Set(progressKey, new OAuthProgressResponse
                {
                    ProgressId = progressId,
                    Status = "Completed",
                    Message = "Setup complete! You can now authorize access to your Azure environment.",
                    ProgressPercentage = 100,
                    AuthorizationUrl = oauthResponse.AuthorizationUrl,
                    State = oauthResponse.State,
                    ExpiresAt = oauthResponse.ExpiresAt
                }, TimeSpan.FromMinutes(5));

                _logger.LogInformation("OAuth setup completed for organization {OrganizationId}, progress ID {ProgressId}",
                    organizationId, progressId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Key Vault for OAuth setup, progress ID {ProgressId}", progressId);

                var progressKey = $"oauth_progress_{progressId}";
                _cache.Set(progressKey, new OAuthProgressResponse
                {
                    ProgressId = progressId,
                    Status = "Failed",
                    Message = $"Setup failed: {ex.Message}",
                    ProgressPercentage = 0
                }, TimeSpan.FromMinutes(5));
            }
        }

        private void UpdateProgress(string progressKey, string status, string message, int percentage)
        {
            var progress = _cache.Get<OAuthProgressResponse>(progressKey);
            if (progress != null)
            {
                progress.Status = status;
                progress.Message = message;
                progress.ProgressPercentage = percentage;
                _cache.Set(progressKey, progress, TimeSpan.FromMinutes(10));
            }
        }

        private async Task<OAuthInitiateResponse> CreateOAuthUrlAsync(OAuthInitiateRequest request, Guid organizationId)
        {
            // Original OAuth URL generation logic (moved from InitiateOAuthFlowAsync)
            var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
            var tenantIdSecret = await _secretClient.GetSecretAsync("oauth-tenant-id");
            var clientId = clientIdSecret.Value.Value;
            var tenantId = tenantIdSecret.Value.Value;
            var redirectUri = _configuration["OAuth:RedirectUri"];
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
            var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
                         $"?client_id={clientId}" +
                         $"&response_type=code" +
                         $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
                         $"&scope={HttpUtility.UrlEncode(scopeString)}" +
                         $"&state={state}" +
                         $"&response_mode=query";

            return new OAuthInitiateResponse
            {
                AuthorizationUrl = authUrl,
                State = state,
                ExpiresAt = DateTime.UtcNow.Add(_stateCacheDuration),
                RequiresKeyVaultCreation = false
            };
        }

        // Enhanced CreateMspKeyVaultAsync with progress callback
        private async Task CreateMspKeyVaultAsync(Guid organizationId, Action<string, string, int>? progressCallback = null)
        {
            try
            {
                progressCallback?.Invoke("Creating", "Reading configuration...", 20);

                var keyVaultInfo = GetMspKeyVaultInfo(organizationId);
                var resourceGroupName = await GetResourceGroupNameAsync();
                var subscriptionId = await GetSubscriptionIdAsync();
                var location = _configuration["Azure:Location"] ?? "Canada Central";

                progressCallback?.Invoke("Creating", "Validating permissions...", 30);

                // Get the main application's service principal Object ID for Key Vault access
                var compassAppObjectId = await GetCompassAppObjectIdAsync();

                _logger.LogInformation("Creating MSP Key Vault {KeyVaultName} in resource group {ResourceGroup} (subscription: {SubscriptionId})",
                    keyVaultInfo.Name, resourceGroupName, subscriptionId);

                progressCallback?.Invoke("Creating", "Initializing Azure resources...", 40);

                // Get subscription and resource group
                var subscriptionResource = _armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));

                // FIXED: Call Get() to fetch subscription data before accessing .Data
                var subscriptionResponse = await subscriptionResource.GetAsync();
                var subscription = subscriptionResponse.Value;

                var resourceGroupResponse = await subscription.GetResourceGroupAsync(resourceGroupName);
                var resourceGroup = resourceGroupResponse.Value;

                progressCallback?.Invoke("Creating", "Configuring Key Vault properties...", 50);

                // FIXED: Get the tenant ID properly from subscription data
                var tenantId = Guid.Parse(subscription.Data.TenantId!.ToString());

                // FIXED: Key Vault properties with proper constructor
                var vaultProperties = new KeyVaultProperties(tenantId, new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard))
                {
                    EnabledForTemplateDeployment = false,
                    EnabledForDiskEncryption = false,
                    EnabledForDeployment = false,
                    EnableSoftDelete = false,
                    SoftDeleteRetentionInDays = 7,
                    EnablePurgeProtection = true,
                    // REMOVED: PublicNetworkAccess - not available in this SDK version
                    NetworkRuleSet = new KeyVaultNetworkRuleSet()
                    {
                        Bypass = KeyVaultNetworkRuleBypassOption.AzureServices,
                        DefaultAction = KeyVaultNetworkRuleAction.Allow
                    }
                };

                progressCallback?.Invoke("Creating", "Setting up access permissions...", 60);

                // FIXED: Create permissions and add to access policy properly
                var permissions = new IdentityAccessPermissions();

                // Add secret permissions to the existing collections
                permissions.Secrets.Add(IdentityAccessSecretPermission.Get);
                permissions.Secrets.Add(IdentityAccessSecretPermission.List);
                permissions.Secrets.Add(IdentityAccessSecretPermission.Set);
                permissions.Secrets.Add(IdentityAccessSecretPermission.Delete);

                var accessPolicy = new KeyVaultAccessPolicy(tenantId, compassAppObjectId.ToString(), permissions);
                vaultProperties.AccessPolicies.Add(accessPolicy);

                // FIXED: Key Vault data with proper constructor including properties
                var createContent = new KeyVaultCreateOrUpdateContent(new AzureLocation(location), vaultProperties);

                // Add tags for identification
                createContent.Tags.Add("Purpose", "MSP-OAuth-Tokens");
                createContent.Tags.Add("MSP-Organization", organizationId.ToString());
                createContent.Tags.Add("Environment", keyVaultInfo.Environment);
                createContent.Tags.Add("CreatedBy", "Silverfern-Compass-Auto");
                createContent.Tags.Add("CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd"));

                progressCallback?.Invoke("Creating", "Creating Key Vault in Azure...", 70);

                // Get Key Vault collection from resource group
                var vaultCollection = resourceGroup.GetKeyVaults();

                // FIXED: Create the Key Vault with proper operation handling
                var operation = await vaultCollection.CreateOrUpdateAsync(
                    WaitUntil.Completed,
                    keyVaultInfo.Name,
                    createContent);

                // FIXED: Check operation result properly
                if (!operation.HasCompleted)
                {
                    throw new InvalidOperationException("Key Vault creation did not complete");
                }

                if (!operation.HasValue)
                {
                    throw new InvalidOperationException("Key Vault creation completed but returned no result");
                }

                progressCallback?.Invoke("Creating", "Finalizing Key Vault setup...", 85);

                _logger.LogInformation("Successfully created MSP Key Vault {KeyVaultName} at {KeyVaultUri}",
                    keyVaultInfo.Name, keyVaultInfo.Uri);

                // Wait a moment for the Key Vault to be fully ready
                await Task.Delay(TimeSpan.FromSeconds(5));

                progressCallback?.Invoke("Creating", "Key Vault ready!", 90);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create MSP Key Vault for organization {OrganizationId}", organizationId);
                progressCallback?.Invoke("Failed", $"Creation failed: {ex.Message}", 0);
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

                    // Store the error in cache for frontend to retrieve
                    if (!string.IsNullOrEmpty(request.State))
                    {
                        var errorKey = $"oauth_error_{request.State}";
                        var errorInfo = new OAuthErrorInfo
                        {
                            Error = request.Error,
                            ErrorDescription = request.ErrorDescription,
                            IsUserError = IsUserRecoverableError(request.Error),
                            UserMessage = GetUserFriendlyErrorMessage(request.Error, request.ErrorDescription),
                            Timestamp = DateTime.UtcNow
                        };

                        _cache.Set(errorKey, errorInfo, TimeSpan.FromMinutes(10));
                        _logger.LogInformation("Stored OAuth error info for state {State}: {ErrorType}", request.State, request.Error);
                    }

                    return false;
                }

                // Retrieve state data
                var stateKey = $"oauth_state_{request.State}";
                _logger.LogDebug("Looking for cached state with key: {StateKey}", stateKey);

                if (!_cache.TryGetValue(stateKey, out OAuthStateData? stateData) || stateData == null)
                {
                    _logger.LogWarning("Invalid or expired OAuth state: {State}", request.State);
                    return false;
                }

                _logger.LogInformation("Found cached state for client {ClientId} in organization {OrganizationId}",
                    stateData.ClientId, stateData.OrganizationId);

                // Remove state from cache (one-time use)
                _cache.Remove(stateKey);

                // Exchange authorization code for tokens
                _logger.LogInformation("Exchanging authorization code for ARM tokens...");
                var armTokenResponse = await ExchangeCodeForArmTokensAsync(request.Code, stateData.RedirectUri);
                if (armTokenResponse == null)
                {
                    _logger.LogError("ARM token exchange failed for state: {State}", request.State);

                    var errorKey = $"oauth_error_{request.State}";
                    var errorInfo = new OAuthErrorInfo
                    {
                        Error = "arm_token_exchange_failed",
                        ErrorDescription = "Failed to exchange authorization code for ARM access tokens",
                        IsUserError = false,
                        UserMessage = "Authentication setup failed. Please try again or contact support if the issue persists.",
                        Timestamp = DateTime.UtcNow
                    };
                    _cache.Set(errorKey, errorInfo, TimeSpan.FromMinutes(10));
                    return false;
                }

                _logger.LogInformation("ARM token exchange successful, getting Graph tokens...");

                // Use refresh token to get Graph tokens
                var graphTokenResponse = await ExchangeRefreshTokenForGraphTokensAsync(armTokenResponse.RefreshToken);
                if (graphTokenResponse == null)
                {
                    _logger.LogWarning("Graph token exchange failed, storing ARM tokens only");
                }

                _logger.LogInformation("Storing credentials...");

                // Store both sets of credentials
                await StoreBothCredentialsAsync(stateData, armTokenResponse, graphTokenResponse);

                _logger.LogInformation("OAuth tokens successfully stored for client {ClientName} (ID: {ClientId})",
                    stateData.ClientName, stateData.ClientId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle OAuth callback for state: {State}", request.State);

                // Store general error info
                if (!string.IsNullOrEmpty(request.State))
                {
                    var errorKey = $"oauth_error_{request.State}";
                    var errorInfo = new OAuthErrorInfo
                    {
                        Error = "callback_processing_failed",
                        ErrorDescription = ex.Message,
                        IsUserError = false,
                        UserMessage = "An unexpected error occurred during authentication setup. Please try again.",
                        Timestamp = DateTime.UtcNow
                    };
                    _cache.Set(errorKey, errorInfo, TimeSpan.FromMinutes(10));
                }

                return false;
            }
        }
        private async Task<OAuthTokenResponse?> ExchangeCodeForArmTokensAsync(string code, string redirectUri)
        {
            try
            {
                var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
                var clientSecretSecret = await _secretClient.GetSecretAsync("oauth-client-secret");

                var clientId = clientIdSecret.Value.Value;
                var clientSecret = clientSecretSecret.Value.Value;
                var tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

                // ARM scopes only for initial token exchange
                var armScopes = GetScopesForType(OAuthScopeTypes.ResourceManager);
                var scopeString = string.Join(" ", armScopes);

                var formData = new Dictionary<string, string>
        {
            {"client_id", clientId},
            {"client_secret", clientSecret},
            {"code", code},
            {"grant_type", "authorization_code"},
            {"redirect_uri", redirectUri},
            {"scope", scopeString}
        };

                var formContent = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(tokenEndpoint, formContent);
                var jsonContent = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("ARM token response: {TokenResponse}", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("ARM token exchange failed: {StatusCode} - {Content}",
                        response.StatusCode, jsonContent);
                    return null;
                }

                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                if (!tokenData.TryGetProperty("access_token", out var accessTokenElement) ||
                    !tokenData.TryGetProperty("expires_in", out var expiresInElement))
                {
                    _logger.LogError("Missing required properties in ARM token response: {Response}", jsonContent);
                    return null;
                }

                var accessToken = accessTokenElement.GetString();
                var refreshToken = tokenData.TryGetProperty("refresh_token", out var refreshTokenElement)
                    ? refreshTokenElement.GetString() ?? "" : "";
                var scope = tokenData.TryGetProperty("scope", out var scopeElement)
                    ? scopeElement.GetString() ?? "" : scopeString;

                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("ARM access token is null or empty");
                    return null;
                }

                _logger.LogInformation("ARM token exchange successful - AccessToken length: {Length}, HasRefreshToken: {HasRefresh}",
                    accessToken.Length, !string.IsNullOrEmpty(refreshToken));

                return new OAuthTokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInElement.GetInt32()),
                    TokenType = tokenData.TryGetProperty("token_type", out var tokenTypeElement)
                        ? tokenTypeElement.GetString() ?? "Bearer" : "Bearer",
                    Scope = scope
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to exchange authorization code for ARM tokens");
                return null;
            }
        }

        // Method 2: Exchange refresh token for Graph tokens
        private async Task<OAuthTokenResponse?> ExchangeRefreshTokenForGraphTokensAsync(string refreshToken)
        {
            try
            {
                var clientIdSecret = await _secretClient.GetSecretAsync("oauth-client-id");
                var clientSecretSecret = await _secretClient.GetSecretAsync("oauth-client-secret");

                var clientId = clientIdSecret.Value.Value;
                var clientSecret = clientSecretSecret.Value.Value;
                var tokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

                // Graph scopes only for refresh token exchange
                var graphScopes = GetScopesForType(OAuthScopeTypes.MicrosoftGraph);
                var scopeString = string.Join(" ", graphScopes);

                var formData = new Dictionary<string, string>
        {
            {"client_id", clientId},
            {"client_secret", clientSecret},
            {"refresh_token", refreshToken},
            {"grant_type", "refresh_token"},
            {"scope", scopeString}
        };

                var formContent = new FormUrlEncodedContent(formData);
                var response = await _httpClient.PostAsync(tokenEndpoint, formContent);
                var jsonContent = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("Graph token response: {TokenResponse}", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Graph token exchange failed: {StatusCode} - {Content}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                if (!tokenData.TryGetProperty("access_token", out var accessTokenElement) ||
                    !tokenData.TryGetProperty("expires_in", out var expiresInElement))
                {
                    _logger.LogError("Missing required properties in Graph token response: {Response}", jsonContent);
                    return null;
                }

                var accessToken = accessTokenElement.GetString();
                var newRefreshToken = tokenData.TryGetProperty("refresh_token", out var refreshTokenElement)
                    ? refreshTokenElement.GetString() ?? refreshToken : refreshToken; // Keep original if not provided
                var scope = tokenData.TryGetProperty("scope", out var scopeElement)
                    ? scopeElement.GetString() ?? "" : scopeString;

                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogError("Graph access token is null or empty");
                    return null;
                }

                _logger.LogInformation("Graph token exchange successful - AccessToken length: {Length}, Scope contains Graph: {HasGraph}",
                    accessToken.Length, scope.Contains("graph.microsoft.com"));

                return new OAuthTokenResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInElement.GetInt32()),
                    TokenType = tokenData.TryGetProperty("token_type", out var tokenTypeElement)
                        ? tokenTypeElement.GetString() ?? "Bearer" : "Bearer",
                    Scope = scope
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to exchange refresh token for Graph tokens");
                return null;
            }
        }

        // Method 3: Store both ARM and Graph credentials
        private async Task StoreBothCredentialsAsync(OAuthStateData stateData, OAuthTokenResponse armTokenResponse, OAuthTokenResponse? graphTokenResponse)
        {
            try
            {
                // Get existing credentials if they exist (for upgrades)
                StoredCredentials? existingCredentials = null;
                try
                {
                    existingCredentials = await GetStoredCredentialsAsync(stateData.ClientId, stateData.OrganizationId);
                }
                catch
                {
                    // Ignore errors, treat as new credential storage
                }

                var credentials = existingCredentials ?? new StoredCredentials
                {
                    ClientId = stateData.ClientId,
                    ClientName = stateData.ClientName,
                    StoredAt = DateTime.UtcNow
                };

                // Store ARM credentials
                credentials.AccessToken = armTokenResponse.AccessToken;
                credentials.RefreshToken = armTokenResponse.RefreshToken;
                credentials.ExpiresAt = armTokenResponse.ExpiresAt;
                credentials.Scope = armTokenResponse.Scope;
                credentials.AvailableScopes = OAuthScopeTypes.ResourceManager;

                // Store Graph credentials if available
                if (graphTokenResponse != null)
                {
                    credentials.GraphAccessToken = graphTokenResponse.AccessToken;
                    credentials.GraphRefreshToken = graphTokenResponse.RefreshToken;
                    credentials.GraphExpiresAt = graphTokenResponse.ExpiresAt;
                    credentials.GraphScope = graphTokenResponse.Scope;
                    credentials.AvailableScopes = OAuthScopeTypes.Both;

                    _logger.LogInformation("Stored both ARM and Graph credentials for client {ClientName} (ID: {ClientId})",
                        stateData.ClientName, stateData.ClientId);
                }
                else
                {
                    _logger.LogWarning("Stored ARM credentials only for client {ClientName} (ID: {ClientId}) - Graph token exchange failed",
                        stateData.ClientName, stateData.ClientId);
                }

                var credentialsJson = JsonSerializer.Serialize(credentials);
                var secretName = $"client-{stateData.ClientId}-oauth-tokens";

                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(stateData.OrganizationId);
                await mspKeyVaultClient.SetSecretAsync(secretName, credentialsJson);

                _logger.LogInformation("Enhanced credentials stored for client {ClientName} (ID: {ClientId}) with scopes: {AvailableScopes}",
                    stateData.ClientName, stateData.ClientId, credentials.AvailableScopes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store both credentials for client {ClientId}", stateData.ClientId);
                throw;
            }
        }
        public async Task<OAuthErrorInfo?> GetOAuthErrorAsync(string state)
        {
            try
            {
                var errorKey = $"oauth_error_{state}";
                var errorInfo = _cache.Get<OAuthErrorInfo>(errorKey);

                if (errorInfo != null)
                {
                    // Remove error from cache after retrieval (one-time use)
                    _cache.Remove(errorKey);
                }

                return errorInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve OAuth error for state: {State}", state);
                return null;
            }
        }
        private bool IsUserRecoverableError(string error)
        {
            return error switch
            {
                "access_denied" => true,
                "invalid_request" => false,
                "unauthorized_client" => false,
                "unsupported_response_type" => false,
                "invalid_scope" => false,
                "server_error" => false,
                "temporarily_unavailable" => true,
                _ => false
            };
        }

        private string GetUserFriendlyErrorMessage(string error, string? errorDescription)
        {
            return error switch
            {
                "access_denied" => "You declined to authorize access to your Azure environment. To set up OAuth access, please try again and click 'Accept' when prompted.",
                "invalid_request" => "There was a technical issue with the authorization request. Please try again or contact support.",
                "unauthorized_client" => "This application is not authorized to access your Azure tenant. Please contact your Azure administrator to add this application as an external user, or use a different Azure account that has access to both tenants.",
                "unsupported_response_type" => "There was a technical configuration issue. Please contact support.",
                "invalid_scope" => "The requested permissions are not available. Please contact support.",
                "server_error" => "Microsoft's authentication service is experiencing issues. Please try again in a few minutes.",
                "temporarily_unavailable" => "Microsoft's authentication service is temporarily unavailable. Please try again in a few minutes.",
                _ => !string.IsNullOrEmpty(errorDescription) ? errorDescription : "An unexpected error occurred during authentication. Please try again."
            };
        }

        public async Task<StoredCredentials?> GetStoredCredentialsAsync(Guid clientId, Guid organizationId)
        {
            try
            {
                // Ensure Key Vault exists before trying to access it
                await EnsureMspKeyVaultExistsAsync(organizationId);

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

                bool anyTokenRefreshed = false;

                // Refresh ARM tokens if they exist and are expired
                if (!string.IsNullOrEmpty(credentials.RefreshToken) &&
                    credentials.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Refreshing ARM tokens for client {ClientId}", clientId);
                    var armTokenResponse = await RefreshAccessTokenWithScopesAsync(credentials.RefreshToken, OAuthScopeTypes.ResourceManager);

                    if (armTokenResponse != null)
                    {
                        // Update only ARM token fields
                        credentials.AccessToken = armTokenResponse.AccessToken;
                        credentials.RefreshToken = armTokenResponse.RefreshToken;
                        credentials.ExpiresAt = armTokenResponse.ExpiresAt;
                        credentials.Scope = armTokenResponse.Scope;
                        anyTokenRefreshed = true;
                        _logger.LogInformation("ARM tokens refreshed successfully for client {ClientId}", clientId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh ARM tokens for client {ClientId}", clientId);
                    }
                }

                // Refresh Graph tokens if they exist and are expired
                if (!string.IsNullOrEmpty(credentials.GraphRefreshToken) &&
                    credentials.GraphExpiresAt <= DateTime.UtcNow.AddMinutes(5))
                {
                    _logger.LogInformation("Refreshing Graph tokens for client {ClientId}", clientId);
                    var graphTokenResponse = await RefreshAccessTokenWithScopesAsync(credentials.GraphRefreshToken, OAuthScopeTypes.MicrosoftGraph);

                    if (graphTokenResponse != null)
                    {
                        // Update only Graph token fields
                        credentials.GraphAccessToken = graphTokenResponse.AccessToken;
                        credentials.GraphRefreshToken = graphTokenResponse.RefreshToken;
                        credentials.GraphExpiresAt = graphTokenResponse.ExpiresAt;
                        credentials.GraphScope = graphTokenResponse.Scope;
                        anyTokenRefreshed = true;
                        _logger.LogInformation("Graph tokens refreshed successfully for client {ClientId}", clientId);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to refresh Graph tokens for client {ClientId} - refresh token may be expired, re-consent required", clientId);

                        // Mark Graph tokens as invalid but don't clear them yet
                        // This allows the status endpoint to show "needs re-consent"
                        _logger.LogInformation("Graph refresh failed - user will need to re-authorize with Graph permissions");
                    }
                }

                // Save updated credentials if any tokens were refreshed
                if (anyTokenRefreshed)
                {
                    var updatedJson = JsonSerializer.Serialize(credentials);
                    await mspKeyVaultClient.SetSecretAsync(secretName, updatedJson);
                    _logger.LogInformation("Updated credentials saved for client {ClientId}", clientId);
                }

                // Return true if we have valid ARM tokens (minimum requirement)
                bool hasValidArm = !string.IsNullOrEmpty(credentials.AccessToken) &&
                                  credentials.ExpiresAt > DateTime.UtcNow.AddMinutes(5);

                _logger.LogInformation("Token refresh completed for client {ClientId}. Valid ARM: {HasValidArm}, Any refreshed: {AnyRefreshed}",
                    clientId, hasValidArm, anyTokenRefreshed);

                return hasValidArm;
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

        private async Task EnsureMspKeyVaultExistsAsync(Guid organizationId)
        {
            try
            {
                var keyVaultInfo = GetMspKeyVaultInfo(organizationId);

                // Try to access the Key Vault first
                var exists = await CheckMspKeyVaultExistsAsync(organizationId);
                if (exists)
                {
                    _logger.LogDebug("MSP Key Vault {KeyVaultName} already exists", keyVaultInfo.Name);
                    return;
                }

                _logger.LogInformation("MSP Key Vault {KeyVaultName} does not exist, creating automatically...", keyVaultInfo.Name);

                // Create the Key Vault automatically
                await CreateMspKeyVaultAsync(organizationId);

                _logger.LogInformation("Successfully created MSP Key Vault {KeyVaultName} for organization {OrganizationId}",
                    keyVaultInfo.Name, organizationId);
            }
            catch (Exception ex)
            {
                var keyVaultInfo = GetMspKeyVaultInfo(organizationId);
                _logger.LogError(ex, "Failed to ensure MSP Key Vault {KeyVaultName} exists for organization {OrganizationId}",
                    keyVaultInfo.Name, organizationId);
                throw new InvalidOperationException($"Unable to create MSP Key Vault '{keyVaultInfo.Name}' for OAuth functionality. Error: {ex.Message}", ex);
            }
        }

        private async Task<bool> CheckMspKeyVaultExistsAsync(Guid organizationId)
        {
            try
            {
                var keyVaultInfo = GetMspKeyVaultInfo(organizationId);

                // Try to create a client and test basic connectivity
                var testClient = new SecretClient(new Uri(keyVaultInfo.Uri), _credential);

                // Try a simple operation that would fail if Key Vault doesn't exist
                await foreach (var page in testClient.GetPropertiesOfSecretsAsync().AsPages())
                {
                    // Just getting the first page is enough to test connectivity
                    break;
                }

                _logger.LogDebug("MSP Key Vault {KeyVaultName} exists and is accessible", keyVaultInfo.Name);
                return true;
            }
            catch (Exception ex) when (IsKeyVaultNotFoundError(ex))
            {
                var keyVaultInfo = GetMspKeyVaultInfo(organizationId);
                _logger.LogDebug("MSP Key Vault {KeyVaultName} does not exist or is not accessible", keyVaultInfo.Name);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking MSP Key Vault existence for organization {OrganizationId}", organizationId);
                return false;
            }
        }

        private (string Name, string Uri, string OrgShort, string Environment) GetMspKeyVaultInfo(Guid organizationId)
        {
            var environment = _configuration["Environment"] ?? "dev";
            var uniqueSuffix = _configuration["UniqueSuffix"] ?? "cmp001";
            var orgShort = organizationId.ToString("N").Substring(0, 8).ToLowerInvariant();
            var mspKeyVaultName = $"kv-{environment}-{orgShort}-{uniqueSuffix}";
            var keyVaultUri = $"https://{mspKeyVaultName}.vault.azure.net/";

            return (mspKeyVaultName, keyVaultUri, orgShort, environment);
        }

        private static bool IsKeyVaultNotFoundError(Exception ex)
        {
            return ex is Azure.RequestFailedException requestEx && requestEx.Status == 404 ||
                   ex is HttpRequestException ||
                   ex is System.Net.Sockets.SocketException ||
                   ex.Message.Contains("No such host is known") ||
                   ex.Message.Contains("vault.azure.net");
        }

        private async Task<string> GetSubscriptionIdAsync()
        {
            try
            {
                // Try to get from main Key Vault first
                try
                {
                    var subscriptionSecret = await _secretClient.GetSecretAsync("azure-subscription-id");
                    var subscriptionValue = subscriptionSecret.Value.Value;

                    if (!string.IsNullOrEmpty(subscriptionValue))
                    {
                        _logger.LogDebug("Using Azure Subscription ID from Key Vault");
                        return subscriptionValue;
                    }
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogWarning("azure-subscription-id not found in Key Vault, checking configuration...");
                }

                // Fallback to configuration
                var configuredSubscriptionId = _configuration["Azure:SubscriptionId"];
                if (!string.IsNullOrEmpty(configuredSubscriptionId))
                {
                    _logger.LogDebug("Using Azure Subscription ID from configuration");
                    return configuredSubscriptionId;
                }

                // If neither source has it, provide helpful error
                throw new InvalidOperationException(
                    "Azure Subscription ID not found. Please add it to Key Vault 'kv-dev-7yu4s2pu' as secret 'azure-subscription-id'. " +
                    "You can find this by running: az account show --query id -o tsv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Azure Subscription ID");
                throw;
            }
        }

        private async Task<string> GetResourceGroupNameAsync()
        {
            try
            {
                // Try to get from main Key Vault first
                try
                {
                    var resourceGroupSecret = await _secretClient.GetSecretAsync("azure-resource-group-name");
                    var resourceGroupValue = resourceGroupSecret.Value.Value;

                    if (!string.IsNullOrEmpty(resourceGroupValue))
                    {
                        _logger.LogDebug("Using Azure Resource Group Name from Key Vault: {ResourceGroup}", resourceGroupValue);
                        return resourceGroupValue;
                    }
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogWarning("azure-resource-group-name not found in Key Vault, checking configuration...");
                }

                // Fallback to configuration
                var configuredResourceGroup = _configuration["Azure:ResourceGroupName"];
                if (!string.IsNullOrEmpty(configuredResourceGroup))
                {
                    _logger.LogDebug("Using Azure Resource Group Name from configuration: {ResourceGroup}", configuredResourceGroup);
                    return configuredResourceGroup;
                }

                // Default fallback
                _logger.LogInformation("Using default resource group name: rg-compass-dev");
                return "rg-compass-dev";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Azure Resource Group Name, using default: rg-compass-dev");
                return "rg-compass-dev";
            }
        }

        private async Task<Guid> GetCompassAppObjectIdAsync()
        {
            try
            {
                // Try to get from main Key Vault first
                try
                {
                    var objectIdSecret = await _secretClient.GetSecretAsync("compass-app-object-id");
                    var objectIdValue = objectIdSecret.Value.Value;

                    if (!string.IsNullOrEmpty(objectIdValue) && Guid.TryParse(objectIdValue, out var keyVaultObjectId))
                    {
                        _logger.LogDebug("Using Compass App Object ID from Key Vault: {ObjectId}", keyVaultObjectId);
                        return keyVaultObjectId;
                    }
                }
                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogWarning("compass-app-object-id not found in Key Vault, checking configuration...");
                }

                // Fallback to configuration
                var configuredObjectId = _configuration["Azure:CompassAppObjectId"];
                if (!string.IsNullOrEmpty(configuredObjectId) && Guid.TryParse(configuredObjectId, out var objectId))
                {
                    _logger.LogDebug("Using Compass App Object ID from configuration: {ObjectId}", objectId);
                    return objectId;
                }

                // If neither source has it, provide helpful error
                throw new InvalidOperationException(
                    "Compass application Object ID not found. Please add it to Key Vault 'kv-dev-7yu4s2pu' as secret 'compass-app-object-id'. " +
                    "You can find this by running: az ad sp list --display-name 'Sentinel Cloud oAuth' --query '[0].id' -o tsv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Compass application Object ID");
                throw;
            }
        }

        private async Task<OAuthTokenResponse?> ExchangeCodeForTokensAsync(string code, string redirectUri, OAuthStateData stateData)


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

                var tokenEndpoint = $"https://login.microsoftonline.com/common/oauth2/v2.0/token";

                var formData = new Dictionary<string, string>
                {
                    {"client_id", clientId},
                    {"client_secret", clientSecret},
                    {"code", code},
                    {"grant_type", "authorization_code"},
                    {"redirect_uri", redirectUri},
                    {"scope", string.Join(" ", GetScopesForType(stateData.RequestedScopes))}
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

                var tokenEndpoint = $"https://login.microsoftonline.com/common/oauth2/v2.0/token";

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
                _logger.LogInformation("DEBUG: Token response scope: '{Scope}'", tokenResponse.Scope);
                _logger.LogInformation("DEBUG: Contains graph.microsoft.com: {HasGraph}", tokenResponse.Scope.Contains("graph.microsoft.com"));
                _logger.LogInformation("DEBUG: Contains management.azure.com: {HasARM}", tokenResponse.Scope.Contains("management.azure.com"));
                // Get existing credentials if they exist (for upgrades)
                StoredCredentials? existingCredentials = null;
                try
                {
                    existingCredentials = await GetStoredCredentialsAsync(stateData.ClientId, stateData.OrganizationId);
                }
                catch
                {
                    // Ignore errors, treat as new credential storage
                }

                var credentials = existingCredentials ?? new StoredCredentials
                {
                    ClientId = stateData.ClientId,
                    ClientName = stateData.ClientName,
                    StoredAt = DateTime.UtcNow
                };

                // Determine if this is Resource Manager or Graph token based on scope
                var isGraphToken = tokenResponse.Scope.Contains("graph.microsoft.com");
                var isResourceManagerToken = tokenResponse.Scope.Contains("management.azure.com");

                if (isResourceManagerToken)
                {
                    credentials.AccessToken = tokenResponse.AccessToken;
                    credentials.RefreshToken = tokenResponse.RefreshToken;
                    credentials.ExpiresAt = tokenResponse.ExpiresAt;
                    credentials.Scope = tokenResponse.Scope;
                    credentials.AvailableScopes |= OAuthScopeTypes.ResourceManager;
                }

                if (isGraphToken)
                {
                    credentials.GraphAccessToken = tokenResponse.AccessToken;
                    credentials.GraphRefreshToken = tokenResponse.RefreshToken;
                    credentials.GraphExpiresAt = tokenResponse.ExpiresAt;
                    credentials.GraphScope = tokenResponse.Scope;
                    credentials.AvailableScopes |= OAuthScopeTypes.MicrosoftGraph;
                }

                // If both scopes are present, this is a combined token
                if (isResourceManagerToken && isGraphToken)
                {
                    credentials.AvailableScopes = OAuthScopeTypes.Both;
                }

                var credentialsJson = JsonSerializer.Serialize(credentials);
                var secretName = $"client-{stateData.ClientId}-oauth-tokens";

                var mspKeyVaultClient = await GetMspKeyVaultClientAsync(stateData.OrganizationId);
                await mspKeyVaultClient.SetSecretAsync(secretName, credentialsJson);

                _logger.LogInformation("Enhanced credentials stored for client {ClientName} (ID: {ClientId}) with scopes: {AvailableScopes}",
                    stateData.ClientName, stateData.ClientId, credentials.AvailableScopes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store enhanced credentials for client {ClientId}", stateData.ClientId);
                throw;
            }
        }

        private async Task<SecretClient> GetMspKeyVaultClientAsync(Guid organizationId)
        {
            var keyVaultInfo = GetMspKeyVaultInfo(organizationId);

            _logger.LogInformation("Connecting to MSP Key Vault: {KeyVaultName} ({KeyVaultUri}) for organization {OrganizationId}",
                keyVaultInfo.Name, keyVaultInfo.Uri, organizationId);

            return new SecretClient(new Uri(keyVaultInfo.Uri), _credential);
        }
    }
}