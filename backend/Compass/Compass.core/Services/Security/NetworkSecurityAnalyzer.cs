using Azure.ResourceManager.ResourceGraph;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.Security;

public interface INetworkSecurityAnalyzer
{
    Task<SecurityPostureResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default);

    Task<SecurityPostureResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

public class NetworkSecurityAnalyzer : INetworkSecurityAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly IPrivateEndpointAnalyzer _privateEndpointAnalyzer;
    private readonly IDataEncryptionAnalyzer _dataEncryptionAnalyzer;
    private readonly IAdvancedThreatProtectionAnalyzer _atpAnalyzer;
    private readonly ILogger<NetworkSecurityAnalyzer> _logger;

    public NetworkSecurityAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        IPrivateEndpointAnalyzer privateEndpointAnalyzer,
        IDataEncryptionAnalyzer dataEncryptionAnalyzer,
        IAdvancedThreatProtectionAnalyzer atpAnalyzer,
        ILogger<NetworkSecurityAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _privateEndpointAnalyzer = privateEndpointAnalyzer;
        _dataEncryptionAnalyzer = dataEncryptionAnalyzer;
        _atpAnalyzer = atpAnalyzer;
        _logger = logger;
    }

    public async Task<SecurityPostureResults> AnalyzeAsync(
        string[] subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Network Security analysis for subscriptions: {Subscriptions}",
            string.Join(",", subscriptionIds));

        var results = new SecurityPostureResults();
        var findings = new List<SecurityFinding>();

        try
        {
            await AnalyzeNetworkSecurityAsync(subscriptionIds, results, findings, cancellationToken);

            results.SecurityFindings = findings;
            results.Score = CalculateNetworkSecurityScore(results);

            _logger.LogInformation("Network Security analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Network Security for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<SecurityPostureResults> AnalyzeWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Network Security analysis for client {ClientId}", clientId);

        // For now, fall back to standard analysis
        // Future enhancement: Use OAuth for enhanced network security queries
        return await AnalyzeAsync(subscriptionIds, cancellationToken);
    }

    private async Task AnalyzeNetworkSecurityAsync(
        string[] subscriptionIds,
        SecurityPostureResults results,
        List<SecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Network Security controls and configurations...");

        try
        {
            // Get all network-related resources
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Analyze core network security components
            var networkSecurityGroups = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/networksecuritygroups").ToList();

            var virtualNetworks = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks").ToList();

            var publicIPs = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses").ToList();

            var applicationGateways = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/applicationgateways").ToList();

            var loadBalancers = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/loadbalancers").ToList();

            var azureFirewalls = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/azurefirewalls").ToList();

            // Initialize network security analysis
            results.NetworkSecurity = new NetworkSecurityAnalysis
            {
                NetworkSecurityGroups = networkSecurityGroups.Count,
                OpenToInternetRules = 0,
                OverlyPermissiveRules = 0
            };

            // Analyze NSGs for security issues
            int openToInternetRules = 0;
            int overlyPermissiveRules = 0;

            foreach (var nsg in networkSecurityGroups)
            {
                var (openRules, permissiveRules) = await AnalyzeNetworkSecurityGroupAsync(nsg, findings);
                openToInternetRules += openRules;
                overlyPermissiveRules += permissiveRules;
            }

            results.NetworkSecurity.OpenToInternetRules = openToInternetRules;
            results.NetworkSecurity.OverlyPermissiveRules = overlyPermissiveRules;

            // Analyze Virtual Networks for security
            foreach (var vnet in virtualNetworks)
            {
                await AnalyzeVirtualNetworkSecurityAsync(vnet, findings);
            }

            // Analyze Public IPs for exposure risks
            await AnalyzePublicIPExposureAsync(publicIPs, findings);

            // Analyze Application Gateways for WAF configuration
            foreach (var appGw in applicationGateways)
            {
                await AnalyzeApplicationGatewaySecurityAsync(appGw, findings);
            }

            // Analyze Load Balancers for security configuration
            foreach (var loadBalancer in loadBalancers)
            {
                await AnalyzeLoadBalancerSecurityAsync(loadBalancer, findings);
            }

            // Analyze Azure Firewalls
            foreach (var firewall in azureFirewalls)
            {
                await AnalyzeAzureFirewallSecurityAsync(firewall, findings);
            }

            // Integrate specialized security analyzers
            var privateEndpointFindings = await _privateEndpointAnalyzer.AnalyzePrivateEndpointCoverageAsync(allResources, cancellationToken);
            findings.AddRange(privateEndpointFindings);

            var encryptionFindings = await _dataEncryptionAnalyzer.AnalyzeDataEncryptionStatusAsync(allResources, cancellationToken);
            findings.AddRange(encryptionFindings);

            var atpFindings = await _atpAnalyzer.AnalyzeAdvancedThreatProtectionAsync(allResources, cancellationToken);
            findings.AddRange(atpFindings);

            // Perform core network security checks
            await PerformCoreNetworkSecurityChecks(allResources, findings);

            // Check for missing network security components
            if (networkSecurityGroups.Count == 0 && virtualNetworks.Any())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = "network.security",
                    ResourceName = "Network Security Groups",
                    SecurityControl = "Network Segmentation",
                    Issue = "Virtual networks exist without Network Security Groups for traffic filtering",
                    Recommendation = "Deploy Network Security Groups to control traffic flow and implement network segmentation",
                    Severity = "High",
                    ComplianceFramework = "CIS Azure"
                });
            }

            _logger.LogInformation("Network Security analysis completed. NSGs: {NSGCount}, Public IPs: {PublicIPCount}, Open rules: {OpenRules}",
                networkSecurityGroups.Count, publicIPs.Count, openToInternetRules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze network security");

            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "error.network",
                ResourceName = "Network Security Analysis",
                SecurityControl = "Network Assessment",
                Issue = "Failed to analyze network security configuration",
                Recommendation = "Review network permissions and retry security analysis",
                Severity = "High",
                ComplianceFramework = "General"
            });
        }
    }

    private async Task<(int openRules, int permissiveRules)> AnalyzeNetworkSecurityGroupAsync(
        AzureResource nsg,
        List<SecurityFinding> findings)
    {
        string nsgName = nsg.Name;
        string nsgId = nsg.Id;
        int openToInternetRules = 0;
        int overlyPermissiveRules = 0;

        try
        {
            if (!string.IsNullOrEmpty(nsg.Properties))
            {
                using var properties = JsonDocument.Parse(nsg.Properties);
                var rootElement = properties.RootElement;

                if (rootElement.TryGetProperty("securityRules", out var securityRules))
                {
                    var (openRules, permissiveRules, ruleFindings) = await AnalyzeSecurityRules(
                        securityRules, nsgId, nsgName);
                    openToInternetRules += openRules;
                    overlyPermissiveRules += permissiveRules;
                    findings.AddRange(ruleFindings);
                }

                if (rootElement.TryGetProperty("defaultSecurityRules", out var defaultRules))
                {
                    await AnalyzeDefaultSecurityRules(defaultRules, nsgId, nsgName, findings);
                }

                await AnalyzeNsgAssociations(rootElement, nsgId, nsgName, findings);
            }

            await PerformEnhancedNsgSecurityChecks(nsg, findings);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse NSG properties for {NSGName}", nsgName);
        }

        return (openToInternetRules, overlyPermissiveRules);
    }

    private async Task<(int openRules, int permissiveRules, List<SecurityFinding> findings)> AnalyzeSecurityRules(
        JsonElement securityRules, string nsgId, string nsgName)
    {
        var findings = new List<SecurityFinding>();
        int openToInternetRules = 0;
        int overlyPermissiveRules = 0;

        foreach (var rule in securityRules.EnumerateArray())
        {
            try
            {
                if (!rule.TryGetProperty("properties", out var ruleProps))
                    continue;

                var ruleName = rule.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
                var direction = ruleProps.TryGetProperty("direction", out var dirElement) ? dirElement.GetString() : "";
                var access = ruleProps.TryGetProperty("access", out var accessElement) ? accessElement.GetString() : "";
                var protocol = ruleProps.TryGetProperty("protocol", out var protocolElement) ? protocolElement.GetString() : "";
                var sourcePrefix = ruleProps.TryGetProperty("sourceAddressPrefix", out var sourcePrefixElement) ? sourcePrefixElement.GetString() : "";
                var destPort = ruleProps.TryGetProperty("destinationPortRange", out var destPortElement) ? destPortElement.GetString() : "";

                if (direction?.ToLowerInvariant() == "inbound" && access?.ToLowerInvariant() == "allow")
                {
                    if (sourcePrefix == "0.0.0.0/0" || sourcePrefix == "*" || sourcePrefix == "Internet")
                    {
                        openToInternetRules++;
                        var severity = DetermineRuleSeverity(protocol, destPort);

                        findings.Add(new SecurityFinding
                        {
                            Category = "Network",
                            ResourceId = nsgId,
                            ResourceName = $"{nsgName} - Rule: {ruleName}",
                            SecurityControl = "Internet Exposure",
                            Issue = $"NSG rule '{ruleName}' allows inbound traffic from any internet source (0.0.0.0/0) on {protocol?.ToUpper() ?? "ALL"} {destPort ?? "ALL"}",
                            Recommendation = $"Restrict source to specific IP ranges, implement least privilege access. Consider using Application Gateway or Load Balancer with WAF for web traffic.",
                            Severity = severity,
                            ComplianceFramework = "CIS Azure"
                        });
                    }

                    if (IsOverlyPermissiveRule(sourcePrefix, destPort, protocol))
                    {
                        overlyPermissiveRules++;

                        findings.Add(new SecurityFinding
                        {
                            Category = "Network",
                            ResourceId = nsgId,
                            ResourceName = $"{nsgName} - Rule: {ruleName}",
                            SecurityControl = "Network Segmentation",
                            Issue = $"NSG rule '{ruleName}' is overly permissive: {protocol?.ToUpper() ?? "ALL"} from {sourcePrefix} to {destPort ?? "ALL"}",
                            Recommendation = "Implement more restrictive source ranges and specific port restrictions based on actual business requirements",
                            Severity = "Medium",
                            ComplianceFramework = "CIS Azure"
                        });
                    }

                    await CheckDangerousProtocolsAndPorts(rule, ruleName, nsgId, nsgName, findings);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to analyze security rule in NSG {NSGName}", nsgName);
            }
        }

        await Task.CompletedTask;
        return (openToInternetRules, overlyPermissiveRules, findings);
    }

    private string DetermineRuleSeverity(string? protocol, string? destPort)
    {
        if (destPort == "*" || destPort == "0-65535")
            return "Critical";

        var dangerousPorts = new[] { "22", "3389", "1433", "3306", "5432", "6379", "27017" };
        if (dangerousPorts.Contains(destPort))
            return "High";

        if (protocol?.ToLowerInvariant() == "tcp" &&
            (destPort == "80" || destPort == "443" || destPort == "8080"))
            return "High";

        return "Medium";
    }

    private bool IsOverlyPermissiveRule(string? sourcePrefix, string? destPort, string? protocol)
    {
        if (sourcePrefix?.Contains("/8") == true || sourcePrefix?.Contains("/16") == true)
            return true;

        if (destPort?.Contains("-") == true)
        {
            var parts = destPort.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var start) &&
                int.TryParse(parts[1], out var end))
            {
                return (end - start) > 100;
            }
        }

        if (protocol == "*")
            return true;

        return false;
    }

    private async Task CheckDangerousProtocolsAndPorts(JsonElement rule, string? ruleName,
        string nsgId, string nsgName, List<SecurityFinding> findings)
    {
        if (!rule.TryGetProperty("properties", out var props))
            return;

        var protocol = props.TryGetProperty("protocol", out var protocolElement) ? protocolElement.GetString() : "";
        var destPort = props.TryGetProperty("destinationPortRange", out var destPortElement) ? destPortElement.GetString() : "";
        var access = props.TryGetProperty("access", out var accessElement) ? accessElement.GetString() : "";

        if (access?.ToLowerInvariant() != "allow")
            return;

        var criticalPorts = new Dictionary<string, string>
        {
            { "22", "SSH" },
            { "3389", "RDP" },
            { "1433", "SQL Server" },
            { "3306", "MySQL" },
            { "5432", "PostgreSQL" },
            { "6379", "Redis" },
            { "27017", "MongoDB" }
        };

        if (criticalPorts.TryGetValue(destPort ?? "", out var serviceName))
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = nsgId,
                ResourceName = $"{nsgName} - Rule: {ruleName}",
                SecurityControl = "Administrative Access",
                Issue = $"NSG rule allows direct access to {serviceName} (port {destPort}) which should be restricted",
                Recommendation = $"Restrict {serviceName} access to jump servers, VPN, or Azure Bastion. Consider using private endpoints.",
                Severity = "High",
                ComplianceFramework = "CIS Azure"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefaultSecurityRules(JsonElement defaultRules, string nsgId, string nsgName,
        List<SecurityFinding> findings)
    {
        bool hasCustomDenyRule = false;

        foreach (var rule in defaultRules.EnumerateArray())
        {
            if (rule.TryGetProperty("properties", out var props) &&
                props.TryGetProperty("access", out var access) &&
                access.GetString()?.ToLowerInvariant() == "deny")
            {
                hasCustomDenyRule = true;
                break;
            }
        }

        if (!hasCustomDenyRule)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = nsgId,
                ResourceName = nsgName,
                SecurityControl = "Default Security Rules",
                Issue = "NSG relies primarily on default security rules without explicit deny rules",
                Recommendation = "Add explicit deny rules for unused ports and protocols to implement defense in depth",
                Severity = "Low",
                ComplianceFramework = "Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeNsgAssociations(JsonElement rootElement, string nsgId, string nsgName,
        List<SecurityFinding> findings)
    {
        if (rootElement.TryGetProperty("subnets", out var subnets))
        {
            var subnetCount = subnets.GetArrayLength();
            if (subnetCount == 0)
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = nsgId,
                    ResourceName = nsgName,
                    SecurityControl = "NSG Association",
                    Issue = "Network Security Group is not associated with any subnets",
                    Recommendation = "Associate NSG with appropriate subnets or remove if unused to reduce management overhead",
                    Severity = "Low",
                    ComplianceFramework = "Best Practice"
                });
            }
        }

        await Task.CompletedTask;
    }

    private async Task PerformEnhancedNsgSecurityChecks(AzureResource nsg, List<SecurityFinding> findings)
    {
        string nsgName = nsg.Name;
        string nsgId = nsg.Id;

        if (!nsg.HasTags || !nsg.Tags.ContainsKey("Purpose"))
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = nsgId,
                ResourceName = nsgName,
                SecurityControl = "Asset Management",
                Issue = "Network Security Group lacks proper tagging for security governance",
                Recommendation = "Add Purpose, Environment, Owner, and SecurityZone tags to identify NSG function and responsibility",
                Severity = "Low",
                ComplianceFramework = "CIS Azure"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeVirtualNetworkSecurityAsync(AzureResource vnet, List<SecurityFinding> findings)
    {
        string vnetName = vnet.Name;
        string vnetId = vnet.Id;

        if (!string.IsNullOrEmpty(vnet.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(vnet.Properties);
                if (properties.RootElement.TryGetProperty("enableDdosProtection", out var ddosProtection))
                {
                    if (!ddosProtection.GetBoolean())
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category = "Network",
                            ResourceId = vnetId,
                            ResourceName = vnetName,
                            SecurityControl = "DDoS Protection",
                            Issue = "Virtual Network does not have DDoS Protection enabled",
                            Recommendation = "Enable Azure DDoS Protection Standard for production virtual networks",
                            Severity = "Medium",
                            ComplianceFramework = "CIS Azure"
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

    private async Task AnalyzePublicIPExposureAsync(List<AzureResource> publicIPs, List<SecurityFinding> findings)
    {
        if (publicIPs.Count == 0)
        {
            await Task.CompletedTask;
            return;
        }

        foreach (var publicIP in publicIPs.Take(10))
        {
            if (!publicIP.HasTags || !publicIP.Tags.ContainsKey("Purpose"))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = publicIP.Id,
                    ResourceName = publicIP.Name,
                    SecurityControl = "Asset Management",
                    Issue = "Public IP address lacks proper tagging for security tracking",
                    Recommendation = "Add Purpose, Owner, and Environment tags to track public IP usage and ownership",
                    Severity = "Medium",
                    ComplianceFramework = "CIS Azure"
                });
            }
        }

        if (publicIPs.Count > 10)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "public.ip.management",
                ResourceName = "Public IP Management",
                SecurityControl = "Attack Surface Reduction",
                Issue = $"High number of public IP addresses ({publicIPs.Count}) increases attack surface",
                Recommendation = "Review if all public IPs are necessary, consider using NAT Gateway or Load Balancer to reduce exposed IPs",
                Severity = "Medium",
                ComplianceFramework = "NIST"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeApplicationGatewaySecurityAsync(AzureResource appGateway, List<SecurityFinding> findings)
    {
        string appGwName = appGateway.Name;
        string appGwId = appGateway.Id;

        if (!string.IsNullOrEmpty(appGateway.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(appGateway.Properties);

                bool hasWAF = properties.RootElement.TryGetProperty("webApplicationFirewallConfiguration", out var wafConfig) ||
                             appGateway.Sku?.ToLowerInvariant().Contains("waf") == true;

                if (!hasWAF)
                {
                    findings.Add(new SecurityFinding
                    {
                        Category = "Network",
                        ResourceId = appGwId,
                        ResourceName = appGwName,
                        SecurityControl = "Web Application Firewall",
                        Issue = "Application Gateway does not have Web Application Firewall (WAF) enabled",
                        Recommendation = "Enable WAF on Application Gateway to protect against common web attacks (OWASP Top 10)",
                        Severity = "High",
                        ComplianceFramework = "OWASP"
                    });
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeLoadBalancerSecurityAsync(AzureResource loadBalancer, List<SecurityFinding> findings)
    {
        string lbName = loadBalancer.Name;
        string lbId = loadBalancer.Id;

        findings.Add(new SecurityFinding
        {
            Category = "Network",
            ResourceId = lbId,
            ResourceName = lbName,
            SecurityControl = "Load Balancer Security",
            Issue = "Load Balancer security configuration should be verified",
            Recommendation = "Ensure Load Balancer has appropriate health probes, backend pool security, and NSG associations configured",
            Severity = "Low",
            ComplianceFramework = "Azure Security Benchmark"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeAzureFirewallSecurityAsync(AzureResource firewall, List<SecurityFinding> findings)
    {
        string firewallName = firewall.Name;
        string firewallId = firewall.Id;

        _logger.LogDebug("Analyzing Azure Firewall security configuration for {FirewallName}", firewallName);

        try
        {
            if (!string.IsNullOrEmpty(firewall.Properties))
            {
                var properties = JsonDocument.Parse(firewall.Properties);

                if (properties.RootElement.TryGetProperty("threatIntelMode", out var threatIntelMode))
                {
                    var mode = threatIntelMode.GetString()?.ToLowerInvariant();
                    if (mode != "deny")
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category = "Network",
                            ResourceId = firewallId,
                            ResourceName = firewallName,
                            SecurityControl = "Threat Intelligence",
                            Issue = $"Azure Firewall threat intelligence mode is set to '{mode}' instead of 'Deny'",
                            Recommendation = "Configure Azure Firewall threat intelligence to 'Deny' mode to automatically block traffic to/from known malicious IP addresses and domains",
                            Severity = "Medium",
                            ComplianceFramework = "Azure Security Benchmark"
                        });
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Azure Firewall properties for {FirewallName}", firewallName);
        }

        await Task.CompletedTask;
    }

    private async Task PerformCoreNetworkSecurityChecks(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        // Core network security analysis focused on pure network controls
        await AnalyzeNetworkSecurityPostureAsync(allResources, findings);
        await AnalyzeNetworkMonitoringCoverageAsync(allResources, findings);
        await AnalyzeNetworkTrafficControlsAsync(allResources, findings);
    }

    private async Task AnalyzeNetworkSecurityPostureAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        var virtualNetworks = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks").Count();
        var networkSecurityGroups = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/networksecuritygroups").Count();
        var applicationGateways = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/applicationgateways").Count();
        var azureFirewalls = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/azurefirewalls").Count();

        if (virtualNetworks > 0 && azureFirewalls == 0 && applicationGateways == 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "network.security.posture",
                ResourceName = "Network Security Posture",
                SecurityControl = "Defense in Depth",
                Issue = "Virtual networks exist without centralized firewall or application gateway protection",
                Recommendation = "Deploy Azure Firewall or Application Gateway with WAF to provide centralized network security and application protection",
                Severity = "Medium",
                ComplianceFramework = "Defense in Depth"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeNetworkMonitoringCoverageAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        var networkWatchers = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/networkwatchers").Count();
        var virtualNetworks = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks").Count();

        if (virtualNetworks > 0 && networkWatchers == 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "network.monitoring.coverage",
                ResourceName = "Network Monitoring Coverage",
                SecurityControl = "Network Observability",
                Issue = "Virtual networks exist without Network Watcher for monitoring and diagnostics",
                Recommendation = "Deploy Network Watcher in each region with virtual networks to enable flow logs, packet capture, and network diagnostics",
                Severity = "Medium",
                ComplianceFramework = "Azure Security Benchmark"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzeNetworkTrafficControlsAsync(List<AzureResource> allResources, List<SecurityFinding> findings)
    {
        var loadBalancers = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/loadbalancers").Count();
        var trafficManagers = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/trafficmanagerprofiles").Count();
        var frontDoors = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/frontdoors").Count();

        var totalTrafficControls = loadBalancers + trafficManagers + frontDoors;
        var publicIPs = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses").Count();

        if (publicIPs > 5 && totalTrafficControls == 0)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "traffic.control.mechanisms",
                ResourceName = "Network Traffic Controls",
                SecurityControl = "Traffic Management",
                Issue = $"Multiple public IPs ({publicIPs}) without centralized traffic control mechanisms",
                Recommendation = "Implement load balancers, Traffic Manager, or Front Door to centralize and secure traffic routing while reducing public IP exposure",
                Severity = "Medium",
                ComplianceFramework = "Network Security Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private decimal CalculateNetworkSecurityScore(SecurityPostureResults results)
    {
        var baseScore = 100m;

        // Major deductions for critical network security issues
        if (results.NetworkSecurity.OpenToInternetRules > 0)
        {
            baseScore -= results.NetworkSecurity.OpenToInternetRules * 15m; // 15 points per open rule
        }

        if (results.NetworkSecurity.OverlyPermissiveRules > 0)
        {
            baseScore -= results.NetworkSecurity.OverlyPermissiveRules * 8m; // 8 points per permissive rule
        }

        if (results.NetworkSecurity.NetworkSecurityGroups == 0)
        {
            baseScore -= 25m; // 25 points for no NSGs
        }

        // Additional deductions based on enhanced security findings
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var mediumFindings = results.SecurityFindings.Count(f => f.Severity == "Medium");

        baseScore -= (criticalFindings * 12m) + (highFindings * 8m) + (mediumFindings * 3m);

        // Specific deductions for integrated security areas
        var privateEndpointFindings = results.SecurityFindings.Count(f => f.SecurityControl == "Private Connectivity");
        var encryptionFindings = results.SecurityFindings.Count(f => f.Category == "DataEncryption");
        var atpFindings = results.SecurityFindings.Count(f => f.Category == "AdvancedThreatProtection");

        if (privateEndpointFindings > 5) baseScore -= 10m; // Significant private endpoint gaps
        if (encryptionFindings > 3) baseScore -= 8m; // Multiple encryption issues
        if (atpFindings > 5) baseScore -= 6m; // Widespread ATP gaps

        // Bonus for comprehensive security implementation
        var securityControlsImplemented = results.SecurityFindings.Count(f => f.Severity == "Low"); // Low severity often means "should implement"
        if (securityControlsImplemented < 3)
        {
            baseScore += 5m; // Bonus for fewer implementation gaps
        }

        return Math.Max(0, Math.Round(baseScore, 2));
    }
}