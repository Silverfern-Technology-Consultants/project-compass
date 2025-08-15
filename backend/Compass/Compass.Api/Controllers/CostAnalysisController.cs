using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Compass.Core.Models;
using Compass.Core.Interfaces;
using Compass.Data.Interfaces;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CostAnalysisController : ControllerBase
{
    private readonly ICostAnalysisService _costAnalysisService;
    private readonly IClientRepository _clientRepository;
    private readonly IOAuthService _oauthService;
    private readonly IPermissionsService _permissionsService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CostAnalysisController> _logger;

    public CostAnalysisController(
        ICostAnalysisService costAnalysisService,
        IClientRepository clientRepository,
        IOAuthService oauthService,
        IPermissionsService permissionsService,
        HttpClient httpClient,
        ILogger<CostAnalysisController> logger)
    {
        _costAnalysisService = costAnalysisService;
        _clientRepository = clientRepository;
        _oauthService = oauthService;
        _permissionsService = permissionsService;
        _httpClient = httpClient;
        _logger = logger;
    }

    // NEW: Azure Cost Management Query API endpoint
    [HttpPost("clients/{clientId}/analyze-with-query")]
    public async Task<ActionResult<CostAnalysisResponse>> AnalyzeCostTrendsWithQuery(
        Guid clientId,
        [FromBody] CostAnalysisQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify client belongs to organization
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            // Check cost management permissions before allowing analysis
            var azureEnvironments = await _clientRepository.GetClientAzureEnvironmentsAsync(clientId, organizationId.Value);
            var environmentsNeedingSetup = new List<EnvironmentPermissionStatus>();
            
            foreach (var env in azureEnvironments)
            {
                var envStatus = await _permissionsService.GetEnvironmentPermissionStatusAsync(
                    env.AzureEnvironmentId, organizationId.Value);
                
                if (!envStatus.HasCostManagementAccess)
                {
                    environmentsNeedingSetup.Add(envStatus);
                }
            }

            // If any environments need setup, return setup instructions instead of running analysis
            if (environmentsNeedingSetup.Any())
            {
                return BadRequest(new CostAnalysisPermissionError
                {
                    Message = "Cost analysis requires additional permissions setup",
                    RequiresSetup = true,
                    EnvironmentsNeedingSetup = environmentsNeedingSetup
                });
            }

            // Get subscription IDs from client's Azure environments if not provided
            if (!request.SubscriptionIds.Any())
            {
                request.SubscriptionIds = azureEnvironments
                    .SelectMany(env => env.SubscriptionIds ?? new List<string>())
                    .ToList();

                if (!request.SubscriptionIds.Any())
                {
                    return BadRequest("No Azure subscriptions found for this client");
                }
            }

            var response = await _costAnalysisService.AnalyzeCostTrendsWithQueryAsync(
                request, clientId, organizationId.Value, cancellationToken);

            _logger.LogInformation("Cost analysis with query completed for client {ClientId} with {ItemCount} items",
                clientId, response.Items.Count);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for cost analysis query request for client {ClientId}", clientId);
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access for cost analysis query request for client {ClientId}", clientId);
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing cost trends with query for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    // EXISTING: Legacy cost analysis endpoint
    [HttpPost("clients/{clientId}/analyze")]
    public async Task<ActionResult<CostAnalysisResponse>> AnalyzeCostTrends(
        Guid clientId,
        [FromBody] CostAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify client belongs to organization
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            // NEW: Check cost management permissions before allowing analysis
            var azureEnvironments = await _clientRepository.GetClientAzureEnvironmentsAsync(clientId, organizationId.Value);
            var environmentsNeedingSetup = new List<EnvironmentPermissionStatus>();
            
            foreach (var env in azureEnvironments)
            {
                var envStatus = await _permissionsService.GetEnvironmentPermissionStatusAsync(
                    env.AzureEnvironmentId, organizationId.Value);
                
                if (!envStatus.HasCostManagementAccess)
                {
                    environmentsNeedingSetup.Add(envStatus);
                }
            }

            // If any environments need setup, return setup instructions instead of running analysis
            if (environmentsNeedingSetup.Any())
            {
                return BadRequest(new CostAnalysisPermissionError
                {
                    Message = "Cost analysis requires additional permissions setup",
                    RequiresSetup = true,
                    EnvironmentsNeedingSetup = environmentsNeedingSetup
                });
            }

            // Get subscription IDs from client's Azure environments if not provided
            if (!request.SubscriptionIds.Any())
            {
                request.SubscriptionIds = azureEnvironments
                    .SelectMany(env => env.SubscriptionIds ?? new List<string>())
                    .ToList();

                if (!request.SubscriptionIds.Any())
                {
                    return BadRequest("No Azure subscriptions found for this client");
                }
            }

            var response = await _costAnalysisService.AnalyzeCostTrendsWithOAuthAsync(
                request, clientId, organizationId.Value, cancellationToken);

            _logger.LogInformation("Cost analysis completed for client {ClientId} with {ItemCount} items",
                clientId, response.Items.Count);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for cost analysis request for client {ClientId}", clientId);
            return BadRequest(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access for cost analysis request for client {ClientId}", clientId);
            return Forbid(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing cost trends for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("clients/{clientId}/subscription-costs")]
    public async Task<ActionResult<List<SubscriptionCostSummary>>> GetSubscriptionCostSummary(
        Guid clientId,
        [FromQuery] CostTimeRange timeRange = CostTimeRange.LastMonthToThisMonth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify client belongs to organization
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            // Get Azure environments for the client
            var azureEnvironments = await _clientRepository.GetClientAzureEnvironmentsAsync(clientId, organizationId.Value);
            var subscriptionIds = azureEnvironments
                .SelectMany(env => env.SubscriptionIds ?? new List<string>())
                .ToList();

            if (!subscriptionIds.Any())
            {
                return Ok(new List<SubscriptionCostSummary>());
            }

            var request = new CostAnalysisRequest
            {
                SubscriptionIds = subscriptionIds,
                TimeRange = timeRange,
                Aggregation = CostAggregation.Subscription,
                SortBy = CostSortBy.CurrentPeriodCost,
                SortDirection = SortDirection.Descending
            };

            var response = await _costAnalysisService.AnalyzeCostTrendsWithOAuthAsync(
                request, clientId, organizationId.Value, cancellationToken);

            var subscriptionSummaries = response.Items.Select(item => new SubscriptionCostSummary
            {
                SubscriptionId = item.SubscriptionId,
                SubscriptionName = item.Name,
                CurrentPeriodCost = item.CurrentPeriodCost,
                PreviousPeriodCost = item.PreviousPeriodCost,
                CostDifference = item.CostDifference,
                PercentageChange = item.PercentageChange,
                Currency = item.Currency
            }).ToList();

            return Ok(subscriptionSummaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription cost summary for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("clients/{clientId}/debug-azure-api")]
    public async Task<ActionResult<object>> DebugAzureApi(
        Guid clientId,
        [FromQuery] string subscriptionId,
        [FromQuery] string fromDate = "",
        [FromQuery] string toDate = "",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify client belongs to organization
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            // Use default dates if not provided
            if (string.IsNullOrEmpty(fromDate))
                fromDate = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            if (string.IsNullOrEmpty(toDate))
                toDate = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

            _logger.LogInformation("Debug: Testing Azure Cost Management API for subscription {SubscriptionId} from {FromDate} to {ToDate}", 
                subscriptionId, fromDate, toDate);

            // Call the cost analysis service with debug logging
            var request = new CostAnalysisRequest
            {
                SubscriptionIds = new List<string> { subscriptionId },
                TimeRange = CostTimeRange.Last3Months,
                Aggregation = CostAggregation.ResourceType,
                SortBy = CostSortBy.CurrentPeriodCost,
                SortDirection = SortDirection.Descending
            };

            var response = await _costAnalysisService.AnalyzeCostTrendsWithOAuthAsync(
                request, clientId, organizationId.Value, cancellationToken);

            return Ok(new
            {
                message = "Debug API call completed",
                subscriptionId = subscriptionId,
                dateRange = new { from = fromDate, to = toDate },
                itemCount = response.Items.Count,
                summary = response.Summary,
                items = response.Items.Take(10), // First 10 items for debugging
                rawResponse = "Check logs for detailed API responses"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug API call failed for subscription {SubscriptionId}", subscriptionId);
            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
        }
    }

    // NEW: Cost Management setup and permission checking
    [HttpPost("clients/{clientId}/setup-cost-access")]
    public async Task<ActionResult<CostManagementSetupResponse>> SetupCostAccess(
        Guid clientId,
        [FromBody] CostAccessSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify client belongs to organization
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            // Test current permissions
            var permissions = await _oauthService.TestCostManagementPermissionsAsync(
                clientId, organizationId.Value, request.SubscriptionId);

            if (permissions.HasCostAccess)
            {
                return Ok(new CostManagementSetupResponse
                {
                    Status = "Ready",
                    Message = "Cost analysis is ready to use!",
                    HasCostAccess = true,
                    SubscriptionId = request.SubscriptionId
                });
            }

            // Generate setup instructions
            var instructions = await _oauthService.GenerateCostManagementSetupAsync(
                clientId, organizationId.Value, request.SubscriptionId);

            return Ok(new CostManagementSetupResponse
            {
                Status = "SetupRequired",
                Message = "Additional permissions needed for cost analysis",
                HasCostAccess = false,
                SubscriptionId = request.SubscriptionId,
                SetupInstructions = instructions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup cost access for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("clients/{clientId}/permission-matrix")]
    public async Task<ActionResult<AzurePermissionMatrix>> GetPermissionMatrix(
        Guid clientId,
        [FromQuery] string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Verify client belongs to organization
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            var matrix = await _oauthService.GetDetailedPermissionMatrixAsync(
                clientId, organizationId.Value, subscriptionId);

            return Ok(matrix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get permission matrix for client {ClientId}", clientId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("clients/{clientId}/debug-permissions")]
    public async Task<ActionResult<object>> DebugPermissions(
        Guid clientId,
        [FromQuery] string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var organizationId = GetOrganizationIdFromContext();
            if (organizationId == null)
            {
                return BadRequest("Organization context not found");
            }

            // Get OAuth credentials
            var credentials = await _oauthService.GetStoredCredentialsAsync(clientId, organizationId.Value);
            if (credentials == null)
            {
                return BadRequest($"No OAuth credentials found for client {clientId}");
            }

            // Get subscription info
            var azureEnvironments = await _clientRepository.GetClientAzureEnvironmentsAsync(clientId, organizationId.Value);
            var subscriptionIds = azureEnvironments
                .SelectMany(env => env.SubscriptionIds ?? new List<string>())
                .ToList();

            if (!subscriptionIds.Any())
            {
                return BadRequest("No Azure subscriptions found for this client");
            }

            var testSubscriptionId = subscriptionId ?? subscriptionIds.First();

            _logger.LogInformation("Testing Azure permissions for subscription {SubscriptionId}", testSubscriptionId);

            // Test 1: Basic subscription access
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var subscriptionUrl = $"https://management.azure.com/subscriptions/{testSubscriptionId}?api-version=2020-01-01";
            var subscriptionResponse = await _httpClient.GetAsync(subscriptionUrl, cancellationToken);
            var subscriptionContent = await subscriptionResponse.Content.ReadAsStringAsync(cancellationToken);

            // Test 2: Cost Management permissions
            var costPermissionUrl = $"https://management.azure.com/subscriptions/{testSubscriptionId}/providers/Microsoft.CostManagement?api-version=2023-11-01";
            var costPermissionResponse = await _httpClient.GetAsync(costPermissionUrl, cancellationToken);
            var costPermissionContent = await costPermissionResponse.Content.ReadAsStringAsync(cancellationToken);

            // Test 3: Billing permissions
            var billingUrl = $"https://management.azure.com/subscriptions/{testSubscriptionId}/providers/Microsoft.Billing?api-version=2020-05-01";
            var billingResponse = await _httpClient.GetAsync(billingUrl, cancellationToken);
            var billingContent = await billingResponse.Content.ReadAsStringAsync(cancellationToken);

            // Test 4: Consumption API permissions
            var consumptionUrl = $"https://management.azure.com/subscriptions/{testSubscriptionId}/providers/Microsoft.Consumption?api-version=2021-10-01";
            var consumptionResponse = await _httpClient.GetAsync(consumptionUrl, cancellationToken);
            var consumptionContent = await consumptionResponse.Content.ReadAsStringAsync(cancellationToken);

            return Ok(new
            {
                message = "Azure permissions test completed",
                subscriptionId = testSubscriptionId,
                tests = new
                {
                    subscriptionAccess = new
                    {
                        status = subscriptionResponse.StatusCode.ToString(),
                        success = subscriptionResponse.IsSuccessStatusCode,
                        response = subscriptionContent.Length > 500 ? subscriptionContent.Substring(0, 500) + "..." : subscriptionContent
                    },
                    costManagementPermissions = new
                    {
                        status = costPermissionResponse.StatusCode.ToString(),
                        success = costPermissionResponse.IsSuccessStatusCode,
                        response = costPermissionContent.Length > 500 ? costPermissionContent.Substring(0, 500) + "..." : costPermissionContent
                    },
                    billingPermissions = new
                    {
                        status = billingResponse.StatusCode.ToString(),
                        success = billingResponse.IsSuccessStatusCode,
                        response = billingContent.Length > 500 ? billingContent.Substring(0, 500) + "..." : billingContent
                    },
                    consumptionPermissions = new
                    {
                        status = consumptionResponse.StatusCode.ToString(),
                        success = consumptionResponse.IsSuccessStatusCode,
                        response = consumptionContent.Length > 500 ? consumptionContent.Substring(0, 500) + "..." : consumptionContent
                    }
                },
                analysis = new
                {
                    hasSubscriptionAccess = subscriptionResponse.IsSuccessStatusCode,
                    hasCostManagementAccess = costPermissionResponse.IsSuccessStatusCode,
                    hasBillingAccess = billingResponse.IsSuccessStatusCode,
                    hasConsumptionAccess = consumptionResponse.IsSuccessStatusCode,
                    recommendation = GetPermissionRecommendation(subscriptionResponse.IsSuccessStatusCode, costPermissionResponse.IsSuccessStatusCode, billingResponse.IsSuccessStatusCode, consumptionResponse.IsSuccessStatusCode)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug permissions test failed for client {ClientId}", clientId);
            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
        }
    }

    private string GetPermissionRecommendation(bool hasSubscription, bool hasCostManagement, bool hasBilling, bool hasConsumption)
    {
        if (!hasSubscription)
            return "❌ No subscription access - OAuth delegation may be incomplete";
        if (!hasCostManagement && !hasBilling && !hasConsumption)
            return "❌ No cost/billing permissions - need Cost Management Reader role";
        if (hasCostManagement)
            return "✅ Has Cost Management access - API should work";
        if (hasBilling)
            return "⚠️ Has Billing access but not Cost Management - try Billing APIs";
        if (hasConsumption)
            return "⚠️ Has Consumption access but not Cost Management - try Consumption APIs";
        return "❓ Unknown permission state";
    }

    private Guid? GetOrganizationIdFromContext()
    {
        var orgClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgClaim, out var orgId) ? orgId : null;
    }
}

public class SubscriptionCostSummary
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public decimal CurrentPeriodCost { get; set; }
    public decimal PreviousPeriodCost { get; set; }
    public decimal CostDifference { get; set; }
    public decimal PercentageChange { get; set; }
    public string Currency { get; set; } = "USD";
}

// NEW: Cost Management setup DTOs
public class CostAccessSetupRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
}

public class CostManagementSetupResponse
{
    public string Status { get; set; } = string.Empty; // "Ready" or "SetupRequired"
    public string Message { get; set; } = string.Empty;
    public bool HasCostAccess { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public CostManagementSetupInstructions? SetupInstructions { get; set; }
}

// NEW: Error response when cost analysis permissions are missing
public class CostAnalysisPermissionError
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresSetup { get; set; }
    public List<EnvironmentPermissionStatus> EnvironmentsNeedingSetup { get; set; } = new();
}
