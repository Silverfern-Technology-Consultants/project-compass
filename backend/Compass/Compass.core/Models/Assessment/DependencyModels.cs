using Compass.Core.Models;

namespace Compass.Core.Models.Assessment;

// Dependency Analysis Models
public class DependencyAnalysisResults
{
    public int TotalResources { get; set; }
    public List<VirtualMachineDependency> VirtualMachineDependencies { get; set; } = new();
    public List<NetworkDependency> NetworkDependencies { get; set; } = new();
    public List<StorageDependency> StorageDependencies { get; set; } = new();
    public List<DatabaseDependency> DatabaseDependencies { get; set; } = new();
    public ResourceGroupAnalysis ResourceGroupAnalysis { get; set; } = new();
    public NetworkTopology NetworkTopology { get; set; } = new();
    public EnvironmentSeparationAnalysis EnvironmentSeparation { get; set; } = new();
}

public class VirtualMachineDependency
{
    public AzureResource VirtualMachine { get; set; } = new();
    public List<AzureResource> NetworkInterfaces { get; set; } = new();
    public List<AzureResource> PublicIPs { get; set; } = new();
    public List<AzureResource> NetworkSecurityGroups { get; set; } = new();
    public List<AzureResource> VirtualNetworks { get; set; } = new();
    public List<AzureResource> ManagedDisks { get; set; } = new();

    public string DependencyChain => BuildDependencyChain();

    private string BuildDependencyChain()
    {
        var chain = $"VM: {VirtualMachine.Name}";
        if (NetworkInterfaces.Any())
            chain += $" → NIC: {string.Join(", ", NetworkInterfaces.Select(n => n.Name))}";
        if (PublicIPs.Any())
            chain += $" → Public IP: {string.Join(", ", PublicIPs.Select(p => p.Name))}";
        if (NetworkSecurityGroups.Any())
            chain += $" → NSG: {string.Join(", ", NetworkSecurityGroups.Select(n => n.Name))}";
        if (VirtualNetworks.Any())
            chain += $" → VNet: {string.Join(", ", VirtualNetworks.Select(v => v.Name))}";
        return chain;
    }
}

public class NetworkDependency
{
    public AzureResource VirtualNetwork { get; set; } = new();
    public List<string> Subnets { get; set; } = new();
    public List<AzureResource> NetworkSecurityGroups { get; set; } = new();
    public List<AzureResource> NetworkInterfaces { get; set; } = new();
    public List<AzureResource> VirtualNetworkGateways { get; set; } = new();
    public List<AzureResource> ConnectedResources { get; set; } = new();
}

public class StorageDependency
{
    public AzureResource StorageAccount { get; set; } = new();
    public List<AzureResource> AssociatedVMs { get; set; } = new();
    public List<AzureResource> Disks { get; set; } = new();
    public List<AzureResource> PrivateEndpoints { get; set; } = new();
}

public class DatabaseDependency
{
    public AzureResource DatabaseServer { get; set; } = new();
    public List<AzureResource> Databases { get; set; } = new();
    public List<AzureResource> PrivateEndpoints { get; set; } = new();
    public List<AzureResource> ConnectedApplications { get; set; } = new();
}

public class ResourceGroupAnalysis
{
    public int TotalResourceGroups { get; set; }
    public decimal AverageResourcesPerGroup { get; set; }
    public List<ResourceGroupStats> ResourceGroups { get; set; } = new();
}

public class ResourceGroupStats
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public int ResourceCount { get; set; }
    public Dictionary<string, int> ResourceTypes { get; set; } = new();
    public Dictionary<string, int> NamingPatterns { get; set; } = new();
    public string PrimaryPurpose { get; set; } = string.Empty;
}

public class NetworkTopology
{
    public int VirtualNetworkCount { get; set; }
    public int NetworkGatewayCount { get; set; }
    public int PublicIPCount { get; set; }
    public string TopologyType { get; set; } = string.Empty;
    public List<NetworkSegment> NetworkSegments { get; set; } = new();
}

public class NetworkSegment
{
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public int ConnectedResourceCount { get; set; }
    public bool HasGateway { get; set; }
    public Dictionary<string, int> ResourceTypeDistribution { get; set; } = new();
}

public class EnvironmentSeparationAnalysis
{
    public bool HasProperSeparation { get; set; }
    public Dictionary<string, int> EnvironmentDistribution { get; set; } = new();
    public List<EnvironmentMixingIssue> MixedEnvironmentNetworks { get; set; } = new();
}

public class EnvironmentMixingIssue
{
    public string NetworkName { get; set; } = string.Empty;
    public List<string> DetectedEnvironments { get; set; } = new();
    public List<string> AffectedResources { get; set; } = new();
    public string RiskLevel { get; set; } = string.Empty;
}