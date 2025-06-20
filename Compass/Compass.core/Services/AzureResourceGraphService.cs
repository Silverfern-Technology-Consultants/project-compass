using Compass.Core.Models;
using Microsoft.Extensions.Logging;

namespace Compass.Core.Services;

public interface IAzureResourceGraphService
{
    Task<List<AzureResource>> GetResourcesAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<List<AzureResource>> GetResourcesByTypeAsync(string[] subscriptionIds, string resourceType, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
}

public class AzureResourceGraphService : IAzureResourceGraphService
{
    private readonly ILogger<AzureResourceGraphService> _logger;

    public AzureResourceGraphService(ILogger<AzureResourceGraphService> logger)
    {
        _logger = logger;
    }

    public async Task<List<AzureResource>> GetResourcesAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Azure Resource Graph integration
        _logger.LogWarning("Azure Resource Graph service not yet implemented");
        await Task.Delay(100, cancellationToken); // Simulate async work
        return new List<AzureResource>();
    }

    public async Task<List<AzureResource>> GetResourcesByTypeAsync(string[] subscriptionIds, string resourceType, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Azure Resource Graph integration
        _logger.LogWarning("Azure Resource Graph service not yet implemented");
        await Task.Delay(100, cancellationToken);
        return new List<AzureResource>();
    }

    public async Task<bool> TestConnectionAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        // TODO: Implement Azure Resource Graph integration
        _logger.LogWarning("Azure Resource Graph service not yet implemented");
        await Task.Delay(100, cancellationToken);
        return false; // Always return false for now
    }
}
/*using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services;

public interface IAzureResourceGraphService
{
    Task<List<AzureResource>> GetResourcesAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<List<AzureResource>> GetResourcesByTypeAsync(string[] subscriptionIds, string resourceType, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
}

public class AzureResourceGraphService : IAzureResourceGraphService
{
    private readonly ResourceGraphClient _resourceGraphClient;
    private readonly ILogger<AzureResourceGraphService> _logger;

    public AzureResourceGraphService(ILogger<AzureResourceGraphService> logger)
    {
        _logger = logger;

        // Use DefaultAzureCredential for authentication
        var credential = new DefaultAzureCredential();
        _resourceGraphClient = new ResourceGraphClient(credential);
    }

    public async Task<List<AzureResource>> GetResourcesAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = @"
                Resources
                | where type !in~ (
                    'microsoft.resources/deployments',
                    'microsoft.resources/deploymentscripts',
                    'microsoft.resources/templatespecs'
                )
                | project 
                    id, 
                    name, 
                    type, 
                    resourceGroup, 
                    location, 
                    subscriptionId, 
                    tags,
                    properties,
                    kind,
                    sku
                | order by type asc, name asc";

            return await ExecuteQueryAsync(query, subscriptionIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Azure resources for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<List<AzureResource>> GetResourcesByTypeAsync(string[] subscriptionIds, string resourceType, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = $@"
                Resources
                | where type =~ '{resourceType}'
                | project 
                    id, 
                    name, 
                    type, 
                    resourceGroup, 
                    location, 
                    subscriptionId, 
                    tags,
                    properties,
                    kind,
                    sku
                | order by name asc";

            return await ExecuteQueryAsync(query, subscriptionIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Azure resources of type {ResourceType} for subscriptions: {Subscriptions}",
                resourceType, string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = "Resources | limit 1 | project id";
            var results = await ExecuteQueryAsync(query, subscriptionIds, cancellationToken);

            _logger.LogInformation("Successfully connected to Azure Resource Graph for {SubscriptionCount} subscriptions", subscriptionIds.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Azure Resource Graph for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));
            return false;
        }
    }

    private async Task<List<AzureResource>> ExecuteQueryAsync(string query, string[] subscriptionIds, CancellationToken cancellationToken)
    {
        var request = new ResourceQueryContent(query)
        {
            Subscriptions = { subscriptionIds },
            Options = new ResourceQueryRequestOptions
            {
                ResultFormat = ResultFormat.ObjectArray
            }
        };

        var response = await _resourceGraphClient.ResourcesAsync(request, cancellationToken);
        var resources = new List<AzureResource>();

        if (response.Value.Data is JsonElement dataElement && dataElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataElement.EnumerateArray())
            {
                try
                {
                    var resource = ParseResourceFromJson(item);
                    if (resource != null)
                    {
                        resources.Add(resource);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse resource from query result: {Json}", item.GetRawText());
                }
            }
        }

        _logger.LogInformation("Retrieved {Count} resources from Azure Resource Graph", resources.Count);
        return resources;
    }

    private static AzureResource? ParseResourceFromJson(JsonElement element)
    {
        if (!element.TryGetProperty("id", out var idElement) ||
            !element.TryGetProperty("name", out var nameElement) ||
            !element.TryGetProperty("type", out var typeElement))
        {
            return null;
        }

        var resource = new AzureResource
        {
            Id = idElement.GetString() ?? string.Empty,
            Name = nameElement.GetString() ?? string.Empty,
            Type = typeElement.GetString() ?? string.Empty,
            ResourceGroup = element.TryGetProperty("resourceGroup", out var rgElement) ? rgElement.GetString() : null,
            Location = element.TryGetProperty("location", out var locElement) ? locElement.GetString() : null,
            SubscriptionId = element.TryGetProperty("subscriptionId", out var subElement) ? subElement.GetString() : null,
            Kind = element.TryGetProperty("kind", out var kindElement) ? kindElement.GetString() : null,
            Tags = ParseTags(element),
            Properties = element.TryGetProperty("properties", out var propsElement) ? propsElement.GetRawText() : null,
            Sku = element.TryGetProperty("sku", out var skuElement) ? skuElement.GetRawText() : null
        };

        return resource;
    }

    private static Dictionary<string, string> ParseTags(JsonElement element)
    {
        var tags = new Dictionary<string, string>();

        if (element.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var tag in tagsElement.EnumerateObject())
            {
                if (tag.Value.ValueKind == JsonValueKind.String)
                {
                    tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                }
            }
        }

        return tags;
    }
}*/