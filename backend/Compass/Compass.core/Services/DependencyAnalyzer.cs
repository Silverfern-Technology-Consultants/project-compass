using Compass.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Compass.Core.Services;

public interface IDependencyAnalyzer
{
    Task<DependencyAnalysisResults> AnalyzeDependenciesAsync(List<AzureResource> resources, CancellationToken cancellationToken = default);
}

public class DependencyAnalyzer : IDependencyAnalyzer
{
    private readonly ILogger<DependencyAnalyzer> _logger;

    public DependencyAnalyzer(ILogger<DependencyAnalyzer> logger)
    {
        _logger = logger;
    }

    public Task<DependencyAnalysisResults> AnalyzeDependenciesAsync(List<AzureResource> resources, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting dependency analysis for {ResourceCount} resources", resources.Count);

        var results = new DependencyAnalysisResults
        {
            TotalResources = resources.Count
        };

        // Create resource lookup for faster dependency resolution
        var resourceLookup = resources.ToDictionary(r => r.Id, r => r);
        var resourcesByName = resources
        .GroupBy(r => r.Name.ToLowerInvariant())
        .ToDictionary(g => g.Key, g => g.First());

        // Analyze different dependency types
        results.VirtualMachineDependencies = AnalyzeVMDependencies(resources, resourceLookup, resourcesByName);
        results.NetworkDependencies = AnalyzeNetworkDependencies(resources, resourceLookup, resourcesByName);
        results.StorageDependencies = AnalyzeStorageDependencies(resources, resourceLookup, resourcesByName);
        results.DatabaseDependencies = AnalyzeDatabaseDependencies(resources, resourceLookup, resourcesByName);
        results.ResourceGroupAnalysis = AnalyzeResourceGroupDistribution(resources);
        results.NetworkTopology = AnalyzeNetworkTopology(resources);
        results.EnvironmentSeparation = AnalyzeEnvironmentSeparation(results.VirtualMachineDependencies, results.NetworkDependencies);

        _logger.LogInformation("Dependency analysis completed. Found {VMCount} VM dependencies, {NetworkCount} network dependencies",
            results.VirtualMachineDependencies.Count, results.NetworkDependencies.Count);

        return Task.FromResult(results);
    }

    private List<VirtualMachineDependency> AnalyzeVMDependencies(
        List<AzureResource> resources,
        Dictionary<string, AzureResource> resourceLookup,
        Dictionary<string, AzureResource> resourcesByName)
    {
        var vmDependencies = new List<VirtualMachineDependency>();
        var vms = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

        foreach (var vm in vms)
        {
            var dependency = new VirtualMachineDependency
            {
                VirtualMachine = vm,
                NetworkInterfaces = new List<AzureResource>(),
                PublicIPs = new List<AzureResource>(),
                NetworkSecurityGroups = new List<AzureResource>(),
                VirtualNetworks = new List<AzureResource>(),
                ManagedDisks = new List<AzureResource>()
            };

            // Find network interfaces associated with this VM
            var associatedNICs = FindAssociatedNetworkInterfaces(vm, resources);
            dependency.NetworkInterfaces.AddRange(associatedNICs);

            foreach (var nic in associatedNICs)
            {
                // Find public IPs associated with this NIC
                var publicIPs = FindAssociatedPublicIPs(nic, resources);
                dependency.PublicIPs.AddRange(publicIPs);

                // Find NSGs associated with this NIC
                var nsgs = FindAssociatedNSGs(nic, resources, resourcesByName);
                dependency.NetworkSecurityGroups.AddRange(nsgs);

                // Find virtual networks
                var vnets = FindAssociatedVirtualNetworks(nic, resources, resourcesByName);
                dependency.VirtualNetworks.AddRange(vnets);
            }

            // Find managed disks
            var disks = FindAssociatedDisks(vm, resources);
            dependency.ManagedDisks.AddRange(disks);

            vmDependencies.Add(dependency);
        }

        return vmDependencies;
    }

