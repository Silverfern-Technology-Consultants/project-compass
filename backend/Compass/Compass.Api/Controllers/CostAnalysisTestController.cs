using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Compass.Core.Models;
using Compass.Core.Interfaces;
using Compass.Data.Interfaces;

namespace Compass.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CostAnalysisTestController : ControllerBase
{
    private readonly ICostAnalysisService _costAnalysisService;
    private readonly IClientRepository _clientRepository;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<CostAnalysisTestController> _logger;
    private readonly HttpClient _httpClient;

    public CostAnalysisTestController(
        ICostAnalysisService costAnalysisService,
        IClientRepository clientRepository,
        IOAuthService oauthService,
        ILogger<CostAnalysisTestController> logger,
        HttpClient httpClient)
    {
        _costAnalysisService = costAnalysisService;
        _clientRepository = clientRepository;
        _oauthService = oauthService;
        _logger = logger;
        _httpClient = httpClient;
    }

    [HttpPost("test-azure-api/{clientId}")]
    public async Task<ActionResult<object>> TestAzureApi(
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

            // Get client and subscription info
            var client = await _clientRepository.GetClientByIdAsync(clientId, organizationId.Value);
            if (client == null)
            {
                return NotFound($"Client {clientId} not found");
            }

            // Get subscription IDs
            var azureEnvironments = await _clientRepository.GetClientAzureEnvironmentsAsync(clientId, organizationId.Value);
            var subscriptionIds = azureEnvironments
                .SelectMany(env => env.SubscriptionIds ?? new List<string>())
                .ToList();

            if (!subscriptionIds.Any())
            {
                return BadRequest("No Azure subscriptions found for this client");
            }

            var testSubscriptionId = subscriptionId ?? subscriptionIds.First();

            // Test 1: Simple cost query with realistic dates
            var now = DateTime.UtcNow;
            var fromDate = now.AddDays(-30).ToString("yyyy-MM-dd");
            var toDate = now.AddDays(-1).ToString("yyyy-MM-dd");

            _logger.LogInformation("Testing Azure Cost Management API for subscription {SubscriptionId} from {FromDate} to {ToDate}", 
                testSubscriptionId, fromDate, toDate);

            // Create a simple test query
            var testQuery = new
            {
                type = "ActualCost",
                timeframe = new { from = fromDate, to = toDate },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new[]
                    {
                        new { name = "Cost", function = new { type = "Sum" } },
                        new { name = "PreTaxCost", function = new { type = "Sum" } }
                    },
                    grouping = new[]
                    {
                        new { type = "Dimension", name = "ResourceType" }
                    }
                }
            };

            var requestUrl = $"https://management.azure.com/subscriptions/{testSubscriptionId}/providers/Microsoft.CostManagement/query?api-version=2023-11-01";
            var requestContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(testQuery), 
                System.Text.Encoding.UTF8, 
                "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.PostAsync(requestUrl, requestContent, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation("Azure API Response: Status {StatusCode}, Content: {Content}", 
                response.StatusCode, responseContent);

            // Test 2: Try alternative query formats
            var alternativeResults = new List<object>();

            // Alternative 1: No grouping
            var simpleQuery = new
            {
                type = "ActualCost",
                timeframe = new { from = fromDate, to = toDate },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new[]
                    {
                        new { name = "Cost", function = new { type = "Sum" } }
                    }
                }
            };

            try
            {
                var simpleContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(simpleQuery), 
                    System.Text.Encoding.UTF8, 
                    "application/json");
                
                var simpleResponse = await _httpClient.PostAsync(requestUrl, simpleContent, cancellationToken);
                var simpleResponseContent = await simpleResponse.Content.ReadAsStringAsync(cancellationToken);
                
                alternativeResults.Add(new
                {
                    queryType = "Simple - No Grouping",
                    status = simpleResponse.StatusCode.ToString(),
                    response = simpleResponseContent.Length > 500 ? simpleResponseContent.Substring(0, 500) + "..." : simpleResponseContent
                });
            }
            catch (Exception ex)
            {
                alternativeResults.Add(new { queryType = "Simple - No Grouping", error = ex.Message });
            }

            // Alternative 2: Different time range
            var olderFromDate = now.AddDays(-90).ToString("yyyy-MM-dd");
            var olderToDate = now.AddDays(-60).ToString("yyyy-MM-dd");
            
            var olderQuery = new
            {
                type = "ActualCost",
                timeframe = new { from = olderFromDate, to = olderToDate },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new[]
                    {
                        new { name = "Cost", function = new { type = "Sum" } }
                    },
                    grouping = new[]
                    {
                        new { type = "Dimension", name = "ResourceType" }
                    }
                }
            };

            try
            {
                var olderContent = new StringContent(
                    System.Text.Json.JsonSerializer.Serialize(olderQuery), 
                    System.Text.Encoding.UTF8, 
                    "application/json");
                
                var olderResponse = await _httpClient.PostAsync(requestUrl, olderContent, cancellationToken);
                var olderResponseContent = await olderResponse.Content.ReadAsStringAsync(cancellationToken);
                
                alternativeResults.Add(new
                {
                    queryType = "Older Date Range",
                    dateRange = $"{olderFromDate} to {olderToDate}",
                    status = olderResponse.StatusCode.ToString(),
                    response = olderResponseContent.Length > 500 ? olderResponseContent.Substring(0, 500) + "..." : olderResponseContent
                });
            }
            catch (Exception ex)
            {
                alternativeResults.Add(new { queryType = "Older Date Range", error = ex.Message });
            }

            return Ok(new
            {
                message = "Azure Cost Management API Test Results",
                client = new { clientId = client.ClientId, name = client.Name },
                subscription = testSubscriptionId,
                testDateRange = new { from = fromDate, to = toDate },
                mainQuery = testQuery,
                mainResponse = new
                {
                    status = response.StatusCode.ToString(),
                    isSuccess = response.IsSuccessStatusCode,
                    content = responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent
                },
                alternativeTests = alternativeResults,
                recommendations = new[]
                {
                    "Check if the subscription has any actual costs in the date range",
                    "Verify Azure Cost Management is enabled for this subscription",
                    "Consider using a longer historical date range",
                    "Check if resources have been running long enough to generate cost data"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test Azure API call failed for client {ClientId}", clientId);
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.ToString() });
        }
    }

    private Guid? GetOrganizationIdFromContext()
    {
        var orgClaim = User.FindFirst("organization_id")?.Value;
        return Guid.TryParse(orgClaim, out var orgId) ? orgId : null;
    }
}
