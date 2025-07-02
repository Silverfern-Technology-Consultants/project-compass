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
                _logger.LogInformation("Exchanging authorization code for tokens...");
                var tokenResponse = await ExchangeCodeForTokensAsync(request.Code, stateData.RedirectUri);
                if (tokenResponse == null)
                {
                    _logger.LogError("Token exchange failed for state: {State}", request.State);

                    // Store token exchange failure info
                    var errorKey = $"oauth_error_{request.State}";
                    var errorInfo = new OAuthErrorInfo
                    {
                        Error = "token_exchange_failed",
                        ErrorDescription = "Failed to exchange authorization code for access tokens",
                        IsUserError = false,
                        UserMessage = "Authentication setup failed. Please try again or contact support if the issue persists.",
                        Timestamp = DateTime.UtcNow
                    };
                    _cache.Set(errorKey, errorInfo, TimeSpan.FromMinutes(10));

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

                var tokenEndpoint = $"https://login.microsoftonline.com/common/oauth2/v2.0/token";

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
                var keyVaultInfo = GetMspKeyVaultInfo(stateData.OrganizationId);
                _logger.LogError(ex, "Failed to store credentials for client {ClientId} in organization {OrganizationId}. " +
                    "Key Vault: {KeyVaultName}",
                    stateData.ClientId, stateData.OrganizationId, keyVaultInfo.Name);
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