    private List<NetworkDependency> AnalyzeNetworkDependencies(
        List<AzureResource> resources,
        Dictionary<string, AzureResource> resourceLookup,
        Dictionary<string, AzureResource> resourcesByName)
    {
        var networkDependencies = new List<NetworkDependency>();
        var virtualNetworks = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks").ToList();

        foreach (var vnet in virtualNetworks)
        {
            var dependency = new NetworkDependency
            {
                VirtualNetwork = vnet,
                Subnets = new List<string>(),
                NetworkSecurityGroups = new List<AzureResource>(),
                NetworkInterfaces = new List<AzureResource>(),
                VirtualNetworkGateways = new List<AzureResource>(),
                ConnectedResources = new List<AzureResource>()
            };

            // Find NSGs in the same resource group or with similar naming
            var vnetName = vnet.Name.ToLowerInvariant();
            var nsgs = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/networksecuritygroups" &&
                (r.ResourceGroup == vnet.ResourceGroup ||
                 r.Name.ToLowerInvariant().Contains(vnetName) ||
                 vnetName.Contains(r.Name.ToLowerInvariant().Replace("-nsg", "").Replace("nsg", "")))
            ).ToList();
            dependency.NetworkSecurityGroups.AddRange(nsgs);

            // Find NICs that might be associated
            var nics = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/networkinterfaces" &&
                (r.ResourceGroup == vnet.ResourceGroup || IsNamingRelated(r.Name, vnet.Name))
            ).ToList();
            dependency.NetworkInterfaces.AddRange(nics);

