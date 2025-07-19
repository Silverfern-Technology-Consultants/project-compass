using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Compass.Core.Interfaces;
using Compass.Core.Models.Assessment;
using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services;

public interface ISecurityPostureAssessmentAnalyzer
{
    Task<SecurityPostureResults> AnalyzeSecurityPostureAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);

    Task<SecurityPostureResults> AnalyzeSecurityPostureWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default);
}

public class SecurityPostureAssessmentAnalyzer : ISecurityPostureAssessmentAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly ILogger<SecurityPostureAssessmentAnalyzer> _logger;

    public SecurityPostureAssessmentAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        ILogger<SecurityPostureAssessmentAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _logger = logger;
    }

    public async Task<SecurityPostureResults> AnalyzeSecurityPostureAsync(
        string[] subscriptionIds,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Security Posture analysis for assessment type: {AssessmentType}", assessmentType);

        var results = new SecurityPostureResults();
        var findings = new List<SecurityFinding>();

        try
        {
            switch (assessmentType)
            {
                case AssessmentType.NetworkSecurity:
                    await AnalyzeNetworkSecurityAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.DefenderForCloud:
                    await AnalyzeDefenderForCloudAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                case AssessmentType.SecurityFull:
                    await AnalyzeNetworkSecurityAsync(subscriptionIds, results, findings, cancellationToken);
                    await AnalyzeDefenderForCloudAsync(subscriptionIds, results, findings, cancellationToken);
                    break;

                default:
                    throw new ArgumentException($"Unsupported Security Posture assessment type: {assessmentType}");
            }

            results.SecurityFindings = findings;
            results.Score = CalculateOverallSecurityScore(results);

            _logger.LogInformation("Security Posture analysis completed. Score: {Score}%, Issues found: {IssuesCount}",
                results.Score, findings.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Security Posture for subscriptions: {Subscriptions}",
                string.Join(",", subscriptionIds));
            throw;
        }
    }

    public async Task<SecurityPostureResults> AnalyzeSecurityPostureWithOAuthAsync(
        string[] subscriptionIds,
        Guid clientId,
        Guid organizationId,
        AssessmentType assessmentType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OAuth-enabled Security Posture analysis for client {ClientId}", clientId);

        // For now, fall back to standard analysis since we don't have OAuth-specific security queries yet
        // In the future, this could use OAuth to access Microsoft Defender for Cloud APIs
        return await AnalyzeSecurityPostureAsync(subscriptionIds, assessmentType, cancellationToken);
    }

    private async Task AnalyzeNetworkSecurityAsync(
        string[] subscriptionIds,
        SecurityPostureResults results,
        List<SecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Network Security...");

        try
        {
            // Get all network-related resources
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Analyze Network Security Groups
            var networkSecurityGroups = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/networksecuritygroups").ToList();

            // Analyze Virtual Networks
            var virtualNetworks = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks").ToList();

            // Analyze Public IPs
            var publicIPs = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses").ToList();

            // Analyze Application Gateways and Load Balancers
            var applicationGateways = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/applicationgateways").ToList();

            var loadBalancers = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/loadbalancers").ToList();

            // NEW: Analyze Key Vaults for network security
            var keyVaults = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("keyvault")).ToList();

            // Initialize network security analysis
            results.NetworkSecurity = new NetworkSecurityAnalysis
            {
                NetworkSecurityGroups = networkSecurityGroups.Count,
                OpenToInternetRules = 0, // Will be calculated
                OverlyPermissiveRules = 0 // Will be calculated
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

            // NEW: Analyze Key Vaults for network security configuration
            foreach (var keyVault in keyVaults)
            {
                await AnalyzeKeyVaultNetworkSecurityAsync(keyVault, findings);
            }

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

            _logger.LogInformation("Network Security analysis completed. NSGs: {NSGCount}, Public IPs: {PublicIPCount}, Key Vaults: {KeyVaultCount}, Open rules: {OpenRules}",
                networkSecurityGroups.Count, publicIPs.Count, keyVaults.Count, openToInternetRules);
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

    // NEW: Key Vault Network Security Analysis
    private async Task AnalyzeKeyVaultNetworkSecurityAsync(AzureResource keyVault, List<SecurityFinding> findings)
    {
        string keyVaultName = keyVault.Name;
        string keyVaultId = keyVault.Id;

        _logger.LogDebug("Analyzing Key Vault network security for {KeyVaultName}", keyVaultName);

        if (!string.IsNullOrEmpty(keyVault.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(keyVault.Properties);

                // Check public network access configuration
                if (properties.RootElement.TryGetProperty("publicNetworkAccess", out var networkAccess))
                {
                    if (networkAccess.GetString()?.ToLowerInvariant() == "enabled")
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category = "Network",
                            ResourceId = keyVaultId,
                            ResourceName = keyVaultName,
                            SecurityControl = "Network Access Control",
                            Issue = "Key Vault allows public network access, increasing attack surface",
                            Recommendation = "Configure private endpoints and disable public network access for enhanced security",
                            Severity = "High",
                            ComplianceFramework = "CIS Azure"
                        });
                    }
                }

                // Check for network ACLs configuration
                if (properties.RootElement.TryGetProperty("networkAcls", out var networkAcls))
                {
                    if (networkAcls.TryGetProperty("defaultAction", out var defaultAction))
                    {
                        if (defaultAction.GetString()?.ToLowerInvariant() == "allow")
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "Network",
                                ResourceId = keyVaultId,
                                ResourceName = keyVaultName,
                                SecurityControl = "Network Access Control",
                                Issue = "Key Vault network ACLs default action is 'Allow', permitting broad access",
                                Recommendation = "Set default action to 'Deny' and configure specific IP ranges or virtual network rules",
                                Severity = "Medium",
                                ComplianceFramework = "CIS Azure"
                            });
                        }
                    }

                    // Check for IP rules configuration
                    if (networkAcls.TryGetProperty("ipRules", out var ipRules))
                    {
                        var ipRuleCount = ipRules.GetArrayLength();
                        if (ipRuleCount > 20)
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "Network",
                                ResourceId = keyVaultId,
                                ResourceName = keyVaultName,
                                SecurityControl = "Network Access Control",
                                Issue = $"Key Vault has {ipRuleCount} IP rules, which may be difficult to manage",
                                Recommendation = "Review and consolidate IP rules, consider using virtual network service endpoints instead",
                                Severity = "Low",
                                ComplianceFramework = "Best Practice"
                            });
                        }

                        // Check for overly broad IP ranges
                        foreach (var ipRule in ipRules.EnumerateArray())
                        {
                            if (ipRule.TryGetProperty("value", out var ipValue))
                            {
                                var ip = ipValue.GetString();
                                if (ip == "0.0.0.0/0" || ip == "*")
                                {
                                    findings.Add(new SecurityFinding
                                    {
                                        Category = "Network",
                                        ResourceId = keyVaultId,
                                        ResourceName = keyVaultName,
                                        SecurityControl = "Network Access Control",
                                        Issue = "Key Vault allows access from any IP address (0.0.0.0/0)",
                                        Recommendation = "Restrict IP rules to specific trusted IP ranges only",
                                        Severity = "High",
                                        ComplianceFramework = "CIS Azure"
                                    });
                                }
                            }
                        }
                    }

                    // Check for virtual network rules
                    if (networkAcls.TryGetProperty("virtualNetworkRules", out var vnetRules))
                    {
                        var vnetRuleCount = vnetRules.GetArrayLength();
                        if (vnetRuleCount == 0 && networkAcls.TryGetProperty("defaultAction", out var defAction) &&
                            defAction.GetString()?.ToLowerInvariant() == "deny")
                        {
                            // This is actually good - deny by default with no broad vnet rules
                            _logger.LogDebug("Key Vault {KeyVaultName} has restrictive network configuration", keyVaultName);
                        }
                    }
                }

                // Check for private endpoint connections
                if (properties.RootElement.TryGetProperty("privateEndpointConnections", out var privateEndpoints))
                {
                    var privateEndpointCount = privateEndpoints.GetArrayLength();
                    if (privateEndpointCount == 0)
                    {
                        // Only flag this if public access is also enabled
                        if (properties.RootElement.TryGetProperty("publicNetworkAccess", out var pubAccess) &&
                            pubAccess.GetString()?.ToLowerInvariant() == "enabled")
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "Network",
                                ResourceId = keyVaultId,
                                ResourceName = keyVaultName,
                                SecurityControl = "Private Connectivity",
                                Issue = "Key Vault lacks private endpoint connections and allows public access",
                                Recommendation = "Configure private endpoints to enable secure, private connectivity to Key Vault",
                                Severity = "Medium",
                                ComplianceFramework = "Azure Security Benchmark"
                            });
                        }
                    }
                }

                // Check if Key Vault is in a production environment (based on tags or name)
                bool isProduction = keyVault.Environment?.ToLowerInvariant().Contains("prod") == true ||
                                   keyVault.Tags.ContainsKey("Environment") &&
                                   keyVault.Tags["Environment"].ToLowerInvariant().Contains("prod");

                if (isProduction)
                {
                    // Production Key Vaults should have stricter network controls
                    if (properties.RootElement.TryGetProperty("publicNetworkAccess", out var prodNetworkAccess) &&
                        prodNetworkAccess.GetString()?.ToLowerInvariant() == "enabled")
                    {
                        findings.Add(new SecurityFinding
                        {
                            Category = "Network",
                            ResourceId = keyVaultId,
                            ResourceName = keyVaultName,
                            SecurityControl = "Production Security",
                            Issue = "Production Key Vault should not allow public network access",
                            Recommendation = "Disable public network access for production Key Vaults and use private endpoints exclusively",
                            Severity = "High",
                            ComplianceFramework = "Production Security Best Practice"
                        });
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse Key Vault properties for network security analysis: {KeyVaultName}", keyVaultName);

                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = keyVaultId,
                    ResourceName = keyVaultName,
                    SecurityControl = "Configuration Analysis",
                    Issue = "Unable to parse Key Vault network configuration for detailed security analysis",
                    Recommendation = "Verify Key Vault configuration and permissions for network security analysis",
                    Severity = "Low",
                    ComplianceFramework = "General"
                });
            }
        }
        else
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = keyVaultId,
                ResourceName = keyVaultName,
                SecurityControl = "Configuration Analysis",
                Issue = "Key Vault network configuration properties are not available for analysis",
                Recommendation = "Ensure proper permissions to read Key Vault network configuration",
                Severity = "Low",
                ComplianceFramework = "General"
            });
        }

        await Task.CompletedTask;
    }

    // Rest of the existing methods remain the same but with updated method signatures
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
            // Parse NSG properties for security rules
            if (!string.IsNullOrEmpty(nsg.Properties))
            {
                using var properties = JsonDocument.Parse(nsg.Properties);
                var rootElement = properties.RootElement;

                // Extract security rules from properties
                if (rootElement.TryGetProperty("securityRules", out var securityRules))
                {
                    var (openRules, permissiveRules, ruleFindings) = await AnalyzeSecurityRules(
                        securityRules, nsgId, nsgName);
                    openToInternetRules += openRules;
                    overlyPermissiveRules += permissiveRules;
                    findings.AddRange(ruleFindings);
                }
                // Also check default security rules if available
                if (rootElement.TryGetProperty("defaultSecurityRules", out var defaultRules))
                {
                    await AnalyzeDefaultSecurityRules(defaultRules, nsgId, nsgName, findings);
                }
                // Check for subnets and NICs associations
                await AnalyzeNsgAssociations(rootElement, nsgId, nsgName, findings);
            }
            // Enhanced general NSG security checks
            await PerformEnhancedNsgSecurityChecks(nsg, findings);
            _logger.LogInformation("Enhanced NSG analysis completed for {NSGName}. Open rules: {OpenRules}, Permissive rules: {PermissiveRules}",
                nsgName, openToInternetRules, overlyPermissiveRules);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse NSG properties for {NSGName}", nsgName);

            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = nsgId,
                ResourceName = nsgName,
                SecurityControl = "NSG Analysis",
                Issue = "Unable to parse Network Security Group configuration for detailed analysis",
                Recommendation = "Verify NSG configuration and permissions for security rule analysis",
                Severity = "Medium",
                ComplianceFramework = "General"
            });
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

                // Extract rule details
                var direction = ruleProps.TryGetProperty("direction", out var dirElement) ? dirElement.GetString() : "";
                var access = ruleProps.TryGetProperty("access", out var accessElement) ? accessElement.GetString() : "";
                var protocol = ruleProps.TryGetProperty("protocol", out var protocolElement) ? protocolElement.GetString() : "";
                var sourcePrefix = ruleProps.TryGetProperty("sourceAddressPrefix", out var sourcePrefixElement) ? sourcePrefixElement.GetString() : "";
                var destPrefix = ruleProps.TryGetProperty("destinationAddressPrefix", out var destPrefixElement) ? destPrefixElement.GetString() : "";
                var sourcePort = ruleProps.TryGetProperty("sourcePortRange", out var sourcePortElement) ? sourcePortElement.GetString() : "";
                var destPort = ruleProps.TryGetProperty("destinationPortRange", out var destPortElement) ? destPortElement.GetString() : "";
                var priority = ruleProps.TryGetProperty("priority", out var priorityElement) ? priorityElement.GetInt32() : 0;

                // Check for dangerous inbound rules from internet
                if (direction?.ToLowerInvariant() == "inbound" && access?.ToLowerInvariant() == "allow")
                {
                    // Check for open to internet (0.0.0.0/0 or *)
                    if (sourcePrefix == "0.0.0.0/0" || sourcePrefix == "*" || sourcePrefix == "Internet")
                    {
                        openToInternetRules++;

                        var severity = DetermineRuleSeverity(protocol, destPort, sourcePort);

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

                    // Check for overly broad internal access
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

                    // Check for dangerous protocols and ports
                    await CheckDangerousProtocolsAndPorts(rule, ruleName, nsgId, nsgName, findings);
                }

                // Check for unused rules (high priority but rarely matched)
                if (priority < 1000 && IsLowUtilizationRule(ruleName))
                {
                    findings.Add(new SecurityFinding
                    {
                        Category = "Network",
                        ResourceId = nsgId,
                        ResourceName = $"{nsgName} - Rule: {ruleName}",
                        SecurityControl = "Rule Management",
                        Issue = $"High-priority NSG rule '{ruleName}' may be unused or have low utilization",
                        Recommendation = "Review rule utilization metrics and consider removing or consolidating unused rules",
                        Severity = "Low",
                        ComplianceFramework = "Best Practice"
                    });
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

    private string DetermineRuleSeverity(string? protocol, string? destPort, string? sourcePort)
    {
        // Critical severity for dangerous combinations
        if (destPort == "*" || destPort == "0-65535")
            return "Critical";

        // High severity for common attack vectors
        var dangerousPorts = new[] { "22", "3389", "1433", "3306", "5432", "6379", "27017" };
        if (dangerousPorts.Contains(destPort))
            return "High";

        // High severity for administrative protocols
        if (protocol?.ToLowerInvariant() == "tcp" &&
            (destPort == "80" || destPort == "443" || destPort == "8080"))
            return "High";

        return "Medium";
    }

    private bool IsOverlyPermissiveRule(string? sourcePrefix, string? destPort, string? protocol)
    {
        // Check for overly broad source ranges
        if (sourcePrefix?.Contains("/8") == true || sourcePrefix?.Contains("/16") == true)
            return true;

        // Check for wide port ranges
        if (destPort?.Contains("-") == true)
        {
            var parts = destPort.Split('-');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var start) &&
                int.TryParse(parts[1], out var end))
            {
                return (end - start) > 100; // Port range > 100 ports
            }
        }

        // All protocols wildcard
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

        // Critical administrative ports
        var criticalPorts = new Dictionary<string, string>
        {
            { "22", "SSH" },
            { "3389", "RDP" },
            { "1433", "SQL Server" },
            { "3306", "MySQL" },
            { "5432", "PostgreSQL" },
            { "6379", "Redis" },
            { "27017", "MongoDB" },
            { "5984", "CouchDB" },
            { "9200", "Elasticsearch" },
            { "5601", "Kibana" }
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

        // Check for insecure protocols
        var insecureProtocolPorts = new Dictionary<string, string>
        {
            { "21", "FTP (unencrypted)" },
            { "23", "Telnet (unencrypted)" },
            { "80", "HTTP (unencrypted web traffic)" },
            { "143", "IMAP (unencrypted)" },
            { "110", "POP3 (unencrypted)" },
            { "161", "SNMP (often misconfigured)" }
        };

        if (insecureProtocolPorts.TryGetValue(destPort ?? "", out var insecureService))
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = nsgId,
                ResourceName = $"{nsgName} - Rule: {ruleName}",
                SecurityControl = "Encryption in Transit",
                Issue = $"NSG rule allows {insecureService} which transmits data in clear text",
                Recommendation = "Use encrypted alternatives: SFTP instead of FTP, SSH instead of Telnet, HTTPS instead of HTTP",
                Severity = "Medium",
                ComplianceFramework = "CIS Azure"
            });
        }

        await Task.CompletedTask;
    }

    private bool IsLowUtilizationRule(string? ruleName)
    {
        // Simple heuristics for potentially unused rules
        // In a real implementation, this would check Azure Monitor metrics
        var lowUtilizationIndicators = new[] { "temp", "test", "old", "backup", "unused", "legacy" };

        return lowUtilizationIndicators.Any(indicator =>
            ruleName?.ToLowerInvariant().Contains(indicator) == true);
    }

    private async Task AnalyzeDefaultSecurityRules(JsonElement defaultRules, string nsgId, string nsgName,
        List<SecurityFinding> findings)
    {
        // Analyze if default rules have been properly supplemented with custom rules
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
        // Check subnet associations
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
            else if (subnetCount > 5)
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = nsgId,
                    ResourceName = nsgName,
                    SecurityControl = "NSG Association",
                    Issue = $"NSG is associated with {subnetCount} subnets, which may indicate overly broad security policies",
                    Recommendation = "Consider creating more specific NSGs for different subnet security requirements",
                    Severity = "Medium",
                    ComplianceFramework = "Best Practice"
                });
            }
        }

        // Check network interface associations
        if (rootElement.TryGetProperty("networkInterfaces", out var networkInterfaces))
        {
            var nicCount = networkInterfaces.GetArrayLength();
            if (nicCount > 10)
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = nsgId,
                    ResourceName = nsgName,
                    SecurityControl = "NSG Association",
                    Issue = $"NSG is associated with {nicCount} network interfaces, which may be difficult to manage",
                    Recommendation = "Consider subnet-level NSG association instead of individual NIC associations for easier management",
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

        // Check for proper tagging indicating purpose and environment
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

        // Check for environment-specific security requirements
        var environment = nsg.Environment?.ToLowerInvariant();
        if (environment == "prod" || environment == "production")
        {
            // Production NSGs should have stricter requirements
            if (!nsg.Tags.ContainsKey("SecurityZone"))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = nsgId,
                    ResourceName = nsgName,
                    SecurityControl = "Production Security",
                    Issue = "Production NSG should have SecurityZone classification (DMZ, Internal, Management)",
                    Recommendation = "Add SecurityZone tag to classify network security tiers for proper segmentation",
                    Severity = "Medium",
                    ComplianceFramework = "CIS Azure"
                });
            }

            if (!nsg.Tags.ContainsKey("DataClassification"))
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = nsgId,
                    ResourceName = nsgName,
                    SecurityControl = "Data Protection",
                    Issue = "Production NSG should have data classification tags for compliance",
                    Recommendation = "Add DataClassification tag (Public, Internal, Confidential, Restricted) for compliance tracking",
                    Severity = "Medium",
                    ComplianceFramework = "ISO 27001"
                });
            }
        }

        // Check naming convention for security clarity
        if (!IsSecurityFriendlyName(nsgName))
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = nsgId,
                ResourceName = nsgName,
                SecurityControl = "Naming Convention",
                Issue = "NSG name doesn't clearly indicate its security purpose or network tier",
                Recommendation = "Use descriptive names like 'nsg-web-dmz-prod' or 'nsg-db-internal-dev' to indicate purpose and security zone",
                Severity = "Low",
                ComplianceFramework = "Best Practice"
            });
        }

        await Task.CompletedTask;
    }

    private bool IsSecurityFriendlyName(string nsgName)
    {
        var securityIndicators = new[] { "dmz", "internal", "mgmt", "web", "db", "app", "jump", "bastion" };
        var environmentIndicators = new[] { "dev", "test", "stage", "prod" };

        var nameLower = nsgName.ToLowerInvariant();

        return securityIndicators.Any(indicator => nameLower.Contains(indicator)) &&
               environmentIndicators.Any(env => nameLower.Contains(env));
    }

    private async Task AnalyzeVirtualNetworkSecurityAsync(AzureResource vnet, List<SecurityFinding> findings)
    {
        string vnetName = vnet.Name;
        string vnetId = vnet.Id;

        // Check for DDoS protection
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

        // Check for proper network segmentation (based on naming and tagging)
        bool hasProperSegmentation = vnet.Name.ToLowerInvariant().Contains("dmz") ||
                                    vnet.Name.ToLowerInvariant().Contains("internal") ||
                                    vnet.Tags.ContainsKey("NetworkTier");

        if (!hasProperSegmentation)
        {
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = vnetId,
                ResourceName = vnetName,
                SecurityControl = "Network Segmentation",
                Issue = "Virtual Network lacks clear segmentation indicators",
                Recommendation = "Implement network segmentation with clear naming conventions and network tiers (DMZ, Internal, Management)",
                Severity = "Medium",
                ComplianceFramework = "NIST"
            });
        }

        await Task.CompletedTask;
    }

    private async Task AnalyzePublicIPExposureAsync(List<AzureResource> publicIPs, List<SecurityFinding> findings)
    {
        if (publicIPs.Count == 0)
        {
            // No public IPs might be good for security, but check if this is intentional
            findings.Add(new SecurityFinding
            {
                Category = "Network",
                ResourceId = "public.ip.exposure",
                ResourceName = "Public IP Configuration",
                SecurityControl = "Internet Exposure",
                Issue = "No public IP addresses found - verify this aligns with architecture requirements",
                Recommendation = "Confirm if the lack of public IPs is intentional for security or if NAT Gateway/Load Balancer is needed",
                Severity = "Low",
                ComplianceFramework = "General"
            });
            await Task.CompletedTask;
            return;
        }

        // Analyze each public IP for security risks
        foreach (var publicIP in publicIPs)
        {
            // Check if public IP is properly tagged and managed
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

            // Check for production public IPs (higher security requirements)
            bool isProduction = publicIP.Tags.ContainsKey("Environment") &&
                               publicIP.Tags["Environment"].ToLowerInvariant().Contains("prod");

            if (isProduction)
            {
                findings.Add(new SecurityFinding
                {
                    Category = "Network",
                    ResourceId = publicIP.Id,
                    ResourceName = publicIP.Name,
                    SecurityControl = "Internet Exposure",
                    Issue = "Production public IP should have enhanced monitoring and protection",
                    Recommendation = "Ensure production public IPs have DDoS protection, monitoring alerts, and are associated with WAF-protected resources",
                    Severity = "Medium",
                    ComplianceFramework = "CIS Azure"
                });
            }
        }

        // General recommendation for public IP management
        if (publicIPs.Count > 5)
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

        // Check for WAF configuration
        if (!string.IsNullOrEmpty(appGateway.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(appGateway.Properties);

                // Look for WAF configuration
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
                else
                {
                    // Check WAF mode if configuration exists
                    if (wafConfig.TryGetProperty("firewallMode", out var firewallMode))
                    {
                        if (firewallMode.GetString()?.ToLowerInvariant() == "detection")
                        {
                            findings.Add(new SecurityFinding
                            {
                                Category = "Network",
                                ResourceId = appGwId,
                                ResourceName = appGwName,
                                SecurityControl = "Web Application Firewall",
                                Issue = "WAF is in Detection mode instead of Prevention mode",
                                Recommendation = "Configure WAF in Prevention mode to actively block malicious requests",
                                Severity = "Medium",
                                ComplianceFramework = "OWASP"
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

        // Check for proper SSL/TLS configuration
        findings.Add(new SecurityFinding
        {
            Category = "Network",
            ResourceId = appGwId,
            ResourceName = appGwName,
            SecurityControl = "Encryption in Transit",
            Issue = "Application Gateway SSL/TLS configuration should be verified",
            Recommendation = "Ensure Application Gateway uses TLS 1.2 minimum, strong cipher suites, and valid SSL certificates",
            Severity = "Medium",
            ComplianceFramework = "CIS Azure"
        });

        await Task.CompletedTask;
    }

    private async Task AnalyzeDefenderForCloudAsync(
        string[] subscriptionIds,
        SecurityPostureResults results,
        List<SecurityFinding> findings,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Analyzing Microsoft Defender for Cloud...");

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Look for Security Center resources (these are typically not visible via Resource Graph)
            var securityResources = allResources.Where(r =>
                r.Type.ToLowerInvariant().Contains("security") ||
                r.Type.ToLowerInvariant().Contains("microsoft.security")).ToList();

            // Initialize Defender analysis with default values
            results.DefenderAnalysis = new DefenderForCloudAnalysis
            {
                IsEnabled = securityResources.Any(), // Basic check
                SecurityScore = 0m, // Would need Security Center API to get real score
                HighSeverityRecommendations = 0,
                MediumSeverityRecommendations = 0,
                DefenderPlansStatus = new Dictionary<string, string>()
            };

            // General Defender for Cloud recommendations
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "defender.enablement",
                ResourceName = "Microsoft Defender for Cloud",
                SecurityControl = "Security Monitoring",
                Issue = "Microsoft Defender for Cloud enablement and configuration should be verified",
                Recommendation = "Enable Microsoft Defender for Cloud across all subscriptions and configure enhanced security features",
                Severity = "High",
                ComplianceFramework = "CIS Azure"
            });

            // Check for critical resource types that should have Defender plans
            var virtualMachines = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();
            var sqlServers = allResources.Where(r => r.Type.ToLowerInvariant().Contains("microsoft.sql/servers")).ToList();
            var storageAccounts = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();
            var keyVaults = allResources.Where(r => r.Type.ToLowerInvariant().Contains("keyvault")).ToList();
            var appServices = allResources.Where(r => r.Type.ToLowerInvariant() == "microsoft.web/sites").ToList();

            // Analyze Defender plan recommendations based on resource types
            if (virtualMachines.Any())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = "defender.servers",
                    ResourceName = "Defender for Servers",
                    SecurityControl = "Endpoint Protection",
                    Issue = $"Found {virtualMachines.Count} virtual machines that should be protected by Defender for Servers",
                    Recommendation = "Enable Microsoft Defender for Servers to protect virtual machines from threats and vulnerabilities",
                    Severity = "High",
                    ComplianceFramework = "CIS Azure"
                });

                results.DefenderAnalysis.DefenderPlansStatus["Servers"] = "Review Required";
            }

            if (sqlServers.Any())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = "defender.sql",
                    ResourceName = "Defender for SQL",
                    SecurityControl = "Database Security",
                    Issue = $"Found {sqlServers.Count} SQL servers that should be protected by Defender for SQL",
                    Recommendation = "Enable Microsoft Defender for SQL to detect threats and vulnerabilities in SQL databases",
                    Severity = "High",
                    ComplianceFramework = "CIS Azure"
                });

                results.DefenderAnalysis.DefenderPlansStatus["SQL"] = "Review Required";
            }

            if (storageAccounts.Any())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = "defender.storage",
                    ResourceName = "Defender for Storage",
                    SecurityControl = "Data Protection",
                    Issue = $"Found {storageAccounts.Count} storage accounts that should be protected by Defender for Storage",
                    Recommendation = "Enable Microsoft Defender for Storage to detect malicious activities and data exfiltration attempts",
                    Severity = "Medium",
                    ComplianceFramework = "CIS Azure"
                });

                results.DefenderAnalysis.DefenderPlansStatus["Storage"] = "Review Required";
            }

            if (keyVaults.Any())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = "defender.keyvault",
                    ResourceName = "Defender for Key Vault",
                    SecurityControl = "Secrets Management",
                    Issue = $"Found {keyVaults.Count} key vaults that should be protected by Defender for Key Vault",
                    Recommendation = "Enable Microsoft Defender for Key Vault to detect suspicious access patterns and threats",
                    Severity = "Medium",
                    ComplianceFramework = "CIS Azure"
                });

                results.DefenderAnalysis.DefenderPlansStatus["KeyVault"] = "Review Required";
            }

            if (appServices.Any())
            {
                findings.Add(new SecurityFinding
                {
                    Category = "DefenderForCloud",
                    ResourceId = "defender.appservice",
                    ResourceName = "Defender for App Service",
                    SecurityControl = "Application Security",
                    Issue = $"Found {appServices.Count} app services that should be protected by Defender for App Service",
                    Recommendation = "Enable Microsoft Defender for App Service to detect web application threats and vulnerabilities",
                    Severity = "Medium",
                    ComplianceFramework = "CIS Azure"
                });

                results.DefenderAnalysis.DefenderPlansStatus["AppService"] = "Review Required";
            }

            // Security Center configuration recommendations
            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "security.contacts",
                ResourceName = "Security Contacts",
                SecurityControl = "Incident Response",
                Recommendation = "Configure security contacts to receive alerts and notifications from Microsoft Defender for Cloud",
                Severity = "Medium",
                ComplianceFramework = "CIS Azure"
            });

            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "auto.provisioning",
                ResourceName = "Auto Provisioning",
                SecurityControl = "Security Monitoring",
                Issue = "Auto provisioning of security agents should be enabled",
                Recommendation = "Enable auto provisioning for Log Analytics agent and security extensions to ensure comprehensive monitoring",
                Severity = "Medium",
                ComplianceFramework = "CIS Azure"
            });

            // Update analysis based on findings
            results.DefenderAnalysis.HighSeverityRecommendations = findings.Count(f => f.Category == "DefenderForCloud" && f.Severity == "High");
            results.DefenderAnalysis.MediumSeverityRecommendations = findings.Count(f => f.Category == "DefenderForCloud" && f.Severity == "Medium");

            _logger.LogInformation("Defender for Cloud analysis completed. Security resources: {SecurityCount}, High recommendations: {HighCount}",
                securityResources.Count, results.DefenderAnalysis.HighSeverityRecommendations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze Microsoft Defender for Cloud");

            findings.Add(new SecurityFinding
            {
                Category = "DefenderForCloud",
                ResourceId = "error.defender",
                ResourceName = "Defender for Cloud Analysis",
                SecurityControl = "Security Assessment",
                Issue = "Failed to analyze Microsoft Defender for Cloud configuration",
                Recommendation = "Review permissions and retry Defender for Cloud analysis",
                Severity = "Medium",
                ComplianceFramework = "General"
            });

            // Initialize with error state
            results.DefenderAnalysis = new DefenderForCloudAnalysis
            {
                IsEnabled = false,
                SecurityScore = 0m,
                HighSeverityRecommendations = 1,
                MediumSeverityRecommendations = 0
            };
        }
    }

    private decimal CalculateOverallSecurityScore(SecurityPostureResults results)
    {
        var scoringFactors = new List<decimal>();

        // Network security score (50% weight)
        var networkScore = 100m;
        if (results.NetworkSecurity.OpenToInternetRules > 0)
        {
            networkScore -= results.NetworkSecurity.OpenToInternetRules * 20m; // Major penalty for open rules
        }
        if (results.NetworkSecurity.OverlyPermissiveRules > 0)
        {
            networkScore -= results.NetworkSecurity.OverlyPermissiveRules * 10m; // Penalty for permissive rules
        }
        if (results.NetworkSecurity.NetworkSecurityGroups == 0)
        {
            networkScore -= 30m; // Penalty for no NSGs
        }
        networkScore = Math.Max(0, networkScore);
        scoringFactors.Add(networkScore * 0.50m);

        // Defender for Cloud score (50% weight)
        var defenderScore = 100m;
        if (!results.DefenderAnalysis.IsEnabled)
        {
            defenderScore -= 40m; // Major penalty for not being enabled
        }

        // Penalty based on recommendations
        defenderScore -= (results.DefenderAnalysis.HighSeverityRecommendations * 15m);
        defenderScore -= (results.DefenderAnalysis.MediumSeverityRecommendations * 8m);

        defenderScore = Math.Max(0, defenderScore);
        scoringFactors.Add(defenderScore * 0.50m);

        // Critical finding penalty
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var penalty = (criticalFindings * 20) + (highFindings * 12);

        var finalScore = Math.Max(0, scoringFactors.Sum() - penalty);

        _logger.LogInformation("Security Score calculated: {Score}% (Network: {NetworkScore}%, Defender: {DefenderScore}%, Penalty: {Penalty})",
            finalScore, networkScore, defenderScore, penalty);

        return Math.Round(finalScore, 2);
    }
}