using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Compass.Core.Models;
using Compass.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services;

public interface IAzureResourceGraphService
{
    Task<List<AzureResource>> GetResourcesAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);
    Task<List<AzureResource>> GetResourcesByTypeAsync(string[] subscriptionIds, string resourceType, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(string[] subscriptionIds, CancellationToken cancellationToken = default);

    // NEW: OAuth-enabled methods
    Task<List<AzureResource>> GetResourcesWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default);
}

public class AzureResourceGraphService : IAzureResourceGraphService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<AzureResourceGraphService> _logger;
    private readonly IOAuthService _oauthService;

    public AzureResourceGraphService(ILogger<AzureResourceGraphService> logger, IOAuthService oauthService)
    {
        _logger = logger;
        _oauthService = oauthService;

        // Use DefaultAzureCredential for fallback authentication
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            Diagnostics = { IsLoggingEnabled = true }
        });

        _armClient = new ArmClient(credential);
    }

    // Original methods using DefaultAzureCredential (for backward compatibility)
    public async Task<List<AzureResource>> GetResourcesAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        return await GetResourcesInternalAsync(_armClient, subscriptionIds, cancellationToken);
    }

    public async Task<List<AzureResource>> GetResourcesByTypeAsync(string[] subscriptionIds, string resourceType, CancellationToken cancellationToken = default)
    {
        return await GetResourcesByTypeInternalAsync(_armClient, subscriptionIds, resourceType, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        return await TestConnectionInternalAsync(_armClient, subscriptionIds, cancellationToken);
    }

    // NEW: OAuth-enabled methods
    public async Task<List<AzureResource>> GetResourcesWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var armClient = await CreateOAuthArmClientAsync(clientId, organizationId);
        if (armClient == null)
        {
            _logger.LogWarning("Failed to create OAuth ARM client for client {ClientId}, falling back to default credentials", clientId);
            return await GetResourcesAsync(subscriptionIds, cancellationToken);
        }

        return await GetResourcesInternalAsync(armClient, subscriptionIds, cancellationToken);
    }

    public async Task<bool> TestConnectionWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        var armClient = await CreateOAuthArmClientAsync(clientId, organizationId);
        if (armClient == null)
        {
            _logger.LogWarning("Failed to create OAuth ARM client for client {ClientId}, falling back to default credentials", clientId);
            return await TestConnectionAsync(subscriptionIds, cancellationToken);
        }

        return await TestConnectionInternalAsync(armClient, subscriptionIds, cancellationToken);
    }

    private async Task<ArmClient?> CreateOAuthArmClientAsync(Guid clientId, Guid organizationId)
    {
        try
        {
            var credentials = await _oauthService.GetStoredCredentialsAsync(clientId, organizationId);
            if (credentials == null)
            {
                _logger.LogWarning("No OAuth credentials found for client {ClientId}", clientId);
                return null;
            }

            // Create a token credential from the stored access token
            var tokenCredential = new OAuthTokenCredential(credentials.AccessToken);

            _logger.LogInformation("Created OAuth ARM client for client {ClientId} with token expiring at {ExpiresAt}",
                clientId, credentials.ExpiresAt);

            return new ArmClient(tokenCredential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OAuth ARM client for client {ClientId}", clientId);
            return null;
        }
    }

    private async Task<List<AzureResource>> GetResourcesInternalAsync(ArmClient armClient, string[] subscriptionIds, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Retrieving Azure resources for {SubscriptionCount} subscriptions", subscriptionIds.Length);

            var query = @"
                Resources
                | where type !in~ (
                    'microsoft.resources/deployments',
                    'microsoft.resources/deploymentscripts',
                    'microsoft.resources/templatespecs',
                    'microsoft.alertsmanagement/actionrules',
                    'microsoft.security/assessments'
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
                | order by type asc, name asc
                | limit 1000";

            return await ExecuteQueryAsync(armClient, query, subscriptionIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Azure resources for subscriptions: {Subscriptions}", string.Join(",", subscriptionIds));
            throw;
        }
    }

    private async Task<List<AzureResource>> GetResourcesByTypeInternalAsync(ArmClient armClient, string[] subscriptionIds, string resourceType, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Retrieving Azure resources of type {ResourceType} for {SubscriptionCount} subscriptions",
                resourceType, subscriptionIds.Length);

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
                | order by name asc
                | limit 500";

            return await ExecuteQueryAsync(armClient, query, subscriptionIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Azure resources of type {ResourceType} for subscriptions: {Subscriptions}",
                resourceType, string.Join(",", subscriptionIds));
            throw;
        }
    }

    private async Task<bool> TestConnectionInternalAsync(ArmClient armClient, string[] subscriptionIds, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Testing Azure Resource Graph connection for {SubscriptionCount} subscriptions", subscriptionIds.Length);

            // Simple test query to verify connectivity and permissions
            var query = "Resources | limit 1 | project id, name, type";
            var results = await ExecuteQueryAsync(armClient, query, subscriptionIds, cancellationToken);

            _logger.LogInformation("Successfully connected to Azure Resource Graph. Found {ResultCount} test results", results.Count);
            return true;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 403)
        {
            _logger.LogWarning("Access denied to Azure Resource Graph. Check permissions for subscriptions: {Subscriptions}. Error: {Error}",
                string.Join(",", subscriptionIds), ex.Message);
            return false;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 401)
        {
            _logger.LogWarning("Authentication failed for Azure Resource Graph. Check credentials. Error: {Error}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Azure Resource Graph for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            return false;
        }
    }

    private async Task<List<AzureResource>> ExecuteQueryAsync(ArmClient armClient, string query, string[] subscriptionIds, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing Azure Resource Graph query: {Query}", query);

        // Get the tenant resource to access Resource Graph
        var tenant = armClient.GetTenants().First();

        // Create the query request
        var content = new ResourceQueryContent(query);

        // Add subscription filters
        foreach (var subscriptionId in subscriptionIds)
        {
            content.Subscriptions.Add(subscriptionId);
        }

        // Set query options
        content.Options = new ResourceQueryRequestOptions
        {
            ResultFormat = ResultFormat.ObjectArray,
            Top = 1000 // Limit results to prevent large responses
        };

        try
        {
            // Execute the query
            var response = await tenant.GetResourcesAsync(content, cancellationToken);
            var resources = new List<AzureResource>();

            // Parse the results from BinaryData
            if (response.Value.Data != null)
            {
                var jsonString = response.Value.Data.ToString();
                var dataElement = JsonDocument.Parse(jsonString).RootElement;

                if (dataElement.ValueKind == JsonValueKind.Array)
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
            }

            _logger.LogInformation("Retrieved {ResourceCount} resources from Azure Resource Graph", resources.Count);

            // Log some statistics
            if (resources.Any())
            {
                var resourceTypes = resources.GroupBy(r => r.Type).Take(5);
                _logger.LogDebug("Top resource types: {ResourceTypes}",
                    string.Join(", ", resourceTypes.Select(g => $"{g.Key}: {g.Count()}")));
            }

            return resources;
        }
        catch (Azure.RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Resource Graph query failed. Status: {Status}, Error: {Error}",
                ex.Status, ex.Message);
            throw;
        }
    }

    private static AzureResource? ParseResourceFromJson(JsonElement element)
    {
        // Ensure required properties exist
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
        };

        // Parse optional properties
        if (element.TryGetProperty("resourceGroup", out var rgElement))
            resource.ResourceGroup = rgElement.GetString();

        if (element.TryGetProperty("location", out var locElement))
            resource.Location = locElement.GetString();

        if (element.TryGetProperty("subscriptionId", out var subElement))
            resource.SubscriptionId = subElement.GetString();

        if (element.TryGetProperty("kind", out var kindElement))
            resource.Kind = kindElement.GetString();

        // Parse tags
        resource.Tags = ParseTags(element);

        // Parse properties and sku as JSON strings (for potential future use)
        if (element.TryGetProperty("properties", out var propsElement))
            resource.Properties = propsElement.GetRawText();

        if (element.TryGetProperty("sku", out var skuElement))
            resource.Sku = skuElement.GetRawText();

        return resource;
    }

    private static Dictionary<string, string> ParseTags(JsonElement element)
    {
        var tags = new Dictionary<string, string>();

        if (element.TryGetProperty("tags", out var tagsElement) &&
            tagsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var tag in tagsElement.EnumerateObject())
            {
                if (tag.Value.ValueKind == JsonValueKind.String)
                {
                    var tagValue = tag.Value.GetString();
                    if (!string.IsNullOrEmpty(tagValue))
                    {
                        tags[tag.Name] = tagValue;
                    }
                }
                else if (tag.Value.ValueKind == JsonValueKind.Null)
                {
                    // Handle null tag values
                    tags[tag.Name] = string.Empty;
                }
            }
        }

        return tags;
    }
}

// Custom token credential for OAuth access tokens
public class OAuthTokenCredential : TokenCredential
{
    private readonly string _accessToken;

    public OAuthTokenCredential(string accessToken)
    {
        _accessToken = accessToken;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken(_accessToken, DateTimeOffset.UtcNow.AddHours(1));
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new ValueTask<AccessToken>(new AccessToken(_accessToken, DateTimeOffset.UtcNow.AddHours(1)));
    }
}