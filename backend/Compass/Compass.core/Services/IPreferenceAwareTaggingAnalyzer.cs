using Compass.Core.Models;

namespace Compass.Core.Services;

/// <summary>
/// Interface for tagging analyzers that support client preferences
/// </summary>
public interface IPreferenceAwareTaggingAnalyzer
{
    Task<TaggingResults> AnalyzeTaggingAsync(
        List<AzureResource> resources,
        ClientAssessmentConfiguration? clientConfig = null,
        CancellationToken cancellationToken = default);
}