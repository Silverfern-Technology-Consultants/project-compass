using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Compass.Core.Models;
using Compass.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text;

namespace Compass.Core.Services;

public class CostAnalysisService : ICostAnalysisService
{
    private readonly ILogger<CostAnalysisService> _logger;
    private readonly IOAuthService _oauthService;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private static readonly SemaphoreSlim _rateLimitSemaphore = new(maxCount: 2, initialCount: 2);
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly TimeSpan _minimumRequestInterval = TimeSpan.FromSeconds(2);

    public CostAnalysisService(ILogger<CostAnalysisService> logger, IOAuthService oauthService, HttpClient httpClient, IMemoryCache cache)
    {
        _logger = logger;
        _oauthService = oauthService;
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<CostAnalysisResponse> AnalyzeCostTrendsAsync(CostAnalysisRequest request, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await AnalyzeCostTrendsWithOAuthAsync(request, clientId, organizationId, cancellationToken);
    }

    // NEW: Azure Cost Management Query API implementation with period comparison
    public async Task<CostAnalysisResponse> AnalyzeCostTrendsWithQueryAsync(CostAnalysisQueryRequest request, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting cost trend analysis with query for client {ClientId}", clientId);

            var credentials = await _oauthService.GetStoredCredentialsAsync(clientId, organizationId);
            if (credentials == null)
            {
                throw new InvalidOperationException($"No OAuth credentials found for client {clientId}");
            }

            var response = new CostAnalysisResponse
            {
                TimeRange = CostTimeRange.Custom,
                Aggregation = DetermineAggregationFromQuery(request.Query),
                GeneratedAt = DateTime.UtcNow,
                OriginalQuery = request.Query
            };

            // Get current period data
            var currentPeriodData = new List<AzureCostData>();
            foreach (var subscriptionId in request.SubscriptionIds)
            {
                try
                {
                    var costData = await QueryCostManagementWithCustomQueryAsync(
                        subscriptionId, request.Query, credentials.AccessToken, cancellationToken);
                    currentPeriodData.AddRange(costData);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get current period cost data for subscription {SubscriptionId}", subscriptionId);
                }
            }

            // Calculate previous period and get comparison data (only if requested)
            var previousPeriodData = new List<AzureCostData>();
            if (request.IncludePreviousPeriod && request.Query.TimePeriod != null)
            {
                var previousPeriodQuery = CreatePreviousPeriodQuery(request.Query);
                
                foreach (var subscriptionId in request.SubscriptionIds)
                {
                    try
                    {
                        var costData = await QueryCostManagementWithCustomQueryAsync(
                            subscriptionId, previousPeriodQuery, credentials.AccessToken, cancellationToken);
                        previousPeriodData.AddRange(costData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get previous period cost data for subscription {SubscriptionId}", subscriptionId);
                    }
                }
            }

            response.Items = ConvertToComparisonItemsWithPeriods(currentPeriodData, previousPeriodData, request.Query);
            response.Summary = CalculateQuerySummaryWithComparison(response.Items, request.Query);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing cost trends with query for client {ClientId}", clientId);
            throw;
        }
    }

    // EXISTING: Legacy implementation for backward compatibility
    public async Task<CostAnalysisResponse> AnalyzeCostTrendsWithOAuthAsync(CostAnalysisRequest request, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting legacy cost trend analysis for client {ClientId}", clientId);

            var credentials = await _oauthService.GetStoredCredentialsAsync(clientId, organizationId);
            if (credentials == null)
            {
                throw new InvalidOperationException($"No OAuth credentials found for client {clientId}");
            }

            var timePeriods = CalculateTimePeriods(request.TimeRange);
            var response = new CostAnalysisResponse
            {
                TimeRange = request.TimeRange,
                Aggregation = request.Aggregation,
                GeneratedAt = DateTime.UtcNow
            };

            var currentTask = GetLegacyCostDataAsync(request.SubscriptionIds, timePeriods.current.From, timePeriods.current.To, request.Aggregation, credentials.AccessToken, cancellationToken);
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            var previousTask = GetLegacyCostDataAsync(request.SubscriptionIds, timePeriods.previous.From, timePeriods.previous.To, request.Aggregation, credentials.AccessToken, cancellationToken);

            var currentCosts = await currentTask;
            var previousCosts = await previousTask;

            response.Items = CompareCostData(currentCosts, previousCosts, request.Aggregation);
            response.Items = SortCostData(response.Items, request.SortBy, request.SortDirection);
            response.Summary = CalculateSummary(response.Items);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing cost trends for client {ClientId}", clientId);
            throw;
        }
    }

