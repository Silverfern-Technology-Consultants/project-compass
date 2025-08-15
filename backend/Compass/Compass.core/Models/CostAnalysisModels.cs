using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Compass.Core.Models;

// NEW: Azure Cost Management Query API structure
public class AzureCostQuery
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Usage"; // Fixed as per requirements
    
    [JsonPropertyName("timeframe")]
    public string Timeframe { get; set; } = "Custom";
    
    [JsonPropertyName("timePeriod")]
    public AzureTimePeriod? TimePeriod { get; set; }
    
    [JsonPropertyName("dataset")]
    public AzureDataset Dataset { get; set; } = new();
}

public class AzureTimePeriod
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;
    
    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;
}

public class AzureDataset
{
    [JsonPropertyName("granularity")]
    public string Granularity { get; set; } = "None";
    
    [JsonPropertyName("aggregation")]
    public AzureAggregation Aggregation { get; set; } = new();
    
    [JsonPropertyName("grouping")]
    public List<AzureGrouping> Grouping { get; set; } = new();
    
    [JsonPropertyName("filter")]
    public AzureFilter? Filter { get; set; }
}

public class AzureAggregation
{
    [JsonPropertyName("totalCost")]
    public AzureCostAggregation TotalCost { get; set; } = new()
    {
        Name = "PreTaxCost",
        Function = "Sum"
    };
}

public class AzureCostAggregation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "PreTaxCost";
    
    [JsonPropertyName("function")]
    public string Function { get; set; } = "Sum";
}

public class AzureGrouping
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Dimension";
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class AzureFilter
{
    [JsonPropertyName("dimension")]
    public AzureDimensionFilter? Dimension { get; set; }
    
    [JsonPropertyName("tag")]
    public AzureTagFilter? Tag { get; set; }
    
    [JsonPropertyName("and")]
    public List<AzureFilter>? And { get; set; }
    
    [JsonPropertyName("or")]
    public List<AzureFilter>? Or { get; set; }
}

public class AzureDimensionFilter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "In";
    
    [JsonPropertyName("values")]
    public List<string> Values { get; set; } = new();
}

public class AzureTagFilter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("operator")]
    public string Operator { get; set; } = "In";
    
    [JsonPropertyName("values")]
    public List<string> Values { get; set; } = new();
}

// NEW: Cost Analysis Request with Azure Query
public class CostAnalysisQueryRequest
{
    [Required]
    public AzureCostQuery Query { get; set; } = new();
    
    public List<string> SubscriptionIds { get; set; } = new();
    
    public bool IncludePreviousPeriod { get; set; } = true; // NEW: Option to skip previous period comparison
}

// EXISTING: Legacy Cost Analysis Request (keeping for backward compatibility)
public class CostAnalysisRequest
{
    [Required]
    public List<string> SubscriptionIds { get; set; } = new();
    
    [Required]
    public CostTimeRange TimeRange { get; set; }
    
    [Required]
    public CostAggregation Aggregation { get; set; }
    
    [Required]
    public CostSortBy SortBy { get; set; }
    
    [Required]
    public SortDirection SortDirection { get; set; }
}

public class CostAnalysisResponse
{
    public CostTimeRange TimeRange { get; set; }
    public CostAggregation Aggregation { get; set; }
    public List<CostComparisonItem> Items { get; set; } = new();
    public CostSummary Summary { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public AzureCostQuery? OriginalQuery { get; set; } // NEW: Include original query for reference
}

public class CostComparisonItem
{
    public string Name { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string ResourceLocation { get; set; } = string.Empty; // NEW: Add resource location
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public decimal PreviousPeriodCost { get; set; }
    public decimal CurrentPeriodCost { get; set; }
    public decimal CostDifference { get; set; }
    public decimal PercentageChange { get; set; }
    public string Currency { get; set; } = "USD";
    public List<DailyCostData> DailyCosts { get; set; } = new();
    public Dictionary<string, string> GroupingValues { get; set; } = new(); // NEW: Store grouping dimension values
}

public class DailyCostData
{
    public DateTime Date { get; set; }
    public decimal Cost { get; set; }
    public string Currency { get; set; } = "USD";
}

public class CostSummary
{
    public decimal TotalPreviousPeriodCost { get; set; }
    public decimal TotalCurrentPeriodCost { get; set; }
    public decimal TotalCostDifference { get; set; }
    public decimal TotalPercentageChange { get; set; }
    public string Currency { get; set; } = "USD";
    public int ItemCount { get; set; }
    public string QuerySummary { get; set; } = string.Empty; // NEW: Human-readable query description
}

// EXISTING: Legacy enums (keeping for backward compatibility)
public enum CostTimeRange
{
    LastMonthToThisMonth,
    Last3Months,
    Last6Months,
    LastYearToThisYear,
    Custom
}

public enum CostAggregation
{
    None, // All individual resources
    ResourceType,
    ResourceGroup,
    Subscription,
    Daily
}

public enum CostSortBy
{
    Name,
    ResourceType,
    PreviousPeriodCost,
    CurrentPeriodCost,
    CostDifference,
    PercentageChange
}

public enum SortDirection
{
    Ascending,
    Descending
}

public class CustomTimeRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime ComparisonStartDate { get; set; }
    public DateTime ComparisonEndDate { get; set; }
}

// Azure Cost Management API response models
public class AzureCostQueryResult
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
    public AzureCostQueryProperties properties { get; set; } = new();
}

public class AzureCostQueryProperties
{
    public string? nextLink { get; set; }
    public List<AzureCostColumn> columns { get; set; } = new();
    public List<List<object>> rows { get; set; } = new();
}

public class AzureCostColumn
{
    public string name { get; set; } = string.Empty;
    public string type { get; set; } = string.Empty;
}

public class AzureCostQueryRequest
{
    public string type { get; set; } = "ActualCost";
    public AzureCostTimeframe timeframe { get; set; } = new();
    public AzureCostDataset dataset { get; set; } = new();
}

public class AzureCostTimeframe
{
    public string from { get; set; } = string.Empty;
    public string to { get; set; } = string.Empty;
}

public class AzureCostDataset
{
    public string granularity { get; set; } = "Daily";
    public List<AzureCostAggregation> aggregation { get; set; } = new();
    public List<AzureCostGrouping> grouping { get; set; } = new();
}

public class AzureCostFunction
{
    public string type { get; set; } = string.Empty;
}

public class AzureCostGrouping
{
    public string type { get; set; } = "Dimension";
    public string name { get; set; } = string.Empty;
}