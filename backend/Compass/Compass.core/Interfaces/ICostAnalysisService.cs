using Compass.Core.Models;

namespace Compass.Core.Interfaces;

public interface ICostAnalysisService
{
    Task<CostAnalysisResponse> AnalyzeCostTrendsAsync(CostAnalysisRequest request, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<CostAnalysisResponse> AnalyzeCostTrendsWithOAuthAsync(CostAnalysisRequest request, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
    
    // NEW: Azure Cost Management Query API method
    Task<CostAnalysisResponse> AnalyzeCostTrendsWithQueryAsync(CostAnalysisQueryRequest request, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}