    private async Task<List<AzureCostData>> QueryCostManagementWithCustomQueryAsync(string subscriptionId, AzureCostQuery query, string accessToken, CancellationToken cancellationToken)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _minimumRequestInterval)
            {
                await Task.Delay(_minimumRequestInterval - timeSinceLastRequest, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;

            var requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2023-11-01";

            var requestBody = new
            {
                type = query.Type,
                timeframe = query.Timeframe,
                timePeriod = query.TimePeriod != null ? new
                {
                    from = query.TimePeriod.From,
                    to = query.TimePeriod.To
                } : null,
                dataset = new
                {
                    granularity = query.Dataset.Granularity,
                    aggregation = new
                    {
                        totalCost = new
                        {
                            name = query.Dataset.Aggregation.TotalCost.Name,
                            function = query.Dataset.Aggregation.TotalCost.Function
                        }
                    },
                    grouping = query.Dataset.Grouping?.Select(g => new
                    {
                        type = g.Type,
                        name = g.Name
                    }).ToArray() ?? new object[0]
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.PostAsync(requestUrl, requestContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Azure Cost Management API failed for subscription {SubscriptionId}: {StatusCode} - {Error}", 
                    subscriptionId, response.StatusCode, errorContent);
                return new List<AzureCostData>();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var queryResult = JsonSerializer.Deserialize<AzureCostQueryResult>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            return ParseCostData(queryResult, subscriptionId);
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private CostAggregation DetermineAggregationFromQuery(AzureCostQuery query)
    {
        if (query.Dataset.Grouping == null || !query.Dataset.Grouping.Any())
            return CostAggregation.None;

        var firstGrouping = query.Dataset.Grouping.First().Name;
        return firstGrouping switch
        {
            "ResourceType" => CostAggregation.ResourceType,
            "ResourceGroup" or "ResourceGroupName" => CostAggregation.ResourceGroup,
            "SubscriptionId" or "SubscriptionName" => CostAggregation.Subscription,
            _ when query.Dataset.Granularity == "Daily" => CostAggregation.Daily,
            _ => CostAggregation.None
        };
    }

    private List<CostComparisonItem> ConvertToComparisonItems(List<AzureCostData> costData, AzureCostQuery query)
    {
        return ConvertToComparisonItemsWithPeriods(costData, new List<AzureCostData>(), query);
    }

    private List<CostComparisonItem> ConvertToComparisonItemsWithPeriods(List<AzureCostData> currentPeriodData, List<AzureCostData> previousPeriodData, AzureCostQuery query)
    {
        var items = new List<CostComparisonItem>();
        var currentGroupedData = GroupCostDataByQuery(currentPeriodData, query);
        var previousGroupedData = GroupCostDataByQuery(previousPeriodData, query);
        
        // Get all unique keys from both periods
        var allKeys = currentGroupedData.Keys.Union(previousGroupedData.Keys).ToHashSet();

        foreach (var key in allKeys)
        {
            var currentGroup = currentGroupedData.ContainsKey(key) ? currentGroupedData[key] : new List<AzureCostData>();
            var previousGroup = previousGroupedData.ContainsKey(key) ? previousGroupedData[key] : new List<AzureCostData>();
            
            var currentCost = currentGroup.Sum(c => c.Cost);
            var previousCost = previousGroup.Sum(c => c.Cost);
            var costDifference = currentCost - previousCost;
            
            // FIXED: Better percentage calculation for meaningful display
            decimal percentageChange;
            if (previousCost == 0 && currentCost > 0)
            {
                percentageChange = -999; // Special marker for "N/A" (creation, not change)
            }
            else if (previousCost == 0 && currentCost == 0)
            {
                percentageChange = 0; // Both are zero
            }
            else if (previousCost == 0)
            {
                percentageChange = -100; // Special marker for "Ended" (went to zero)
            }
            else
            {
                // Standard percentage change formula: ((V2 - V1) / V1) * 100
                var calculation = ((currentCost - previousCost) / previousCost) * 100;
                _logger.LogInformation("PERCENTAGE DEBUG: currentCost={CurrentCost}, previousCost={PreviousCost}, calculation={Calculation}", 
                    currentCost, previousCost, calculation);
                percentageChange = calculation;
            }

            // Use first available item for metadata
            var firstItem = currentGroup.FirstOrDefault() ?? previousGroup.FirstOrDefault();
            if (firstItem == null) continue;

            var item = new CostComparisonItem
            {
                Name = GetDisplayName(key, firstItem, query),
                ResourceType = firstItem.ResourceType ?? "Unknown",
                ResourceGroup = firstItem.ResourceGroup ?? string.Empty,
                ResourceLocation = firstItem.ResourceLocation ?? string.Empty,
                SubscriptionId = firstItem.SubscriptionId,
                SubscriptionName = firstItem.SubscriptionName ?? string.Empty,
                CurrentPeriodCost = currentCost,
                PreviousPeriodCost = previousCost,
                CostDifference = costDifference,
                PercentageChange = percentageChange,
                Currency = firstItem.Currency
            };

            if (query.Dataset.Granularity == "Daily")
            {
                // CRITICAL FIX: Generate proper daily costs based on the query time period
                item.DailyCosts = GenerateDailyCostsFromQueryPeriod(currentGroup, query);
            }

            if (query.Dataset.Grouping != null)
            {
                foreach (var grouping in query.Dataset.Grouping)
                {
                    var value = grouping.Name switch
                    {
                        "ResourceType" => firstItem.ResourceType,
                        "ResourceGroup" or "ResourceGroupName" => firstItem.ResourceGroup,
                        "SubscriptionId" => firstItem.SubscriptionId,
                        "SubscriptionName" => firstItem.SubscriptionName,
                        "ResourceId" => firstItem.ResourceId,
                        "ServiceName" => firstItem.Name, // Use the display name for service
                        "ResourceLocation" => firstItem.ResourceLocation,
                        _ => key
                    };
                    item.GroupingValues[grouping.Name] = value ?? string.Empty;
                }
            }

            items.Add(item);
        }

        return items.OrderByDescending(i => i.CurrentPeriodCost).ToList();
    }
    
    private List<DailyCostData> GenerateDailyCostsFromQueryPeriod(List<AzureCostData> costData, AzureCostQuery query)
    {
        var dailyCosts = new List<DailyCostData>();
        
        if (query.TimePeriod == null || costData.Count == 0)
        {
            return dailyCosts;
        }
        
        _logger.LogInformation("TIMEZONE DEBUG: Raw query period - From: '{From}', To: '{To}'", 
            query.TimePeriod.From, query.TimePeriod.To);
        
        // FIXED: Parse as UTC dates explicitly to prevent timezone shifting
        DateTime fromDate, toDate;
        if (!DateTime.TryParse(query.TimePeriod.From, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out fromDate) || 
            !DateTime.TryParse(query.TimePeriod.To, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out toDate))
        {
            _logger.LogWarning("Failed to parse query time period: {From} to {To}", query.TimePeriod.From, query.TimePeriod.To);
            return dailyCosts;
        }
        
        _logger.LogInformation("TIMEZONE DEBUG: Parsed UTC dates - FromDate: {FromDate} (Kind: {FromKind}), ToDate: {ToDate} (Kind: {ToKind})", 
            fromDate, fromDate.Kind, toDate, toDate.Kind);
        
        // Extract just the date parts - frontend sends full datetime ranges like "2025-07-01T00:00:00Z" to "2025-07-31T23:59:59Z"
        // We want the date range from July 1 to July 31 (inclusive)
        var startDate = fromDate.Date;
        var endDate = toDate.Date;
        
        _logger.LogInformation("TIMEZONE DEBUG: Final date range - StartDate: {StartDate}, EndDate: {EndDate}", 
            startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));
        
        _logger.LogInformation("Generating daily costs for period {From} to {To} with {DataPoints} cost data points", 
            startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), costData.Count);
        
        // Get total cost for this resource
        var totalCost = costData.Sum(c => c.Cost);
        var daysInPeriod = (int)(endDate - startDate).TotalDays + 1;
        
        // Create a dictionary of actual cost data by date
        var costDataByDate = costData
            .GroupBy(c => c.Date.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cost));
        
        _logger.LogInformation("Cost data available for {UniqueDates} unique dates: {Dates}", 
            costDataByDate.Count, string.Join(", ", costDataByDate.Keys.Select(d => d.ToString("yyyy-MM-dd"))));
        
        // Generate daily cost entries for the requested period
        for (int i = 0; i < daysInPeriod; i++)
        {
            var currentDate = startDate.AddDays(i);
            var cost = costDataByDate.ContainsKey(currentDate) ? costDataByDate[currentDate] : 0m;
            
            dailyCosts.Add(new DailyCostData
            {
                Date = DateTime.SpecifyKind(currentDate, DateTimeKind.Utc), // Store as UTC for API consistency
                Cost = cost,
                Currency = costData.FirstOrDefault()?.Currency ?? "USD"
            });
        }
        
        _logger.LogInformation("Generated {DailyCount} daily cost entries for resource from {StartDate} to {EndDate}", 
            dailyCosts.Count, 
            dailyCosts.FirstOrDefault()?.Date.ToString("yyyy-MM-dd"), 
            dailyCosts.LastOrDefault()?.Date.ToString("yyyy-MM-dd"));
        return dailyCosts;
    }

    private Dictionary<string, List<AzureCostData>> GroupCostDataByQuery(List<AzureCostData> costData, AzureCostQuery query)
    {
        if (query.Dataset.Grouping == null || !query.Dataset.Grouping.Any())
        {
            return costData.Select((c, i) => new { Key = c.Name ?? c.ResourceId ?? $"Item_{i}", Value = c })
                          .GroupBy(x => x.Key)
                          .ToDictionary(g => g.Key, g => g.Select(x => x.Value).ToList());
        }

        var primaryGrouping = query.Dataset.Grouping.First().Name;
        
        return primaryGrouping switch
        {
            "ResourceType" => costData.GroupBy(c => c.ResourceType ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList()),
            "ResourceGroup" or "ResourceGroupName" => costData.GroupBy(c => c.ResourceGroup ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList()),
            "SubscriptionId" => costData.GroupBy(c => c.SubscriptionId).ToDictionary(g => g.Key, g => g.ToList()),
            "ResourceId" => costData.GroupBy(c => c.ResourceId ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList()),
            _ => costData.GroupBy(c => c.Name ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList())
        };
    }

    private CostSummary CalculateQuerySummary(List<CostComparisonItem> items, AzureCostQuery query)
    {
        return CalculateQuerySummaryWithComparison(items, query);
    }

    private CostSummary CalculateQuerySummaryWithComparison(List<CostComparisonItem> items, AzureCostQuery query)
    {
        if (!items.Any())
        {
            return new CostSummary { QuerySummary = GenerateQuerySummaryText(query) };
        }

        var totalCurrentCost = items.Sum(i => i.CurrentPeriodCost);
        var totalPreviousCost = items.Sum(i => i.PreviousPeriodCost);
        var totalDifference = totalCurrentCost - totalPreviousCost;
        
        // FIXED: Better percentage calculation for summary
        decimal totalPercentageChange;
        if (totalPreviousCost == 0 && totalCurrentCost > 0)
        {
            totalPercentageChange = -999; // Special marker for "N/A" (creation, not change)
        }
        else if (totalPreviousCost == 0 && totalCurrentCost == 0)
        {
            totalPercentageChange = 0; // Both are zero
        }
        else if (totalPreviousCost == 0)
        {
            totalPercentageChange = -100; // Special marker for "Ended" (went to zero)
        }
        else
        {
            // Standard percentage change formula: ((V2 - V1) / V1) * 100
            totalPercentageChange = ((totalCurrentCost - totalPreviousCost) / totalPreviousCost) * 100;
        }

        return new CostSummary
        {
            TotalCurrentPeriodCost = totalCurrentCost,
            TotalPreviousPeriodCost = totalPreviousCost,
            TotalCostDifference = totalDifference,
            TotalPercentageChange = totalPercentageChange,
            Currency = items.First().Currency,
            ItemCount = items.Count,
            QuerySummary = GenerateQuerySummaryText(query)
        };
    }

    private string GenerateQuerySummaryText(AzureCostQuery query)
    {
        var summary = $"Cost analysis using {query.Type} data";
        
        if (query.TimePeriod != null)
        {
            summary += $" from {query.TimePeriod.From} to {query.TimePeriod.To}";
        }
        
        if (query.Dataset.Grouping != null && query.Dataset.Grouping.Any())
        {
            var groupings = string.Join(", ", query.Dataset.Grouping.Select(g => g.Name));
            summary += $" grouped by {groupings}";
        }
        
        summary += $" with {query.Dataset.Granularity} granularity";
        
        return summary;
    }

    private AzureCostQuery CreatePreviousPeriodQuery(AzureCostQuery originalQuery)
    {
        if (originalQuery.TimePeriod == null)
            return originalQuery;

        // Parse dates using UTC to avoid timezone issues
        var fromDate = DateTime.Parse(originalQuery.TimePeriod.From, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        var toDate = DateTime.Parse(originalQuery.TimePeriod.To, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        
        // FIXED: Use proper month arithmetic instead of day counting
        // Check if this is a full month period (starts on 1st and ends on last day of month)
        var isFullMonth = fromDate.Day == 1 && toDate.Day == DateTime.DaysInMonth(toDate.Year, toDate.Month);
        
        DateTime previousFromDate, previousToDate;
        
        if (isFullMonth)
        {
            // For full month periods (e.g., July 1-31), get the previous full month (June 1-30)
            var previousMonth = fromDate.AddMonths(-1);
            previousFromDate = new DateTime(previousMonth.Year, previousMonth.Month, 1);
            previousToDate = new DateTime(previousMonth.Year, previousMonth.Month, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));
        }
        else
        {
            // For partial periods, subtract the same number of days
            var periodLength = (toDate - fromDate).TotalDays;
            previousToDate = fromDate.AddDays(-1);
            previousFromDate = previousToDate.AddDays(-periodLength);
        }
        
        // Log for debugging
        _logger.LogInformation("BACKEND PERIOD DEBUG: Original period: {OriginalFrom} to {OriginalTo} (IsFullMonth: {IsFullMonth})", 
            fromDate.ToString("yyyy-MM-dd"), toDate.ToString("yyyy-MM-dd"), isFullMonth);
        _logger.LogInformation("BACKEND PERIOD DEBUG: Previous period: {PreviousFrom} to {PreviousTo}", 
            previousFromDate.ToString("yyyy-MM-dd"), previousToDate.ToString("yyyy-MM-dd"));

        var previousQuery = new AzureCostQuery
        {
            Type = originalQuery.Type,
            Timeframe = originalQuery.Timeframe,
            TimePeriod = new AzureTimePeriod
            {
                From = previousFromDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                To = previousToDate.ToString("yyyy-MM-ddTHH:mm:ssZ")
            },
            Dataset = originalQuery.Dataset
        };

        return previousQuery;
    }

    private string GetDisplayName(string key, AzureCostData firstItem, AzureCostQuery query)
    {
        // If we have a meaningful name from the item, use it
        if (!string.IsNullOrEmpty(firstItem.Name) && firstItem.Name != "Unknown")
            return firstItem.Name;

        // Otherwise, use the grouping key or fall back to resource type
        if (!string.IsNullOrEmpty(key) && key != "Unknown")
            return key;

        // Last resort: use resource type or Unknown
        return firstItem.ResourceType ?? "Unknown";
    }

    private string GetResourceDisplayName(AzureCostData item, string serviceName)
    {
        // Priority order for display name:
        // 1. Extract resource name from ResourceId
        // 2. Use ServiceName if available
        // 3. Use ResourceType
        // 4. Fall back to "Unknown"
        
        if (!string.IsNullOrEmpty(item.ResourceId))
        {
            var resourceName = ExtractResourceNameFromId(item.ResourceId);
            if (!string.IsNullOrEmpty(resourceName))
                return resourceName;
        }
        
        if (!string.IsNullOrEmpty(serviceName) && serviceName != "Unknown")
            return serviceName;
            
        if (!string.IsNullOrEmpty(item.ResourceType) && item.ResourceType != "Unknown")
            return item.ResourceType;
            
        return "Unknown";
    }
    
    private string ExtractResourceNameFromId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return string.Empty;
            
        // Azure Resource ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0)
        {
            // Return the last segment which should be the resource name
            return segments[segments.Length - 1];
        }
        
        return string.Empty;
    }
    
    private string ExtractResourceTypeFromId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return "Unknown";
            
        // Azure Resource ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        
        // Find the providers segment
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("providers", StringComparison.OrdinalIgnoreCase) && i + 2 < segments.Length)
            {
                // Return the provider and resource type (e.g., "microsoft.sql/servers" or "microsoft.keyvault/vaults")
                var provider = segments[i + 1];
                var resourceType = segments[i + 2];
                
                // Convert to friendly name
                return ConvertToFriendlyResourceType(provider, resourceType);
            }
        }
        
        return "Unknown";
    }
    
    private string ConvertToFriendlyResourceType(string provider, string resourceType)
    {
        var fullType = $"{provider}/{resourceType}".ToLowerInvariant();
        
        return fullType switch
        {
            "microsoft.sql/servers" => "SQL Server",
            "microsoft.sql/servers/databases" => "SQL Database",
            "microsoft.keyvault/vaults" => "Key Vault",
            "microsoft.storage/storageaccounts" => "Storage Account",
            "microsoft.operationalinsights/workspaces" => "Log Analytics Workspace",
            "microsoft.web/sites" => "App Service",
            "microsoft.appconfiguration/configurationstores" => "App Configuration",
            "microsoft.compute/virtualmachines" => "Virtual Machine",
            "microsoft.network/virtualnetworks" => "Virtual Network",
            "microsoft.network/publicipaddresses" => "Public IP Address",
            "microsoft.network/loadbalancers" => "Load Balancer",
            "microsoft.containerregistry/registries" => "Container Registry",
            "microsoft.containerservice/managedclusters" => "AKS Cluster",
            "microsoft.cognitiveservices/accounts" => "Cognitive Services",
            "microsoft.insights/components" => "Application Insights",
            _ => $"{provider}/{resourceType}"
        };
    }

    // Legacy methods for backward compatibility (simplified versions)
    private async Task<List<AzureCostData>> GetLegacyCostDataAsync(List<string> subscriptionIds, string fromDate, string toDate, CostAggregation aggregation, string accessToken, CancellationToken cancellationToken)
    {
        var allCostData = new List<AzureCostData>();

        foreach (var subscriptionId in subscriptionIds)
        {
            try
            {
                var cacheKey = $"cost_data_{subscriptionId}_{fromDate}_{toDate}_{aggregation}";
                
                if (_cache.TryGetValue(cacheKey, out List<AzureCostData>? cachedData) && cachedData != null)
                {
                    allCostData.AddRange(cachedData);
                    continue;
                }
                
                var costData = await QueryLegacyCostManagementApiAsync(subscriptionId, fromDate, toDate, aggregation, accessToken, cancellationToken);
                
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                    SlidingExpiration = TimeSpan.FromMinutes(30),
                    Priority = CacheItemPriority.Normal
                };
                _cache.Set(cacheKey, costData, cacheOptions);
                
                allCostData.AddRange(costData);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cost data for subscription {SubscriptionId}", subscriptionId);
            }
        }

        return allCostData;
    }

    private async Task<List<AzureCostData>> QueryLegacyCostManagementApiAsync(string subscriptionId, string fromDate, string toDate, CostAggregation aggregation, string accessToken, CancellationToken cancellationToken)
    {
        await _rateLimitSemaphore.WaitAsync(cancellationToken);
        try
        {
            var requestUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2023-11-01";

            var queryRequest = new
            {
                type = "ActualCost",
                timeframe = new { from = fromDate, to = toDate },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new[]
                    {
                        new { name = "Cost", function = new { type = "Sum" } }
                    },
                    grouping = aggregation == CostAggregation.ResourceType 
                        ? new[] { new { type = "Dimension", name = "ResourceType" } }
                        : new object[0]
                }
            };
            
            var requestContent = new StringContent(JsonSerializer.Serialize(queryRequest), Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var response = await _httpClient.PostAsync(requestUrl, requestContent, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new List<AzureCostData>();
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var queryResult = JsonSerializer.Deserialize<AzureCostQueryResult>(responseContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            
            return ParseCostData(queryResult, subscriptionId);
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private List<AzureCostData> ParseCostData(AzureCostQueryResult? queryResult, string subscriptionId)
    {
        var costData = new List<AzureCostData>();

        if (queryResult?.properties?.rows == null || !queryResult.properties.rows.Any())
        {
            _logger.LogInformation("No cost data rows returned for subscription {SubscriptionId}", subscriptionId);
            return costData;
        }

        var columns = queryResult.properties.columns;
        var rows = queryResult.properties.rows;

        if (columns == null || !columns.Any())
        {
            _logger.LogWarning("No columns found in cost data response for subscription {SubscriptionId}", subscriptionId);
            return costData;
        }

        _logger.LogInformation("Parsing {RowCount} cost data rows with {ColumnCount} columns for subscription {SubscriptionId}", 
            rows.Count, columns.Count, subscriptionId);
        
        // Log column names for debugging
        _logger.LogInformation("Available columns: {Columns}", string.Join(", ", columns.Select(c => c.name)));
        
        // IMPORTANT: Log first few rows to debug the actual data structure
        for (int i = 0; i < Math.Min(rows.Count, 3); i++)
        {
            var row = rows[i];
            var rowData = new List<string>();
            for (int j = 0; j < Math.Min(row.Count, columns.Count); j++)
            {
                rowData.Add($"{columns[j].name}={row[j]}");
            }
            _logger.LogInformation("Sample row {RowIndex}: {RowData}", i, string.Join(", ", rowData));
        }

        var costColumnIndex = columns.FindIndex(c => 
            c.name.Equals("Cost", StringComparison.OrdinalIgnoreCase) ||
            c.name.Equals("CostInBillingCurrency", StringComparison.OrdinalIgnoreCase) ||
            c.name.Equals("PreTaxCost", StringComparison.OrdinalIgnoreCase));
            
        var resourceTypeColumnIndex = columns.FindIndex(c => c.name.Equals("ResourceType", StringComparison.OrdinalIgnoreCase));
        var resourceIdColumnIndex = columns.FindIndex(c => c.name.Equals("ResourceId", StringComparison.OrdinalIgnoreCase));
        var resourceGroupColumnIndex = columns.FindIndex(c => c.name.Equals("ResourceGroup", StringComparison.OrdinalIgnoreCase) || c.name.Equals("ResourceGroupName", StringComparison.OrdinalIgnoreCase));
        var serviceNameColumnIndex = columns.FindIndex(c => c.name.Equals("ServiceName", StringComparison.OrdinalIgnoreCase));
        var resourceLocationColumnIndex = columns.FindIndex(c => c.name.Equals("ResourceLocation", StringComparison.OrdinalIgnoreCase));
        var subscriptionNameColumnIndex = columns.FindIndex(c => c.name.Equals("SubscriptionName", StringComparison.OrdinalIgnoreCase));
        
        // Look for date columns - Azure Cost Management API can return different date column names
        var usageDateColumnIndex = columns.FindIndex(c => 
            c.name.Equals("UsageDate", StringComparison.OrdinalIgnoreCase) ||
            c.name.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
            c.name.Equals("BillingPeriod", StringComparison.OrdinalIgnoreCase));

        _logger.LogInformation("Column indices - Cost: {Cost}, ResourceType: {ResourceType}, ResourceId: {ResourceId}, ResourceGroup: {ResourceGroup}, ServiceName: {ServiceName}, ResourceLocation: {ResourceLocation}, SubscriptionName: {SubscriptionName}, UsageDate: {UsageDate}",
            costColumnIndex, resourceTypeColumnIndex, resourceIdColumnIndex, resourceGroupColumnIndex, serviceNameColumnIndex, resourceLocationColumnIndex, subscriptionNameColumnIndex, usageDateColumnIndex);

        foreach (var row in rows)
        {
            try
            {
                if (row == null)
                {
                    _logger.LogWarning("Null row encountered for subscription {SubscriptionId}", subscriptionId);
                    continue;
                }
                
                _logger.LogDebug("Processing row with {ColumnCount} values for subscription {SubscriptionId}", row.Count, subscriptionId);

                var item = new AzureCostData
                {
                    SubscriptionId = subscriptionId,
                    Date = DateTime.UtcNow, // Default, will be overridden if UsageDate is found
                    Currency = "USD"
                };

                // Safely get cost value
                if (costColumnIndex >= 0 && costColumnIndex < row.Count && row[costColumnIndex] != null)
                {
                    if (decimal.TryParse(row[costColumnIndex]?.ToString(), out var costValue))
                    {
                        item.Cost = costValue;
                    }
                }

                // Safely get resource type
                if (resourceTypeColumnIndex >= 0 && resourceTypeColumnIndex < row.Count)
                {
                    item.ResourceType = row[resourceTypeColumnIndex]?.ToString() ?? "Unknown";
                }
                else
                {
                    // Extract resource type from ResourceId if ResourceType column not available
                    if (!string.IsNullOrEmpty(item.ResourceId))
                    {
                        item.ResourceType = ExtractResourceTypeFromId(item.ResourceId);
                    }
                    else
                    {
                        item.ResourceType = "Unknown";
                    }
                }

                // Safely get resource ID
                if (resourceIdColumnIndex >= 0 && resourceIdColumnIndex < row.Count)
                {
                    item.ResourceId = row[resourceIdColumnIndex]?.ToString() ?? string.Empty;
                }

                // Safely get resource group
                if (resourceGroupColumnIndex >= 0 && resourceGroupColumnIndex < row.Count)
                {
                    item.ResourceGroup = row[resourceGroupColumnIndex]?.ToString() ?? string.Empty;
                }

                // Safely get resource location
                if (resourceLocationColumnIndex >= 0 && resourceLocationColumnIndex < row.Count)
                {
                    item.ResourceLocation = row[resourceLocationColumnIndex]?.ToString() ?? string.Empty;
                }

                // Safely get subscription name
                if (subscriptionNameColumnIndex >= 0 && subscriptionNameColumnIndex < row.Count)
                {
                    item.SubscriptionName = row[subscriptionNameColumnIndex]?.ToString() ?? string.Empty;
                }

                // Safely get usage date - this is critical for daily granularity
                if (usageDateColumnIndex >= 0 && usageDateColumnIndex < row.Count)
                {
                    var dateValue = row[usageDateColumnIndex]?.ToString();
                    if (!string.IsNullOrEmpty(dateValue))
                    {
                        // Handle different date formats from Azure Cost Management API
                        DateTime usageDate;
                        if (dateValue.Length == 8 && dateValue.All(char.IsDigit)) // Format: 20250701
                        {
                            if (DateTime.TryParseExact(dateValue, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out usageDate))
                            {
                                // Azure returns dates in compact format as local dates - treat as UTC
                                item.Date = DateTime.SpecifyKind(usageDate, DateTimeKind.Utc);
                                _logger.LogDebug("Parsed compact date format: {DateValue} -> {ParsedDate} for subscription {SubscriptionId}", 
                                    dateValue, item.Date.ToString("yyyy-MM-dd"), subscriptionId);
                            }
                            else
                            {
                                _logger.LogWarning("Failed to parse compact date format: {DateValue} for subscription {SubscriptionId}", 
                                    dateValue, subscriptionId);
                            }
                        }
                        else if (DateTime.TryParse(dateValue, out usageDate)) // Standard ISO format
                        {
                            // Convert to UTC if not already
                            if (usageDate.Kind == DateTimeKind.Unspecified)
                            {
                                item.Date = DateTime.SpecifyKind(usageDate, DateTimeKind.Utc);
                            }
                            else
                            {
                                item.Date = usageDate.ToUniversalTime();
                            }
                            _logger.LogDebug("Parsed standard date format: {DateValue} -> {ParsedDate} for subscription {SubscriptionId}", 
                                dateValue, item.Date.ToString("yyyy-MM-dd"), subscriptionId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to parse usage date: {DateValue} for subscription {SubscriptionId}", 
                                dateValue, subscriptionId);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No usage date column found (index: {Index}) for subscription {SubscriptionId}. Available columns: {Columns}", 
                        usageDateColumnIndex, subscriptionId, string.Join(", ", columns.Select(c => c.name)));
                }

                // Set a meaningful name based on available data
                var serviceName = serviceNameColumnIndex >= 0 && serviceNameColumnIndex < row.Count ? row[serviceNameColumnIndex]?.ToString() : null;
                item.Name = GetResourceDisplayName(item, serviceName ?? string.Empty);

                if (item.Cost > 0 || !string.IsNullOrEmpty(item.ResourceType))
                {
                    costData.Add(item);
                    _logger.LogDebug("Added cost item: {Name} - {Cost} {Currency}", item.Name, item.Cost, item.Currency);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse cost data row for subscription {SubscriptionId}. Row has {ColumnCount} values.", 
                    subscriptionId, row?.Count ?? 0);
            }
        }

        _logger.LogInformation("Parsed {ItemCount} cost items for subscription {SubscriptionId}", costData.Count, subscriptionId);
        return costData;
    }

    private (TimePeriod current, TimePeriod previous) CalculateTimePeriods(CostTimeRange timeRange)
    {
        var now = DateTime.UtcNow;
        
        return timeRange switch
        {
            CostTimeRange.LastMonthToThisMonth => (
                current: new TimePeriod
                {
                    From = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd"),
                    To = now.AddDays(-1).ToString("yyyy-MM-dd")
                },
                previous: new TimePeriod
                {
                    From = new DateTime(now.Year, now.Month, 1).AddMonths(-1).ToString("yyyy-MM-dd"),
                    To = new DateTime(now.Year, now.Month, 1).AddDays(-1).ToString("yyyy-MM-dd")
                }
            ),
            _ => throw new ArgumentException($"Unsupported time range: {timeRange}")
        };
    }

    private List<CostComparisonItem> CompareCostData(List<AzureCostData> currentCosts, List<AzureCostData> previousCosts, CostAggregation aggregation)
    {
        var comparisonItems = new List<CostComparisonItem>();
        var currentGroups = GroupCostData(currentCosts, aggregation);
        var previousGroups = GroupCostData(previousCosts, aggregation);
        var allKeys = currentGroups.Keys.Union(previousGroups.Keys).ToList();

        foreach (var key in allKeys)
        {
            var currentCost = currentGroups.ContainsKey(key) ? currentGroups[key].Sum(c => c.Cost) : 0;
            var previousCost = previousGroups.ContainsKey(key) ? previousGroups[key].Sum(c => c.Cost) : 0;
            var costDifference = currentCost - previousCost;
            
            // FIXED: Better percentage calculation for meaningful display
            decimal percentageChange;
            if (previousCost == 0 && currentCost > 0)
            {
                percentageChange = -999; // Special marker for "N/A" (creation, not change)
            }
            else if (previousCost == 0 && currentCost == 0)
            {
                percentageChange = 0; // Both are zero
            }
            else if (previousCost == 0)
            {
                percentageChange = -100; // Special marker for "Ended" (went to zero)
            }
            else
            {
                // Standard percentage change formula: ((V2 - V1) / V1) * 100
                percentageChange = ((currentCost - previousCost) / previousCost) * 100;
            }

            var firstItem = currentGroups.ContainsKey(key) ? currentGroups[key].First() : previousGroups[key].First();

            comparisonItems.Add(new CostComparisonItem
            {
                Name = key,
                ResourceType = firstItem.ResourceType ?? string.Empty,
                ResourceGroup = firstItem.ResourceGroup ?? string.Empty,
                SubscriptionId = firstItem.SubscriptionId,
                CurrentPeriodCost = currentCost,
                PreviousPeriodCost = previousCost,
                CostDifference = costDifference,
                PercentageChange = percentageChange,
                Currency = firstItem.Currency
            });
        }

        return comparisonItems;
    }

    private Dictionary<string, List<AzureCostData>> GroupCostData(List<AzureCostData> costData, CostAggregation aggregation)
    {
        return aggregation switch
        {
            CostAggregation.ResourceType => costData.GroupBy(c => c.ResourceType ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList()),
            CostAggregation.ResourceGroup => costData.GroupBy(c => c.ResourceGroup ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList()),
            CostAggregation.Subscription => costData.GroupBy(c => c.SubscriptionId).ToDictionary(g => g.Key, g => g.ToList()),
            _ => costData.GroupBy(c => c.Name ?? "Unknown").ToDictionary(g => g.Key, g => g.ToList())
        };
    }

    private List<CostComparisonItem> SortCostData(List<CostComparisonItem> items, CostSortBy sortBy, SortDirection direction)
    {
        return sortBy switch
        {
            CostSortBy.CurrentPeriodCost => direction == SortDirection.Ascending 
                ? items.OrderBy(i => i.CurrentPeriodCost).ToList()
                : items.OrderByDescending(i => i.CurrentPeriodCost).ToList(),
            _ => items.OrderBy(i => i.Name).ToList()
        };
    }

    private CostSummary CalculateSummary(List<CostComparisonItem> items)
    {
        if (!items.Any())
        {
            return new CostSummary();
        }

        var totalCurrent = items.Sum(i => i.CurrentPeriodCost);
        var totalPrevious = items.Sum(i => i.PreviousPeriodCost);
        var totalDifference = totalCurrent - totalPrevious;
        
        // FIXED: Better percentage calculation for legacy summary
        decimal totalPercentageChange;
        if (totalPrevious == 0 && totalCurrent > 0)
        {
            totalPercentageChange = -999; // Special marker for "N/A" (creation, not change)
        }
        else if (totalPrevious == 0 && totalCurrent == 0)
        {
            totalPercentageChange = 0; // Both are zero
        }
        else if (totalPrevious == 0)
        {
            totalPercentageChange = -100; // Special marker for "Ended" (went to zero)
        }
        else
        {
            // Standard percentage change formula: ((V2 - V1) / V1) * 100
            totalPercentageChange = ((totalCurrent - totalPrevious) / totalPrevious) * 100;
        }

        return new CostSummary
        {
            TotalCurrentPeriodCost = totalCurrent,
            TotalPreviousPeriodCost = totalPrevious,
            TotalCostDifference = totalDifference,
            TotalPercentageChange = totalPercentageChange,
            Currency = items.First().Currency,
            ItemCount = items.Count
        };
    }
}

// Helper classes
public class AzureCostData
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ResourceLocation { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";
}

public class TimePeriod
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
}