            // Find virtual network gateways
            var gateways = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworkgateways" &&
                (r.ResourceGroup == vnet.ResourceGroup || IsNamingRelated(r.Name, vnet.Name))
            ).ToList();
            dependency.VirtualNetworkGateways.AddRange(gateways);

            // Extract subnet information from properties if available
            if (!string.IsNullOrEmpty(vnet.Properties))
            {
                dependency.Subnets = ExtractSubnetsFromProperties(vnet.Properties);
            }

            networkDependencies.Add(dependency);
        }

        return networkDependencies;
    }

    private List<StorageDependency> AnalyzeStorageDependencies(
        List<AzureResource> resources,
        Dictionary<string, AzureResource> resourceLookup,
        Dictionary<string, AzureResource> resourcesByName)
    {
        var storageDependencies = new List<StorageDependency>();
        var storageAccounts = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.storage/storageaccounts").ToList();

        foreach (var storage in storageAccounts)
        {
            var dependency = new StorageDependency
            {
                StorageAccount = storage,
                AssociatedVMs = new List<AzureResource>(),
                Disks = new List<AzureResource>(),
                PrivateEndpoints = new List<AzureResource>()
            };

            // Find VMs that might use this storage (based on naming patterns)
            var associatedVMs = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines" &&
                (r.ResourceGroup == storage.ResourceGroup || IsNamingRelated(r.Name, storage.Name))
            ).ToList();
            dependency.AssociatedVMs.AddRange(associatedVMs);

            // Find disks that might be related
            var relatedDisks = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.compute/disks" &&
                (r.ResourceGroup == storage.ResourceGroup || IsNamingRelated(r.Name, storage.Name))
            ).ToList();
            dependency.Disks.AddRange(relatedDisks);

            // Find private endpoints
            var privateEndpoints = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/privateendpoints" &&
                r.ResourceGroup == storage.ResourceGroup
            ).ToList();
            dependency.PrivateEndpoints.AddRange(privateEndpoints);

            storageDependencies.Add(dependency);
        }

        return storageDependencies;
    }

    private List<DatabaseDependency> AnalyzeDatabaseDependencies(
        List<AzureResource> resources,
        Dictionary<string, AzureResource> resourceLookup,
        Dictionary<string, AzureResource> resourcesByName)
    {
        var dbDependencies = new List<DatabaseDependency>();
        var sqlServers = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.sql/servers").ToList();

        foreach (var sqlServer in sqlServers)
        {
            var dependency = new DatabaseDependency
            {
                DatabaseServer = sqlServer,
                Databases = new List<AzureResource>(),
                PrivateEndpoints = new List<AzureResource>(),
                ConnectedApplications = new List<AzureResource>()
            };

            // Find databases on this server
            var databases = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.sql/servers/databases" &&
                r.Id.Contains(sqlServer.Name, StringComparison.OrdinalIgnoreCase)
            ).ToList();
            dependency.Databases.AddRange(databases);

            // Find private endpoints for this SQL server
            var privateEndpoints = resources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.network/privateendpoints" &&
                (r.ResourceGroup == sqlServer.ResourceGroup ||
                 r.Name.Contains(sqlServer.Name, StringComparison.OrdinalIgnoreCase))
            ).ToList();
            dependency.PrivateEndpoints.AddRange(privateEndpoints);

            // Find potential connected applications (web apps, function apps in same RG)
            var connectedApps = resources.Where(r =>
                (r.Type.ToLowerInvariant() == "microsoft.web/sites" ||
                 r.Type.ToLowerInvariant() == "microsoft.web/functions") &&
                r.ResourceGroup == sqlServer.ResourceGroup
            ).ToList();
            dependency.ConnectedApplications.AddRange(connectedApps);

            dbDependencies.Add(dependency);
        }

        return dbDependencies;
    }

    private ResourceGroupAnalysis AnalyzeResourceGroupDistribution(List<AzureResource> resources)
    {
        var analysis = new ResourceGroupAnalysis();

        var resourceGroups = resources.GroupBy(r => r.ResourceGroup ?? "Unknown").ToList();

        foreach (var rg in resourceGroups)
        {
            var rgStats = new ResourceGroupStats
            {
                ResourceGroupName = rg.Key,
                ResourceCount = rg.Count(),
                ResourceTypes = rg.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.Count())
            };

            // Analyze naming patterns within the resource group
            var namingPatterns = rg.GroupBy(r => ClassifyNamingPattern(r.Name))
                .ToDictionary(g => g.Key, g => g.Count());
            rgStats.NamingPatterns = namingPatterns;

            // Determine primary purpose based on resource types
            rgStats.PrimaryPurpose = DeterminePrimaryPurpose(rgStats.ResourceTypes);

            analysis.ResourceGroups.Add(rgStats);
        }

        analysis.TotalResourceGroups = resourceGroups.Count;
        analysis.AverageResourcesPerGroup = resourceGroups.Count > 0
            ? Math.Round((decimal)resources.Count / resourceGroups.Count, 1)
            : 0;

        return analysis;
    }

    private NetworkTopology AnalyzeNetworkTopology(List<AzureResource> resources)
    {
        var topology = new NetworkTopology();

        var virtualNetworks = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks").ToList();
        var networkGateways = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworkgateways").ToList();
        var publicIPs = resources.Where(r => r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses").ToList();

        topology.VirtualNetworkCount = virtualNetworks.Count;
        topology.NetworkGatewayCount = networkGateways.Count;
        topology.PublicIPCount = publicIPs.Count;

        // Analyze network segmentation
        foreach (var vnet in virtualNetworks)
        {
            var networkSegment = new NetworkSegment
            {
                VirtualNetworkName = vnet.Name,
                ResourceGroup = vnet.ResourceGroup ?? "Unknown",
                ConnectedResourceCount = CountConnectedResources(vnet, resources),
                HasGateway = networkGateways.Any(gw =>
                    gw.ResourceGroup == vnet.ResourceGroup ||
                    IsNamingRelated(gw.Name, vnet.Name))
            };

            // Analyze connected resource types
            var connectedResources = resources.Where(r =>
                r.ResourceGroup == vnet.ResourceGroup ||
                IsNetworkRelated(r, vnet)).ToList();

            networkSegment.ResourceTypeDistribution = connectedResources
                .GroupBy(r => r.Type)
                .ToDictionary(g => g.Key, g => g.Count());

            topology.NetworkSegments.Add(networkSegment);
        }

        // Determine if this is a hub-and-spoke or flat network topology
        topology.TopologyType = DetermineTopologyType(virtualNetworks, networkGateways);

        return topology;
    }

    private EnvironmentSeparationAnalysis AnalyzeEnvironmentSeparation(
        List<VirtualMachineDependency> vmDependencies,
        List<NetworkDependency> networkDependencies)
    {
        var analysis = new EnvironmentSeparationAnalysis();

        // Analyze environment indicators in VM names
        var environmentPatterns = new Dictionary<string, List<string>>
        {
            ["development"] = new(),
            ["dev"] = new(),
            ["test"] = new(),
            ["staging"] = new(),
            ["production"] = new(),
            ["prod"] = new()
        };

        foreach (var vmDep in vmDependencies)
        {
            var vmName = vmDep.VirtualMachine.Name.ToLowerInvariant();
            foreach (var env in environmentPatterns.Keys)
            {
                if (vmName.Contains(env))
                {
                    environmentPatterns[env].Add(vmDep.VirtualMachine.Name);
                    break;
                }
            }
        }

        // Check for environment mixing within virtual networks
        foreach (var networkDep in networkDependencies)
        {
            var vnetName = networkDep.VirtualNetwork.Name;
            var connectedVMs = vmDependencies
                .Where(vm => vm.VirtualNetworks.Any(vn => vn.Name == vnetName))
                .ToList();

            if (connectedVMs.Count > 1)
            {
                var environments = new HashSet<string>();
                foreach (var vm in connectedVMs)
                {
                    var detectedEnv = DetectEnvironmentFromName(vm.VirtualMachine.Name);
                    if (!string.IsNullOrEmpty(detectedEnv))
                    {
                        environments.Add(detectedEnv);
                    }
                }

                if (environments.Count > 1)
                {
                    analysis.MixedEnvironmentNetworks.Add(new EnvironmentMixingIssue
                    {
                        NetworkName = vnetName,
                        DetectedEnvironments = environments.ToList(),
                        AffectedResources = connectedVMs.Select(vm => vm.VirtualMachine.Name).ToList(),
                        RiskLevel = "High"
                    });
                }
            }
        }

        analysis.EnvironmentDistribution = environmentPatterns
            .Where(kvp => kvp.Value.Any())
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);

        analysis.HasProperSeparation = !analysis.MixedEnvironmentNetworks.Any();

        return analysis;
    }

    // Helper methods
    private List<AzureResource> FindAssociatedNetworkInterfaces(AzureResource vm, List<AzureResource> resources)
    {
        var vmName = vm.Name.ToLowerInvariant();
        return resources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.network/networkinterfaces" &&
            (r.ResourceGroup == vm.ResourceGroup &&
             (r.Name.ToLowerInvariant().Contains(vmName) || vmName.Contains(r.Name.ToLowerInvariant().Replace("-nic", "").Replace("nic", ""))))
        ).ToList();
    }

    private List<AzureResource> FindAssociatedPublicIPs(AzureResource nic, List<AzureResource> resources)
    {
        var nicName = nic.Name.ToLowerInvariant().Replace("-nic", "").Replace("nic", "");
        return resources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.network/publicipaddresses" &&
            r.ResourceGroup == nic.ResourceGroup &&
            (r.Name.ToLowerInvariant().Contains(nicName) || nicName.Contains(r.Name.ToLowerInvariant().Replace("-ip", "").Replace("ip", "")))
        ).ToList();
    }

    private List<AzureResource> FindAssociatedNSGs(AzureResource nic, List<AzureResource> resources, Dictionary<string, AzureResource> resourcesByName)
    {
        var nicName = nic.Name.ToLowerInvariant().Replace("-nic", "").Replace("nic", "");
        return resources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.network/networksecuritygroups" &&
            r.ResourceGroup == nic.ResourceGroup &&
            (r.Name.ToLowerInvariant().Contains(nicName) ||
             r.Name.ToLowerInvariant().Contains("nsg") && nicName.Contains(r.Name.ToLowerInvariant().Replace("-nsg", "").Replace("nsg", "")))
        ).ToList();
    }

    private List<AzureResource> FindAssociatedVirtualNetworks(AzureResource nic, List<AzureResource> resources, Dictionary<string, AzureResource> resourcesByName)
    {
        return resources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.network/virtualnetworks" &&
            (r.ResourceGroup == nic.ResourceGroup || IsNamingRelated(r.Name, nic.Name))
        ).ToList();
    }

    private List<AzureResource> FindAssociatedDisks(AzureResource vm, List<AzureResource> resources)
    {
        var vmName = vm.Name.ToLowerInvariant();
        return resources.Where(r =>
            r.Type.ToLowerInvariant() == "microsoft.compute/disks" &&
            (r.ResourceGroup == vm.ResourceGroup &&
             (r.Name.ToLowerInvariant().Contains(vmName) ||
              r.Name.ToLowerInvariant().Contains("osdisk") ||
              r.Name.ToLowerInvariant().Contains("datadisk")))
        ).ToList();
    }

    private bool IsNamingRelated(string name1, string name2)
    {
        var clean1 = CleanResourceName(name1);
        var clean2 = CleanResourceName(name2);

        return clean1.Contains(clean2, StringComparison.OrdinalIgnoreCase) ||
               clean2.Contains(clean1, StringComparison.OrdinalIgnoreCase) ||
               GetResourceBaseName(clean1) == GetResourceBaseName(clean2);
    }

    private bool IsNetworkRelated(AzureResource resource, AzureResource vnet)
    {
        return resource.ResourceGroup == vnet.ResourceGroup ||
               IsNamingRelated(resource.Name, vnet.Name) ||
               (resource.Type.Contains("network", StringComparison.OrdinalIgnoreCase) &&
                resource.ResourceGroup == vnet.ResourceGroup);
    }

    private string CleanResourceName(string name)
    {
        // Remove common suffixes and prefixes
        var cleaned = name.ToLowerInvariant()
            .Replace("-nic", "")
            .Replace("-nsg", "")
            .Replace("-ip", "")
            .Replace("-vnet", "")
            .Replace("-disk", "")
            .Replace("nic", "")
            .Replace("nsg", "")
            .Replace("vnet", "");

        return cleaned;
    }

    private string GetResourceBaseName(string name)
    {
        // Extract the base name by removing common patterns
        var baseName = Regex.Replace(name, @"[-_](dev|test|prod|production|staging|development|\d+|nic|nsg|ip|vnet|disk)$", "", RegexOptions.IgnoreCase);
        return baseName;
    }

    private int CountConnectedResources(AzureResource vnet, List<AzureResource> resources)
    {
        return resources.Count(r =>
            r.ResourceGroup == vnet.ResourceGroup &&
            (r.Type.Contains("network", StringComparison.OrdinalIgnoreCase) ||
             r.Type.Contains("compute", StringComparison.OrdinalIgnoreCase)));
    }

    private string DetermineTopologyType(List<AzureResource> virtualNetworks, List<AzureResource> networkGateways)
    {
        if (virtualNetworks.Count == 1)
            return "Single Network";

        if (networkGateways.Count > 1)
            return "Hub-and-Spoke";

        if (virtualNetworks.Count > 3)
            return "Complex Multi-Network";

        return "Multi-Network";
    }

    private string DeterminePrimaryPurpose(Dictionary<string, int> resourceTypes)
    {
        var totalResources = resourceTypes.Values.Sum();

        if (resourceTypes.ContainsKey("microsoft.compute/virtualmachines"))
            return "Compute";

        if (resourceTypes.ContainsKey("microsoft.web/sites") || resourceTypes.ContainsKey("microsoft.web/serverfarms"))
            return "Web Applications";

        if (resourceTypes.ContainsKey("microsoft.sql/servers"))
            return "Database";

        if (resourceTypes.ContainsKey("microsoft.storage/storageaccounts"))
            return "Storage";

        if (resourceTypes.Keys.Any(k => k.Contains("network", StringComparison.OrdinalIgnoreCase)))
            return "Networking";

        return "Mixed";
    }

    private string ClassifyNamingPattern(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Other";

        if (name.Contains("_")) return "Snake_case";
        if (name.Contains("-")) return "Kebab-case";
        if (name.All(c => !char.IsLetter(c) || char.IsUpper(c))) return "Uppercase";
        if (name.All(c => !char.IsLetter(c) || char.IsLower(c))) return "Lowercase";
        if (char.IsUpper(name[0]) && name.Any(char.IsUpper)) return "PascalCase";
        if (char.IsLower(name[0]) && name.Any(char.IsUpper)) return "CamelCase";

        return "Other";
    }

    private string? DetectEnvironmentFromName(string name)
    {
        var nameLower = name.ToLowerInvariant();
        string[] environments = { "dev", "test", "staging", "stage", "prod", "production", "qa", "uat" };

        return environments.FirstOrDefault(env => nameLower.Contains(env));
    }

    private List<string> ExtractSubnetsFromProperties(string properties)
    {
        var subnets = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(properties);
            if (doc.RootElement.TryGetProperty("subnets", out var subnetsArray))
            {
                foreach (var subnet in subnetsArray.EnumerateArray())
                {
                    if (subnet.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            subnets.Add(name);
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Properties might not be valid JSON or might not contain subnets
        }

        return subnets;
    }
}