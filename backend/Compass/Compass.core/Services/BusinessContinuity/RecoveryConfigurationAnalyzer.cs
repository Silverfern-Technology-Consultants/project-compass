using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.BusinessContinuity;

public interface IRecoveryConfigurationAnalyzer
{
    Task<BusinessContinuityResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default);

    Task<BusinessContinuityResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

public class RecoveryConfigurationAnalyzer : IRecoveryConfigurationAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<RecoveryConfigurationAnalyzer> _logger;

    public RecoveryConfigurationAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        ILogger<RecoveryConfigurationAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<BusinessContinuityResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Recovery Configuration analysis for {SubscriptionCount} subscriptions", subscriptionIds.Length);

        var results = new BusinessContinuityResults();
        var findings = new List<BusinessContinuityFinding>();

        try
        {
            await AnalyzeRecoveryConfigurationAsync(subscriptionIds, results, findings, cancellationToken);

            results.Findings = findings;
            results.Score = CalculateRecoveryConfigurationScore(results.DisasterRecoveryAnalysis, findings);

            _logger.LogInformation("Recovery Configuration analysis completed. Score: {Score}%, DR Plan: {HasDRPlan}",
                results.Score, results.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze recovery configuration for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<BusinessContinuityResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Recovery Configuration analysis for client {ClientId}", clientId);

        try
        {
            // Test OAuth credentials first
            var hasOAuthCredentials = await _oauthService.TestCredentialsAsync(clientId, organizationId);
            if (hasOAuthCredentials)
            {
                _logger.LogInformation("OAuth credentials available for enhanced recovery analysis");
                // For now, use standard analysis - OAuth enhancement can be added later for Site Recovery APIs
                return await AnalyzeAsync(subscriptionIds, cancellationToken);
            }
            else
            {
                _logger.LogInformation("OAuth not available, using standard recovery analysis");
                return await AnalyzeAsync(subscriptionIds, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OAuth recovery analysis failed, falling back to standard analysis");
            return await AnalyzeAsync(subscriptionIds, cancellationToken);
        }
    }

    private async Task AnalyzeRecoveryConfigurationAsync(
        string[] subscriptionIds,
        BusinessContinuityResults results,
        List<BusinessContinuityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Recovery Configuration across {SubscriptionCount} subscriptions...", subscriptionIds.Length);

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for Site Recovery resources
            var siteRecoveryVaults = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.recoveryservices/vaults").ToList();

            var replicationPolicies = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("replicationpolicies")).ToList();

            // Look for Traffic Manager (for DR routing)
            var trafficManagers = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/trafficmanagerprofiles").ToList();

            // Look for Application Gateways and Load Balancers (for failover)
            var appGateways = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/applicationgateways").ToList();

            var loadBalancers = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/loadbalancers").ToList();

            // Look for Availability Sets and Zones
            var availabilitySets = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.compute/availabilitysets").ToList();

            // Analyze multi-region deployment
            var regions = allResources.Select(r => r.Location).Where(l => !string.IsNullOrEmpty(l)).Distinct().ToList();
            var hasMultiRegion = regions.Count > 1;

            // Initialize disaster recovery analysis
            results.DisasterRecoveryAnalysis = new DisasterRecoveryAnalysis
            {
                HasDisasterRecoveryPlan = siteRecoveryVaults.Any() || trafficManagers.Any(),
                ReplicationEnabledResources = replicationPolicies.Count,
                RecoveryObjectives = new Dictionary<string, string>
                {
                    ["MultiRegionDeployment"] = hasMultiRegion ? "Yes" : "No",
                    ["TrafficManagement"] = trafficManagers.Any() ? "Configured" : "Not Configured",
                    ["SiteRecoveryVaults"] = siteRecoveryVaults.Count.ToString(),
                    ["LoadBalancers"] = loadBalancers.Count.ToString(),
                    ["AvailabilitySets"] = availabilitySets.Count.ToString(),
                    ["TotalRegions"] = regions.Count.ToString()
                }
            };

            // Analyze disaster recovery readiness
            await AnalyzeMultiRegionDeploymentAsync(allResources, regions, hasMultiRegion, results, findings);
            await AnalyzeTrafficManagementAsync(trafficManagers, hasMultiRegion, findings);
            await AnalyzeSiteRecoveryConfigurationAsync(siteRecoveryVaults, allResources, findings);
            await AnalyzeApplicationRedundancyAsync(allResources, findings);
            await AnalyzeAvailabilityConfigurationAsync(availabilitySets, allResources, findings);
            await AnalyzeNetworkRedundancyAsync(loadBalancers, appGateways, findings);
            await AnalyzeAppServiceBackupCapabilitiesAsync(allResources, findings);
            await AnalyzeNetworkDRCapabilitiesAsync(allResources, findings);

            _logger.LogInformation("Recovery Configuration analysis completed. DR Plan: {HasDRPlan}, Replicated Resources: {ReplicatedCount}, Regions: {RegionCount}",
                results.DisasterRecoveryAnalysis.HasDisasterRecoveryPlan,
                results.DisasterRecoveryAnalysis.ReplicationEnabledResources,
                regions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze recovery configuration");
            throw;
        }
    }

    private async Task AnalyzeMultiRegionDeploymentAsync(
        List<AzureResource> allResources,
        List<string> regions,
        bool hasMultiRegion,
        BusinessContinuityResults results,
        List<BusinessContinuityFinding> findings)
    {
        if (!hasMultiRegion)
        {
            var criticalResourceCount = allResources.Count(r =>
                r.Type.ToLowerInvariant().Contains("virtualmachines") ||
                r.Type.ToLowerInvariant().Contains("sql") ||
                r.Type.ToLowerInvariant().Contains("webapp"));

            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = "infrastructure.regions",
                ResourceName = "Multi-Region Deployment",
                Issue = $"All {criticalResourceCount} critical resources deployed in single region, creating single point of failure",
                Recommendation = "Deploy critical resources across multiple Azure regions for disaster recovery",
                Severity = "High",
                BusinessImpact = "Complete service outage if the primary region becomes unavailable"
            });

            results.DisasterRecoveryAnalysis.SinglePointsOfFailure.Add("Single region deployment");
        }
        else
        {
            // Analyze region distribution
            var resourcesByRegion = allResources.GroupBy(r => r.Location).ToDictionary(g => g.Key, g => g.Count());
            var primaryRegion = resourcesByRegion.OrderByDescending(r => r.Value).First();
            var secondaryRegions = resourcesByRegion.Where(r => r.Key != primaryRegion.Key).ToList();

            if (secondaryRegions.Sum(r => r.Value) < primaryRegion.Value * 0.3m) // Less than 30% in secondary regions
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = "infrastructure.distribution",
                    ResourceName = "Resource Distribution",
                    Issue = "Resource distribution heavily skewed toward primary region, insufficient DR capacity",
                    Recommendation = "Balance resource distribution across regions to ensure adequate DR capacity",
                    Severity = "Medium",
                    BusinessImpact = "Insufficient capacity in secondary regions for effective disaster recovery"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeTrafficManagementAsync(
        List<AzureResource> trafficManagers,
        bool hasMultiRegion,
        List<BusinessContinuityFinding> findings)
    {
        if (!trafficManagers.Any() && hasMultiRegion)
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = "traffic.management",
                ResourceName = "Traffic Management",
                Issue = "Multi-region deployment lacks traffic management for automated failover",
                Recommendation = "Implement Azure Traffic Manager or Front Door for intelligent traffic routing and failover",
                Severity = "Medium",
                BusinessImpact = "Manual intervention required during regional outages, extending recovery time"
            });
        }
        else if (trafficManagers.Any())
        {
            // Analyze Traffic Manager configuration
            foreach (var tm in trafficManagers.Take(5)) // Limit for performance
            {
                await AnalyzeTrafficManagerConfigurationAsync(tm, findings);
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeTrafficManagerConfigurationAsync(AzureResource trafficManager, List<BusinessContinuityFinding> findings)
    {
        string tmName = trafficManager.Name;
        string tmId = trafficManager.Id;

        if (!string.IsNullOrEmpty(trafficManager.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(trafficManager.Properties);

                // Check routing method
                if (properties.RootElement.TryGetProperty("trafficRoutingMethod", out var routingMethod))
                {
                    var method = routingMethod.GetString()?.ToLowerInvariant();
                    if (method == "performance" || method == "priority")
                    {
                        // Good for DR scenarios
                    }
                    else if (method == "weighted" || method == "geographic")
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = tmId,
                            ResourceName = tmName,
                            Issue = $"Traffic Manager using {method} routing may not be optimal for disaster recovery",
                            Recommendation = "Consider Priority or Performance routing methods for better disaster recovery capabilities",
                            Severity = "Low",
                            BusinessImpact = "Suboptimal traffic routing during disaster recovery scenarios"
                        });
                    }
                }

                // Check endpoint configuration
                if (properties.RootElement.TryGetProperty("endpoints", out var endpoints))
                {
                    if (endpoints.GetArrayLength() < 2)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = tmId,
                            ResourceName = tmName,
                            Issue = "Traffic Manager has fewer than 2 endpoints configured",
                            Recommendation = "Configure at least 2 endpoints in different regions for effective failover",
                            Severity = "High",
                            BusinessImpact = "No failover capability without multiple endpoints"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSiteRecoveryConfigurationAsync(
        List<AzureResource> siteRecoveryVaults,
        List<AzureResource> allResources,
        List<BusinessContinuityFinding> findings)
    {
        var criticalVMs = allResources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

        if (siteRecoveryVaults.Count == 0 && criticalVMs.Any())
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = "site.recovery",
                ResourceName = "Site Recovery Configuration",
                Issue = $"Found {criticalVMs.Count} virtual machines without Site Recovery protection",
                Recommendation = "Implement Azure Site Recovery for critical virtual machine workloads",
                Severity = "High",
                BusinessImpact = "Extended recovery times and potential data loss during disasters"
            });
        }
        else
        {
            // Analyze Site Recovery vault configuration
            foreach (var vault in siteRecoveryVaults.Take(5)) // Limit for performance
            {
                await AnalyzeSiteRecoveryVaultAsync(vault, findings);
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeSiteRecoveryVaultAsync(AzureResource vault, List<BusinessContinuityFinding> findings)
    {
        string vaultName = vault.Name;
        string vaultId = vault.Id;

        // Check vault tagging for governance
        if (!vault.HasTags || !vault.Tags.ContainsKey("Environment"))
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = vaultId,
                ResourceName = vaultName,
                Issue = "Site Recovery vault lacks proper tagging for governance and management",
                Recommendation = "Add Environment, Owner, and Purpose tags to Site Recovery vault",
                Severity = "Low",
                BusinessImpact = "Difficult to manage and audit disaster recovery infrastructure"
            });
        }

        // General Site Recovery recommendations
        findings.Add(new BusinessContinuityFinding
        {
            Category = "DisasterRecovery",
            ResourceId = vaultId,
            ResourceName = vaultName,
            Issue = "Site Recovery vault configuration should be verified for optimal DR settings",
            Recommendation = "Verify replication policies, recovery plans, and test failover procedures",
            Severity = "Medium",
            BusinessImpact = "Unverified DR configuration may fail during actual disaster scenarios"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeApplicationRedundancyAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        // Analyze App Services for redundancy
        var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();
        var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.sql/servers").ToList();

        // Check App Services for multiple instances
        var appServicesByName = appServices.GroupBy(a => a.Name.Split('-')[0]).ToList();
        foreach (var appGroup in appServicesByName.Take(10)) // Limit for performance
        {
            if (appGroup.Count() == 1)
            {
                var app = appGroup.First();
                bool isProduction = app.Tags.ContainsKey("Environment") &&
                                   app.Tags["Environment"].ToLowerInvariant().Contains("prod");

                if (isProduction)
                {
                    findings.Add(new BusinessContinuityFinding
                    {
                        Category = "DisasterRecovery",
                        ResourceId = app.Id,
                        ResourceName = app.Name,
                        Issue = "Production application has only one instance deployed",
                        Recommendation = "Deploy multiple instances across availability zones or regions for redundancy",
                        Severity = "Medium",
                        BusinessImpact = "Application unavailable during maintenance or regional issues"
                    });
                }
            }
        }

        // Check SQL Servers for geo-redundancy
        foreach (var sqlServer in sqlServers.Take(10)) // Limit for performance
        {
            bool isProduction = sqlServer.Tags.ContainsKey("Environment") &&
                               sqlServer.Tags["Environment"].ToLowerInvariant().Contains("prod");

            if (isProduction)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = sqlServer.Id,
                    ResourceName = sqlServer.Name,
                    Issue = "Production SQL Server should be reviewed for geo-redundancy configuration",
                    Recommendation = "Consider implementing geo-replication, failover groups, or read replicas",
                    Severity = "Medium",
                    BusinessImpact = "Database unavailable during regional outages without geo-redundancy"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAvailabilityConfigurationAsync(
        List<AzureResource> availabilitySets,
        List<AzureResource> allResources,
        List<BusinessContinuityFinding> findings)
    {
        var virtualMachines = allResources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

        // Check if production VMs are using availability sets or zones
        var productionVMs = virtualMachines.Where(vm =>
            vm.Tags.ContainsKey("Environment") &&
            vm.Tags["Environment"].ToLowerInvariant().Contains("prod")).ToList();

        if (productionVMs.Any() && availabilitySets.Count == 0)
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = "availability.configuration",
                ResourceName = "VM Availability Configuration",
                Issue = $"Found {productionVMs.Count} production VMs without availability sets or zones",
                Recommendation = "Configure availability sets or availability zones for production virtual machines",
                Severity = "Medium",
                BusinessImpact = "VMs vulnerable to planned and unplanned downtime without availability configuration"
            });
        }

        // Analyze individual availability sets
        foreach (var availSet in availabilitySets.Take(5))
        {
            await AnalyzeAvailabilitySetConfigurationAsync(availSet, findings);
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeAvailabilitySetConfigurationAsync(AzureResource availabilitySet, List<BusinessContinuityFinding> findings)
    {
        string asName = availabilitySet.Name;
        string asId = availabilitySet.Id;

        if (!string.IsNullOrEmpty(availabilitySet.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(availabilitySet.Properties);

                // Check fault domain count
                if (properties.RootElement.TryGetProperty("platformFaultDomainCount", out var faultDomains))
                {
                    var faultDomainCount = faultDomains.GetInt32();
                    if (faultDomainCount < 2)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = asId,
                            ResourceName = asName,
                            Issue = "Availability set has insufficient fault domains for high availability",
                            Recommendation = "Configure at least 2 fault domains for proper availability protection",
                            Severity = "Medium",
                            BusinessImpact = "Reduced protection against hardware failures"
                        });
                    }
                }

                // Check update domain count
                if (properties.RootElement.TryGetProperty("platformUpdateDomainCount", out var updateDomains))
                {
                    var updateDomainCount = updateDomains.GetInt32();
                    if (updateDomainCount < 5)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = asId,
                            ResourceName = asName,
                            Issue = "Availability set has suboptimal update domain configuration",
                            Recommendation = "Consider increasing update domains to 5 for better maintenance resilience",
                            Severity = "Low",
                            BusinessImpact = "Increased downtime during planned maintenance activities"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeNetworkRedundancyAsync(
        List<AzureResource> loadBalancers,
        List<AzureResource> appGateways,
        List<BusinessContinuityFinding> findings)
    {
        // Check for load balancer configuration
        if (loadBalancers.Any())
        {
            foreach (var lb in loadBalancers.Take(5)) // Limit for performance
            {
                await AnalyzeLoadBalancerConfigurationAsync(lb, findings);
            }
        }

        // Check for application gateway configuration
        if (appGateways.Any())
        {
            foreach (var ag in appGateways.Take(5)) // Limit for performance
            {
                await AnalyzeApplicationGatewayConfigurationAsync(ag, findings);
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeLoadBalancerConfigurationAsync(AzureResource loadBalancer, List<BusinessContinuityFinding> findings)
    {
        string lbName = loadBalancer.Name;
        string lbId = loadBalancer.Id;

        if (!string.IsNullOrEmpty(loadBalancer.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(loadBalancer.Properties);

                // Check for backend pools
                if (properties.RootElement.TryGetProperty("backendAddressPools", out var backendPools))
                {
                    if (backendPools.GetArrayLength() == 0)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = lbId,
                            ResourceName = lbName,
                            Issue = "Load balancer has no backend address pools configured",
                            Recommendation = "Configure backend pools with multiple instances for high availability",
                            Severity = "Medium",
                            BusinessImpact = "Load balancer provides no redundancy without backend pools"
                        });
                    }
                }

                // Check for health probes
                if (properties.RootElement.TryGetProperty("probes", out var probes))
                {
                    if (probes.GetArrayLength() == 0)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = lbId,
                            ResourceName = lbName,
                            Issue = "Load balancer lacks health probes for backend monitoring",
                            Recommendation = "Configure health probes to ensure traffic is only sent to healthy instances",
                            Severity = "Medium",
                            BusinessImpact = "Traffic may be sent to unhealthy instances without health probes"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeApplicationGatewayConfigurationAsync(AzureResource appGateway, List<BusinessContinuityFinding> findings)
    {
        string agName = appGateway.Name;
        string agId = appGateway.Id;

        // General Application Gateway analysis
        findings.Add(new BusinessContinuityFinding
        {
            Category = "DisasterRecovery",
            ResourceId = agId,
            ResourceName = agName,
            Issue = "Application Gateway configuration should be verified for high availability",
            Recommendation = "Ensure Application Gateway has multiple instances and proper health probe configuration",
            Severity = "Low",
            BusinessImpact = "Application Gateway single point of failure without proper HA configuration"
        });

        await Task.CompletedTask;
    }

    private decimal CalculateRecoveryConfigurationScore(DisasterRecoveryAnalysis drAnalysis, List<BusinessContinuityFinding> findings)
    {
        var baseScore = 100m;

        // Deduct points for missing DR plan
        if (!drAnalysis.HasDisasterRecoveryPlan)
        {
            baseScore -= 40m;
        }

        // Deduct points for single points of failure
        foreach (var spof in drAnalysis.SinglePointsOfFailure)
        {
            baseScore -= 15m;
        }

        // Apply penalties for critical findings
        var criticalFindings = findings.Count(f => f.Severity == "High");
        var mediumFindings = findings.Count(f => f.Severity == "Medium");

        var penalty = (criticalFindings * 10) + (mediumFindings * 5);
        var finalScore = Math.Max(0, baseScore - penalty);

        _logger.LogInformation("Recovery Configuration Score: Base {BaseScore}%, Penalty {Penalty}, Final {FinalScore}%",
            baseScore, penalty, finalScore);

        return Math.Round(finalScore, 2);
    }

    private async Task AnalyzeAppServiceBackupCapabilitiesAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();
        var functionApps = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites" &&
                                                   r.Kind?.ToLowerInvariant().Contains("functionapp") == true).ToList();

        // Analyze App Services
        foreach (var app in appServices.Take(10))
        {
            bool isProduction = app.Tags.ContainsKey("Environment") &&
                               app.Tags["Environment"].ToLowerInvariant().Contains("prod");

            if (isProduction)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = app.Id,
                    ResourceName = app.Name,
                    Issue = "Production App Service backup configuration should be verified",
                    Recommendation = "Configure App Service backup to include app content, configuration, and databases. Consider deployment slots for blue-green deployments.",
                    Severity = "Medium",
                    BusinessImpact = "App Service configuration and content vulnerable to loss without proper backup"
                });
            }

            // Check for deployment slots (indicates DR planning)
            if (!string.IsNullOrEmpty(app.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(app.Properties);
                    if (properties.RootElement.TryGetProperty("slotSwapStatus", out var swapStatus) ||
                        app.Name.Contains("-staging") || app.Name.Contains("-slot"))
                    {
                        // Has deployment slots - this is good for DR
                    }
                    else if (isProduction)
                    {
                        findings.Add(new BusinessContinuityFinding
                        {
                            Category = "DisasterRecovery",
                            ResourceId = app.Id,
                            ResourceName = app.Name,
                            Issue = "Production App Service lacks deployment slots for zero-downtime deployments",
                            Recommendation = "Create staging deployment slots to enable blue-green deployments and reduce deployment risks",
                            Severity = "Low",
                            BusinessImpact = "Higher risk deployments without staging slots for testing and rollback"
                        });
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed
                }
            }
        }

        // Analyze Function Apps
        foreach (var func in functionApps.Take(10))
        {
            bool isProduction = func.Tags.ContainsKey("Environment") &&
                               func.Tags["Environment"].ToLowerInvariant().Contains("prod");

            if (isProduction)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = func.Id,
                    ResourceName = func.Name,
                    Issue = "Production Function App disaster recovery strategy should be defined",
                    Recommendation = "Implement Function App deployment automation, source control integration, and consider multi-region deployment for critical functions",
                    Severity = "Medium",
                    BusinessImpact = "Function Apps require deployment automation for quick recovery in disaster scenarios"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeNetworkDRCapabilitiesAsync(List<AzureResource> allResources, List<BusinessContinuityFinding> findings)
    {
        var vpnGateways = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworkgateways").ToList();
        var expressRouteCircuits = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/expressroutecircuits").ToList();
        var dnsZones = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/dnszones").ToList();
        var publicIPs = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses").ToList();

        // Analyze VPN Gateway redundancy
        foreach (var vpnGw in vpnGateways.Take(5))
        {
            if (!string.IsNullOrEmpty(vpnGw.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(vpnGw.Properties);
                    if (properties.RootElement.TryGetProperty("sku", out var sku))
                    {
                        if (sku.TryGetProperty("name", out var skuName))
                        {
                            var skuValue = skuName.GetString()?.ToLowerInvariant();
                            if (skuValue?.Contains("basic") == true)
                            {
                                findings.Add(new BusinessContinuityFinding
                                {
                                    Category = "DisasterRecovery",
                                    ResourceId = vpnGw.Id,
                                    ResourceName = vpnGw.Name,
                                    Issue = "VPN Gateway using Basic SKU lacks high availability features",
                                    Recommendation = "Upgrade to Standard or HighPerformance SKU for active-active configuration and better resilience",
                                    Severity = "Medium",
                                    BusinessImpact = "Basic VPN Gateway is single point of failure for hybrid connectivity"
                                });
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed
                }
            }
        }

        // Analyze ExpressRoute redundancy
        if (expressRouteCircuits.Any())
        {
            var circuitsByLocation = expressRouteCircuits.GroupBy(er => er.Location).ToList();

            if (circuitsByLocation.Count == 1 && expressRouteCircuits.Count == 1)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = "expressroute.redundancy",
                    ResourceName = "ExpressRoute Redundancy",
                    Issue = "Single ExpressRoute circuit creates connectivity single point of failure",
                    Recommendation = "Implement dual ExpressRoute circuits in different peering locations for redundancy",
                    Severity = "High",
                    BusinessImpact = "Complete loss of dedicated connectivity if single ExpressRoute circuit fails"
                });
            }

            foreach (var circuit in expressRouteCircuits.Take(3))
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = circuit.Id,
                    ResourceName = circuit.Name,
                    Issue = "ExpressRoute circuit monitoring and failover procedures should be verified",
                    Recommendation = "Ensure ExpressRoute monitoring is configured and failover procedures are documented and tested",
                    Severity = "Medium",
                    BusinessImpact = "ExpressRoute failures require rapid detection and response for business continuity"
                });
            }
        }

        // Analyze DNS zone redundancy
        foreach (var dnsZone in dnsZones.Take(5))
        {
            bool isProduction = dnsZone.Tags.ContainsKey("Environment") &&
                               dnsZone.Tags["Environment"].ToLowerInvariant().Contains("prod");

            if (isProduction)
            {
                findings.Add(new BusinessContinuityFinding
                {
                    Category = "DisasterRecovery",
                    ResourceId = dnsZone.Id,
                    ResourceName = dnsZone.Name,
                    Issue = "Production DNS zone disaster recovery strategy should be defined",
                    Recommendation = "Implement DNS zone backup, consider secondary DNS services, and document DNS failover procedures",
                    Severity = "Medium",
                    BusinessImpact = "DNS zone failures can impact all services relying on domain name resolution"
                });
            }
        }

        // Analyze Public IP redundancy and standard SKU usage
        var basicPublicIPs = publicIPs.Where(pip =>
        {
            if (!string.IsNullOrEmpty(pip.Properties))
            {
                try
                {
                    var properties = JsonDocument.Parse(pip.Properties);
                    if (properties.RootElement.TryGetProperty("sku", out var sku))
                    {
                        if (sku.TryGetProperty("name", out var skuName))
                        {
                            return skuName.GetString()?.ToLowerInvariant() == "basic";
                        }
                    }
                }
                catch (JsonException)
                {
                    // Properties parsing failed
                }
            }
            return false;
        }).ToList();

        if (basicPublicIPs.Any())
        {
            findings.Add(new BusinessContinuityFinding
            {
                Category = "DisasterRecovery",
                ResourceId = "publicip.sku.basic",
                ResourceName = "Basic SKU Public IPs",
                Issue = $"Found {basicPublicIPs.Count} Basic SKU Public IPs that don't support availability zones",
                Recommendation = "Upgrade to Standard SKU Public IPs for availability zone support and better SLA",
                Severity = "Low",
                BusinessImpact = "Basic Public IPs don't support availability zones, limiting high availability options"
            });
        }

        await Task.CompletedTask;
    }
}