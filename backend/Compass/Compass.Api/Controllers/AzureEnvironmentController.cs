using Compass.Core.Models;
using Compass.Core.Services;
using Compass.Core.Interfaces;
using Compass.Data;
using Compass.Data.Entities;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AzureEnvironmentController : ControllerBase
{
    private readonly CompassDbContext _context;
    private readonly IClientRepository _clientRepository;
    private readonly IClientService _clientService;
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureEnvironmentController> _logger;

    public AzureEnvironmentController(
     CompassDbContext context,
     IClientRepository clientRepository,
     IClientService clientService,
     IAzureResourceGraphService resourceGraphService,
     IOAuthService oauthService,
     IConfiguration configuration,
     ILogger<AzureEnvironmentController> logger)
    {
        _context = context;
        _clientRepository = clientRepository;
        _clientService = clientService;
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("client/{clientId}")]
    public async Task<ActionResult<List<AzureEnvironmentDto>>> GetClientEnvironments(Guid clientId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Validate client access
            var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, clientId, "ViewEnvironments");
            if (!hasClientAccess)
            {
                return Forbid("You don't have permission to view environments for this client");
            }

            var environments = await _context.AzureEnvironments
                .Where(e => e.ClientId == clientId)
                .OrderBy(e => e.Name)
                .ToListAsync();

            var environmentDtos = new List<AzureEnvironmentDto>();

            // Check OAuth status for each environment
            foreach (var e in environments)
            {
                bool hasOAuth = false;
                string connectionMethod = "DefaultCredentials";

                if (e.ClientId.HasValue)
                {
                    try
                    {
                        hasOAuth = await _oauthService.TestCredentialsAsync(e.ClientId.Value, organizationId.Value);
                        connectionMethod = hasOAuth ? "OAuth" : "DefaultCredentials";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Failed to test OAuth credentials for environment {EnvironmentId}: {Error}",
                            e.AzureEnvironmentId, ex.Message);
                        // Keep default values
                    }
                }

                environmentDtos.Add(new AzureEnvironmentDto
                {
                    AzureEnvironmentId = e.AzureEnvironmentId,
                    ClientId = e.ClientId,
                    Name = e.Name,
                    Description = e.Description,
                    TenantId = e.TenantId,
                    SubscriptionIds = e.SubscriptionIds,
                    ServicePrincipalId = e.ServicePrincipalId,
                    ServicePrincipalName = e.ServicePrincipalName,
                    IsActive = e.IsActive,
                    CreatedDate = e.CreatedDate,
                    LastAccessDate = e.LastAccessDate,
                    LastConnectionTest = e.LastConnectionTest,
                    LastConnectionTestDate = e.LastConnectionTestDate,
                    LastConnectionError = e.LastConnectionError,
                    HasOAuthCredentials = hasOAuth,
                    ConnectionMethod = connectionMethod
                });
            }

            return Ok(environmentDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environments for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{environmentId}")]
    public async Task<ActionResult<AzureEnvironmentDto>> GetEnvironment(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access through the environment's client
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ViewEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to view this environment");
                }
            }

            // Check OAuth status
            bool hasOAuth = false;
            string connectionMethod = "DefaultCredentials";

            if (environment.ClientId.HasValue)
            {
                try
                {
                    hasOAuth = await _oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId.Value);
                    connectionMethod = hasOAuth ? "OAuth" : "DefaultCredentials";
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to test OAuth credentials for environment {EnvironmentId}: {Error}",
                        environmentId, ex.Message);
                }
            }

            var environmentDto = new AzureEnvironmentDto
            {
                AzureEnvironmentId = environment.AzureEnvironmentId,
                ClientId = environment.ClientId,
                Name = environment.Name,
                Description = environment.Description,
                TenantId = environment.TenantId,
                SubscriptionIds = environment.SubscriptionIds,
                ServicePrincipalId = environment.ServicePrincipalId,
                ServicePrincipalName = environment.ServicePrincipalName,
                IsActive = environment.IsActive,
                CreatedDate = environment.CreatedDate,
                LastAccessDate = environment.LastAccessDate,
                LastConnectionTest = environment.LastConnectionTest,
                LastConnectionTestDate = environment.LastConnectionTestDate,
                LastConnectionError = environment.LastConnectionError,
                HasOAuthCredentials = hasOAuth,
                ConnectionMethod = connectionMethod
            };

            return Ok(environmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<AzureEnvironmentDto>> CreateEnvironment([FromBody] CreateAzureEnvironmentRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            // Validate client access
            var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, request.ClientId, "ManageEnvironments");
            if (!hasClientAccess)
            {
                return Forbid("You don't have permission to create environments for this client");
            }

            // Verify client exists and belongs to organization
            var client = await _clientRepository.GetByIdAndOrganizationAsync(request.ClientId, organizationId.Value);
            if (client == null)
            {
                return NotFound("Client not found");
            }

            // Check for duplicate environment name within client
            var existingEnvironment = await _context.AzureEnvironments
                .FirstOrDefaultAsync(e => e.ClientId == request.ClientId &&
                                         e.Name.ToLower() == request.Name.ToLower() &&
                                         e.IsActive);

            if (existingEnvironment != null)
            {
                return BadRequest("An environment with this name already exists for this client");
            }

            var environment = new AzureEnvironment
            {
                ClientId = request.ClientId,
                CustomerId = customerId.Value, // Still track who created it
                Name = request.Name,
                Description = request.Description,
                TenantId = request.TenantId,
                SubscriptionIds = request.SubscriptionIds,
                ServicePrincipalId = request.ServicePrincipalId,
                ServicePrincipalName = request.ServicePrincipalName,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            _context.AzureEnvironments.Add(environment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Azure environment created: {EnvironmentId} ({Name}) for client {ClientId} by {CustomerId}",
                environment.AzureEnvironmentId, environment.Name, request.ClientId, customerId);

            var environmentDto = new AzureEnvironmentDto
            {
                AzureEnvironmentId = environment.AzureEnvironmentId,
                ClientId = environment.ClientId,
                Name = environment.Name,
                Description = environment.Description,
                TenantId = environment.TenantId,
                SubscriptionIds = environment.SubscriptionIds,
                ServicePrincipalId = environment.ServicePrincipalId,
                ServicePrincipalName = environment.ServicePrincipalName,
                IsActive = environment.IsActive,
                CreatedDate = environment.CreatedDate
            };

            return CreatedAtAction(nameof(GetEnvironment), new { environmentId = environment.AzureEnvironmentId }, environmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating environment for client {ClientId}", request.ClientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{environmentId}")]
    public async Task<ActionResult<AzureEnvironmentDto>> UpdateEnvironment(Guid environmentId, [FromBody] UpdateAzureEnvironmentRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ManageEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to update this environment");
                }
            }

            // Check for duplicate name if name is being changed
            if (request.Name != environment.Name)
            {
                var existingEnvironment = await _context.AzureEnvironments
                    .FirstOrDefaultAsync(e => e.ClientId == environment.ClientId &&
                                             e.Name.ToLower() == request.Name.ToLower() &&
                                             e.AzureEnvironmentId != environmentId &&
                                             e.IsActive);

                if (existingEnvironment != null)
                {
                    return BadRequest("An environment with this name already exists for this client");
                }
            }

            // Update environment properties
            environment.Name = request.Name;
            environment.Description = request.Description;
            environment.TenantId = request.TenantId;
            environment.SubscriptionIds = request.SubscriptionIds;
            environment.ServicePrincipalId = request.ServicePrincipalId;
            environment.ServicePrincipalName = request.ServicePrincipalName;
            environment.IsActive = request.IsActive;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Azure environment updated: {EnvironmentId} ({Name}) by {CustomerId}",
                environmentId, environment.Name, customerId);

            var environmentDto = new AzureEnvironmentDto
            {
                AzureEnvironmentId = environment.AzureEnvironmentId,
                ClientId = environment.ClientId,
                Name = environment.Name,
                Description = environment.Description,
                TenantId = environment.TenantId,
                SubscriptionIds = environment.SubscriptionIds,
                ServicePrincipalId = environment.ServicePrincipalId,
                ServicePrincipalName = environment.ServicePrincipalName,
                IsActive = environment.IsActive,
                CreatedDate = environment.CreatedDate,
                LastAccessDate = environment.LastAccessDate,
                LastConnectionTest = environment.LastConnectionTest,
                LastConnectionTestDate = environment.LastConnectionTestDate,
                LastConnectionError = environment.LastConnectionError
            };

            return Ok(environmentDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{environmentId}")]
    public async Task<IActionResult> DeleteEnvironment(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ManageEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to delete this environment");
                }
            }

            // Check if environment has any assessments
            var hasAssessments = await _context.Assessments
                .AnyAsync(a => a.EnvironmentId == environmentId);

            if (hasAssessments)
            {
                // Soft delete - mark as inactive
                environment.IsActive = false;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Azure environment soft deleted: {EnvironmentId} ({Name}) by {CustomerId}",
                    environmentId, environment.Name, customerId);

                return Ok(new { message = "Environment deactivated successfully (assessments exist)" });
            }
            else
            {
                // Hard delete - no assessments depend on it
                _context.AzureEnvironments.Remove(environment);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Azure environment deleted: {EnvironmentId} ({Name}) by {CustomerId}",
                    environmentId, environment.Name, customerId);

                return Ok(new { message = "Environment deleted successfully" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("oauth/initiate")]
    [Authorize]
    public async Task<ActionResult<OAuthInitiateResponse>> InitiateOAuth([FromBody] OAuthInitiateRequest request)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            var response = await _oauthService.InitiateOAuthFlowAsync(request, organizationId.Value);

            _logger.LogInformation("OAuth flow initiated for client {ClientName} by user {UserId}",
                request.ClientName, GetCurrentCustomerId());

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate OAuth flow for client {ClientId}", request.ClientId);
            return StatusCode(500, "Failed to initiate OAuth flow");
        }
    }
    [HttpGet("oauth/error/{state}")]
    [AllowAnonymous] // Frontend needs to access this after OAuth callback
    public async Task<ActionResult<OAuthErrorInfo>> GetOAuthError(string state)
    {
        try
        {
            var errorInfo = await _oauthService.GetOAuthErrorAsync(state);

            if (errorInfo == null)
            {
                return NotFound(new { message = "No error information found for this request" });
            }

            return Ok(errorInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve OAuth error for state: {State}", state);
            return StatusCode(500, "Failed to retrieve error information");
        }
    }

    [HttpPost("{environmentId}/test-connection")]
    public async Task<ActionResult<ConnectionTestResult>> TestEnvironmentConnection(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            var customerId = GetCurrentCustomerId();

            if (organizationId == null || customerId == null)
            {
                return BadRequest("Organization or customer context not found");
            }

            var environment = await _context.AzureEnvironments
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            // Validate client access
            if (environment.ClientId.HasValue)
            {
                var hasClientAccess = await _clientService.CanUserAccessClient(customerId.Value, environment.ClientId.Value, "ViewEnvironments");
                if (!hasClientAccess)
                {
                    return Forbid("You don't have permission to test this environment");
                }
            }

            // Validate that we have subscription IDs
            if (environment.SubscriptionIds == null || !environment.SubscriptionIds.Any())
            {
                var errorMessage = "No subscription IDs found for this environment";
                _logger.LogWarning("Connection test failed for environment {EnvironmentId}: {Error}", environmentId, errorMessage);

                environment.LastConnectionTest = false;
                environment.LastConnectionTestDate = DateTime.UtcNow;
                environment.LastConnectionError = errorMessage;
                environment.LastAccessDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new ConnectionTestResult
                {
                    Success = false,
                    Message = errorMessage,
                    ErrorCode = "NO_SUBSCRIPTIONS",
                    Details = new Dictionary<string, object>
                    {
                        ["EnvironmentId"] = environmentId,
                        ["SubscriptionIds"] = environment.SubscriptionIds ?? new List<string>(),
                        ["TestedAt"] = DateTime.UtcNow,
                        ["SubscriptionCount"] = 0
                    }
                });
            }

            var subscriptionArray = environment.SubscriptionIds.ToArray();
            _logger.LogInformation("Testing connection with subscription array: {SubscriptionArray}",
                string.Join(", ", subscriptionArray));

            bool canConnect;
            string? connectionMethod = null;
            string? connectionError = null;
            bool hasOAuth = false;

            // Try OAuth credentials first if environment has a client
            if (environment.ClientId.HasValue)
            {
                _logger.LogInformation("Attempting OAuth connection test for environment {EnvironmentId} with client {ClientId}",
                    environmentId, environment.ClientId);

                // Check if OAuth credentials exist first
                var hasStoredCredentials = await _oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId.Value);
                hasOAuth = hasStoredCredentials;

                if (hasStoredCredentials)
                {
                    canConnect = await _resourceGraphService.TestConnectionWithOAuthAsync(
                        subscriptionArray, environment.ClientId.Value, organizationId.Value);

                    connectionMethod = canConnect ? "OAuth" : null;

                    if (!canConnect)
                    {
                        connectionError = "OAuth credentials exist but failed to access Azure subscriptions. This may indicate the credentials have expired or lack proper permissions.";
                        _logger.LogWarning("OAuth connection failed for environment {EnvironmentId}: {Error}",
                            environmentId, connectionError);
                    }
                }
                else
                {
                    canConnect = false;
                    connectionError = "No OAuth credentials found for this client. Please set up OAuth access first.";
                    _logger.LogInformation("No OAuth credentials found for environment {EnvironmentId} with client {ClientId}",
                        environmentId, environment.ClientId);
                }
            }
            else
            {
                canConnect = false;
                connectionError = "Environment has no associated client for OAuth authentication.";
                _logger.LogInformation("Environment {EnvironmentId} has no associated client, skipping OAuth test", environmentId);
            }

            // Fallback to default credentials only if OAuth completely failed or no client
            bool defaultCredentialsWork = false;
            if (!canConnect)
            {
                _logger.LogInformation("Testing connection with default credentials for environment {EnvironmentId}", environmentId);

                try
                {
                    defaultCredentialsWork = await _resourceGraphService.TestConnectionAsync(subscriptionArray);

                    if (defaultCredentialsWork)
                    {
                        // Only use default credentials as primary method if OAuth isn't set up at all
                        if (!hasOAuth)
                        {
                            canConnect = true;
                            connectionMethod = "DefaultCredentials";
                            connectionError = null;
                        }
                        else
                        {
                            // OAuth is set up but failed - don't mask this with default credentials success
                            canConnect = false;
                            connectionMethod = "Failed";
                            connectionError = connectionError ?? "OAuth authentication failed, but default credentials could connect. Please refresh your OAuth access or contact support.";
                        }
                    }
                    else
                    {
                        connectionMethod = "Failed";
                        if (hasOAuth)
                        {
                            connectionError = connectionError ?? "Both OAuth and default credentials failed to connect.";
                        }
                        else
                        {
                            connectionError = "No OAuth credentials configured and default credentials failed to connect.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Default credentials test failed for environment {EnvironmentId}", environmentId);
                    defaultCredentialsWork = false;
                    connectionMethod = "Failed";

                    if (hasOAuth)
                    {
                        connectionError = $"OAuth failed and default credentials also failed: {ex.Message}";
                    }
                    else
                    {
                        connectionError = $"Default credentials failed: {ex.Message}";
                    }
                }
            }

            // Update environment with test results
            environment.LastConnectionTest = canConnect;
            environment.LastConnectionTestDate = DateTime.UtcNow;
            environment.LastConnectionError = canConnect ? null : connectionError;
            environment.LastAccessDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            // Determine appropriate success message
            string successMessage;
            if (canConnect)
            {
                if (connectionMethod == "OAuth")
                {
                    successMessage = "Successfully connected using OAuth authentication";
                }
                else if (connectionMethod == "DefaultCredentials")
                {
                    successMessage = hasOAuth
                        ? "Connected using default credentials (OAuth setup recommended for production use)"
                        : "Successfully connected using default credentials";
                }
                else
                {
                    successMessage = "Successfully connected to all subscriptions";
                }
            }
            else
            {
                successMessage = connectionError ?? "Failed to connect to Azure subscriptions";
            }

            _logger.LogInformation("Connection test for environment {EnvironmentId}: {Result} using {Method}. HasOAuth: {HasOAuth}",
                environmentId, canConnect ? "Success" : "Failed", connectionMethod, hasOAuth);

            return Ok(new ConnectionTestResult
            {
                Success = canConnect,
                Message = successMessage,
                ErrorCode = canConnect ? null : "CONNECTION_FAILED",
                Details = new Dictionary<string, object>
                {
                    ["EnvironmentId"] = environmentId,
                    ["SubscriptionIds"] = environment.SubscriptionIds,
                    ["TestedAt"] = DateTime.UtcNow,
                    ["SubscriptionCount"] = environment.SubscriptionIds.Count,
                    ["ConnectionMethod"] = connectionMethod ?? "Failed",
                    ["HasOAuthCredentials"] = hasOAuth,
                    ["DefaultCredentialsWork"] = defaultCredentialsWork,
                    ["OAuthAttempted"] = environment.ClientId.HasValue,
                    ["Error"] = connectionError
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for environment {EnvironmentId}", environmentId);

            // Update environment with error
            var environment = await _context.AzureEnvironments.FindAsync(environmentId);
            if (environment != null)
            {
                environment.LastConnectionTest = false;
                environment.LastConnectionTestDate = DateTime.UtcNow;
                environment.LastConnectionError = ex.Message;
                await _context.SaveChangesAsync();
            }

            return Ok(new ConnectionTestResult
            {
                Success = false,
                Message = $"Connection test failed: {ex.Message}",
                ErrorCode = "CONNECTION_ERROR",
                Details = new Dictionary<string, object>
                {
                    ["EnvironmentId"] = environmentId,
                    ["TestedAt"] = DateTime.UtcNow,
                    ["Error"] = ex.Message
                }
            });
        }
    }

    [HttpGet("oauth/progress/{progressId}")]
    [Authorize]
    public async Task<ActionResult<OAuthProgressResponse>> GetOAuthProgress(string progressId)
    {
        try
        {
            var progress = await _oauthService.GetOAuthProgressAsync(progressId);

            if (progress == null)
            {
                return NotFound(new { error = "Progress tracking not found or expired" });
            }

            return Ok(progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get OAuth progress for {ProgressId}", progressId);
            return StatusCode(500, "Failed to retrieve progress");
        }
    }

    [HttpGet("oauth-callback")]
    [AllowAnonymous] // OAuth callback doesn't include authorization header
    public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string state,
        [FromQuery] string? error = null, [FromQuery] string? error_description = null)
    {
        try
        {
            var callbackRequest = new OAuthCallbackRequest
            {
                Code = code ?? string.Empty,
                State = state ?? string.Empty,
                Error = error,
                ErrorDescription = error_description
            };

            var success = await _oauthService.HandleOAuthCallbackAsync(callbackRequest);

            if (success)
            {
                // Redirect to frontend success page
                var frontendUrl = _configuration["App:FrontendUrl"];
                return Redirect($"{frontendUrl}/oauth/success?state={state}");
            }
            else
            {
                // Redirect to frontend error page
                var frontendUrl = _configuration["App:FrontendUrl"];
                var errorMessage = !string.IsNullOrEmpty(error) ? error : "OAuth authentication failed";
                return Redirect($"{frontendUrl}/oauth/error?message={Uri.EscapeDataString(errorMessage)}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback failed");
            var frontendUrl = _configuration["App:FrontendUrl"];
            return Redirect($"{frontendUrl}/oauth/error?message={Uri.EscapeDataString("OAuth callback error")}");
        }
    }

    [HttpPost("{environmentId}/test-oauth")]
    [Authorize]
    public async Task<ActionResult<bool>> TestOAuthCredentials(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Get environment details to find client ID
            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            if (!environment.ClientId.HasValue)
            {
                return BadRequest("Environment does not have an associated client");
            }

            var isValid = await _oauthService.TestCredentialsAsync(environment.ClientId.Value, organizationId.Value);

            _logger.LogInformation("OAuth credentials test for environment {EnvironmentId}: {Result}",
                environmentId, isValid ? "Valid" : "Invalid");

            return Ok(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test OAuth credentials for environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Failed to test OAuth credentials");
        }
    }

    [HttpDelete("{environmentId}/oauth")]
    [Authorize]
    public async Task<IActionResult> RevokeOAuthCredentials(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify environment ownership
            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            if (!environment.ClientId.HasValue)
            {
                return BadRequest("Environment does not have an associated client");
            }

            var success = await _oauthService.RevokeCredentialsAsync(environment.ClientId.Value, organizationId.Value);

            if (success)
            {
                _logger.LogInformation("OAuth credentials revoked for environment {EnvironmentId} by user {UserId}",
                    environmentId, GetCurrentCustomerId());
                return NoContent();
            }
            else
            {
                return StatusCode(500, "Failed to revoke OAuth credentials");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke OAuth credentials for environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Failed to revoke OAuth credentials");
        }
    }

    [HttpPost("{environmentId}/oauth/refresh")]
    [Authorize]
    public async Task<ActionResult<bool>> RefreshOAuthTokens(Guid environmentId)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify environment ownership
            var environment = await _context.AzureEnvironments
                .Include(e => e.Client)
                .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);

            if (environment == null)
            {
                return NotFound("Environment not found");
            }

            if (!environment.ClientId.HasValue)
            {
                return BadRequest("Environment does not have an associated client");
            }

            var success = await _oauthService.RefreshTokensAsync(environment.ClientId.Value, organizationId.Value);

            _logger.LogInformation("OAuth token refresh for environment {EnvironmentId}: {Result}",
                environmentId, success ? "Success" : "Failed");

            return Ok(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh OAuth tokens for environment {EnvironmentId}", environmentId);
            return StatusCode(500, "Failed to refresh OAuth tokens");
        }
    }

    // Helper methods
    private Guid? GetOrganizationIdFromContext()
    {
        var orgIdClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgIdClaim, out var organizationId) ? organizationId : null;
    }

    private Guid? GetCurrentCustomerId()
    {
        var customerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(customerIdClaim, out var customerId) ? customerId : null;
    }
}

// DTOs
public class AzureEnvironmentDto
{
    public Guid AzureEnvironmentId { get; set; }
    public Guid? ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public List<string> SubscriptionIds { get; set; } = new();
    public string? ServicePrincipalId { get; set; }
    public string? ServicePrincipalName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastAccessDate { get; set; }
    public bool? LastConnectionTest { get; set; }
    public DateTime? LastConnectionTestDate { get; set; }
    public string? LastConnectionError { get; set; }

    // NEW: OAuth status fields
    public bool HasOAuthCredentials { get; set; }
    public string ConnectionMethod { get; set; } = "Unknown";
}
public class CreateAzureEnvironmentRequest
{
    [Required]
    public Guid ClientId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public List<string> SubscriptionIds { get; set; } = new();

    [StringLength(36)]
    public string? ServicePrincipalId { get; set; }

    [StringLength(100)]
    public string? ServicePrincipalName { get; set; }
}

public class UpdateAzureEnvironmentRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(36)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public List<string> SubscriptionIds { get; set; } = new();

    [StringLength(36)]
    public string? ServicePrincipalId { get; set; }

    [StringLength(100)]
    public string? ServicePrincipalName { get; set; }

    public bool IsActive { get; set; } = true;
}