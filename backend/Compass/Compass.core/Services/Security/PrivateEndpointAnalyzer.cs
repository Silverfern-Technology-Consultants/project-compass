using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.Security;

public interface IPrivateEndpointAnalyzer
{
    Task<List<SecurityFinding>> AnalyzePrivateEndpointCoverageAsync(
        List<AzureResource> allResources,
        CancellationToken cancellationToken = default);
}

public class PrivateEndpointAnalyzer : IPrivateEndpointAnalyzer
{
    private readonly ILogger<PrivateEndpointAnalyzer> _logger;

    public PrivateEndpointAnalyzer(ILogger<PrivateEndpointAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<List<SecurityFinding>> AnalyzePrivateEndpointCoverageAsync(
        List<AzureResource> allResources,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing Private Endpoint coverage across Azure services");

        var findings = new List<SecurityFinding>();

        try
        {
            // Resources that should have private endpoints
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).ToList();
            var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();
            var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers")).ToList();
            var cosmosDbAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.documentdb/databaseaccounts").ToList();
            var serviceBusNamespaces = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.servicebus/namespaces").ToList();
            var eventHubNamespaces = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.eventhub/namespaces").ToList();
            var containerRegistries = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.containerregistry/registries").ToList();
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();

            // Get existing private endpoints
            var privateEndpoints = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/privateendpoints").ToList();

            // Analyze private endpoint coverage
            var totalCriticalResources = keyVaults.Count + storageAccounts.Count + sqlServers.Count +
                cosmosDbAccounts.Count + containerRegistries.Count + appServices.Count;
            var privateEndpointCoverage = (decimal)privateEndpoints.Count / Math.Max(totalCriticalResources, 1) * 100;

            // Overall coverage assessment
            await AnalyzeOverallCoverageAsync(privateEndpointCoverage, privateEndpoints.Count, totalCriticalResources, findings);

            // Analyze specific resource types without private endpoints
            await AnalyzeResourceTypePrivateEndpoints(keyVaults, "Key Vault", findings);
            await AnalyzeResourceTypePrivateEndpoints(storageAccounts, "Storage Account", findings);
            await AnalyzeResourceTypePrivateEndpoints(sqlServers, "SQL Server", findings);
            await AnalyzeResourceTypePrivateEndpoints(cosmosDbAccounts, "Cosmos DB", findings);
            await AnalyzeResourceTypePrivateEndpoints(containerRegistries, "Container Registry", findings);
            await AnalyzeResourceTypePrivateEndpoints(appServices, "App Service", findings);
            await AnalyzeResourceTypePrivateEndpoints(serviceBusNamespaces, "Service Bus", findings);
            await AnalyzeResourceTypePrivateEndpoints(eventHubNamespaces, "Event Hub", findings);

            // Check for orphaned private endpoints
            await AnalyzeOrphanedPrivateEndpoints(privateEndpoints, findings);

            // Zero Trust architecture assessment
            await AnalyzeZeroTrustReadiness(allResources, findings);

            _logger.LogInformation("Private endpoint analysis completed. Coverage: {Coverage}%, Private endpoints: {Count}",
                privateEndpointCoverage, privateEndpoints.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze private endpoint coverage");

            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "error.private.endpoints",
                ResourceName = "Private Endpoint Analysis",
                SecurityControl = "Network Security Assessment",
                Issue = "Failed to analyze private endpoint coverage",
                Recommendation = "Review permissions and retry private endpoint analysis",
                Severity = "Medium",
                ComplianceFramework = "General"
            });
        }

        return findings;
    }

    private async Task AnalyzeOverallCoverageAsync(decimal coverage, int endpointCount, int totalResources, List<SecurityFinding> findings)
    {
        if (coverage < 50m && totalResources > 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "private.endpoint.coverage",
                ResourceName = "Private Endpoint Coverage",
                SecurityControl = "Zero Trust Networking",
                Issue = $"Low private endpoint coverage ({coverage:F1}%) for critical Azure services. Found {endpointCount} private endpoints for {totalResources} critical resources",
                Recommendation = "Implement private endpoints for Key Vaults, Storage Accounts, SQL Servers, Cosmos DB, and Container Registries to enable zero-trust network access",
                Severity = coverage < 25m ? "High" : "Medium",
                ComplianceFramework = "Zero Trust Architecture"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeResourceTypePrivateEndpoints(List<AzureResource> resources, string resourceType, List<SecurityFinding> findings)
    {
        foreach (var resource in resources.Take(10)) // Limit to avoid performance issues
        {
            var hasPrivateEndpoint = await CheckForPrivateEndpoint(resource);

            if (!hasPrivateEndpoint)
            {
                var severity = DeterminePrivateEndpointSeverity(resource, resourceType);

                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    SecurityControl = "Private Connectivity",
                    Issue = $"{resourceType} '{resource.Name}' is not configured with private endpoint connectivity",
                    Recommendation = GetPrivateEndpointRecommendation(resourceType),
                    Severity = severity,
                    ComplianceFramework = "Zero Trust Architecture"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task<bool> CheckForPrivateEndpoint(AzureResource resource)
    {
        if (string.IsNullOrEmpty(resource.Properties))
            return false;

        try
        {
            var properties = JsonDocument.Parse(resource.Properties);
            if (properties.RootElement.TryGetProperty("privateEndpointConnections", out var connections))
            {
                return connections.GetArrayLength() > 0;
            }

            // Check for publicNetworkAccess property (some services use this)
            if (properties.RootElement.TryGetProperty("publicNetworkAccess", out var publicAccess))
            {
                var accessValue = publicAccess.GetString()?.ToLowerInvariant();
                return accessValue == "disabled"; // Implies private endpoint usage
            }
        }
        catch (JsonException)
        {
            // Properties parsing failed, assume no private endpoint
        }

        await Task.CompletedTask;
        return false;
    }

    private string DeterminePrivateEndpointSeverity(AzureResource resource, string resourceType)
    {
        // Higher severity for production resources
        if (resource.Environment?.ToLowerInvariant().Contains("prod") == true ||
            resource.Tags.ContainsKey("Environment") && resource.Tags["Environment"].ToLowerInvariant().Contains("prod"))
        {
            return "High";
        }

        // Higher severity for certain critical resource types
        if (resourceType == "Key Vault" || resourceType == "SQL Server")
        {
            return "High";
        }

        // Medium severity for storage and other critical services
        if (resourceType == "Storage Account" || resourceType == "Container Registry")
        {
            return "Medium";
        }

        return "Low";
    }

    private string GetPrivateEndpointRecommendation(string resourceType)
    {
        return resourceType switch
        {
            "Key Vault" => "Configure private endpoint for Key Vault to enable secure access to secrets, keys, and certificates without internet exposure",
            "Storage Account" => "Configure private endpoint for Storage Account to secure blob, file, table, and queue access through private network connectivity",
            "SQL Server" => "Configure private endpoint for SQL Server to enable secure database connectivity without public internet access",
            "Cosmos DB" => "Configure private endpoint for Cosmos DB to secure database access and prevent data exfiltration through public endpoints",
            "Container Registry" => "Configure private endpoint for Container Registry to secure container image access and prevent unauthorized image pulls",
            "App Service" => "Configure private endpoint for App Service to enable secure application access and protect against internet-based attacks",
            "Service Bus" => "Configure private endpoint for Service Bus to secure message queuing and prevent unauthorized access to messaging infrastructure",
            "Event Hub" => "Configure private endpoint for Event Hub to secure event streaming and protect against data interception",
            _ => $"Configure private endpoint for {resourceType} to enable secure, private connectivity and disable public network access where possible"
        };
    }

    private async Task AnalyzeOrphanedPrivateEndpoints(List<AzureResource> privateEndpoints, List<SecurityFinding> findings)
    {
        foreach (var pe in privateEndpoints)
        {
            if (!string.IsNullOrEmpty(pe.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(pe.Properties);
                    if (properties.RootElement.TryGetProperty("connectionState", out var connectionState))
                    {
                        if (connectionState.TryGetProperty("status", out var status))
                        {
                            var statusValue = status.GetString()?.ToLowerInvariant();
                            if (statusValue == "disconnected" || statusValue == "rejected")
                            {
                                findings.Add(new SecurityFinding
                                {
                                    Category = "Network",
                                    ResourceId = pe.Id,
                                    ResourceName = pe.Name,
                                    SecurityControl = "Resource Management",
                                    Issue = $"Private endpoint '{pe.Name}' is in {statusValue} state and may be orphaned",
                                    Recommendation = "Review and clean up disconnected or rejected private endpoints to reduce management overhead and potential security confusion",
                                    Severity = "Low",
                                    ComplianceFramework = "Resource Management Best Practice"
                                });
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Continue if properties cannot be parsed
                }
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeZeroTrustReadiness(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        // Zero Trust architecture readiness assessment
        var privateEndpoints = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/privateendpoints").Count();
        var publicIPs = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses").Count();
        var criticalServices = allResources.Where(r =>
            r.Type.ToLowerInvariant().Contains("keyvault") ||
            r.Type.ToLowerInvariant().Contains("storage") ||
            r.Type.ToLowerInvariant().Contains("sql") ||
            r.Type.ToLowerInvariant().Contains("documentdb")).Count();

        var zeroTrustScore = criticalServices > 0 ? (decimal)privateEndpoints / criticalServices * 100 : 100;

        if (zeroTrustScore < 75m && criticalServices > 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "zero.trust.readiness",
                ResourceName = "Zero Trust Readiness",
                SecurityControl = "Zero Trust Architecture",
                Issue = $"Zero Trust network readiness is {zeroTrustScore:F1}% - more private connectivity needed for critical services",
                Recommendation = "Implement Zero Trust network architecture by deploying private endpoints, disabling public access, and using identity-based access controls",
                Severity = zeroTrustScore < 50m ? "High" : "Medium",
                ComplianceFramework = "Zero Trust Security Model"
            });
        }

        // Public IP exposure assessment
        if (publicIPs > criticalServices * 2)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "public.ip.exposure",
                ResourceName = "Public IP Exposure",
                SecurityControl = "Attack Surface Reduction",
                Issue = $"High number of public IP addresses ({publicIPs}) relative to critical services ({criticalServices}) increases attack surface",
                Recommendation = "Review public IP usage and consider consolidating through load balancers, NAT gateways, or private endpoint connections",
                Severity = "Medium",
                ComplianceFramework = "Zero Trust Security Model"
            });
        }

        await Task.CompletedTask;
    }
}