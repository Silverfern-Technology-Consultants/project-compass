using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Core.Models.Assessment;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Compass.Core.Services.Identity;

public class StaleUsersDevicesAnalyzer : IStaleUsersDevicesAnalyzer
{
    private readonly IAzureResourceGraphService _resourceGraphService;
    private readonly IOAuthService _oauthService;
    private readonly IMicrosoftGraphService _graphService;
    private readonly ILogger<StaleUsersDevicesAnalyzer> _logger;

    public StaleUsersDevicesAnalyzer(
        IAzureResourceGraphService resourceGraphService,
        IOAuthService oauthService,
        IMicrosoftGraphService graphService,
        ILogger<StaleUsersDevicesAnalyzer> logger)
    {
        _resourceGraphService = resourceGraphService;
        _oauthService = oauthService;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task<IdentityAccessResults> AnalyzeAsync(string[] subscriptionIds, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Stale Users and Devices analysis (limited - no Graph access)");

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

        try
        {
            var allResources = await _resourceGraphService.GetResourcesAsync(subscriptionIds, cancellationToken);

            // Focus on identity-related resources
            var virtualMachines = allResources.Where(r =>
                r.Type.ToLowerInvariant() == "microsoft.compute/virtualmachines").ToList();

            foreach (var vm in virtualMachines)
            {
                await AnalyzeVirtualMachineIdentityAsync(vm, findings);
            }

            results.InactiveUsers = findings.Count(f => f.FindingType.Contains("User") || f.FindingType.Contains("Identity"));

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "UserDeviceAnalysisLimited",
                ResourceId = "microsoft.graph.users",
                ResourceName = "User and Device Analysis",
                Severity = "Medium",
                Description = "Comprehensive user and device analysis requires Microsoft Graph and Intune permissions",
                Recommendation = "Configure Microsoft Graph permissions to analyze user accounts, device compliance, and lifecycle management",
                BusinessImpact = "Cannot fully assess identity lifecycle and device security risks",
                AdditionalData = new Dictionary<string, string>
                {
                    ["RequiredPermissions"] = "User.Read.All, Device.Read.All, DeviceManagementManagedDevices.Read.All"
                }
            });

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("Stale Users and Devices analysis completed (limited). Identity issues: {IdentityIssues}",
                results.InactiveUsers);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze stale users and devices");

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "UserDeviceAnalysisError",
                ResourceId = "error.userdevice",
                ResourceName = "User Device Analysis",
                Severity = "Medium",
                Description = "Failed to analyze user and device security",
                Recommendation = "Review permissions and retry analysis",
                BusinessImpact = "Cannot assess identity lifecycle security risks"
            });

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
        }
    }

    public async Task<IdentityAccessResults> AnalyzeWithOAuthAsync(string[] subscriptionIds, Guid clientId, Guid organizationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Stale Users and Devices analysis with Microsoft Graph enhancement");

        var results = new IdentityAccessResults();
        var findings = new List<IdentitySecurityFinding>();

        try
        {
            // Test Graph access first
            var hasGraphAccess = await _graphService.TestGraphConnectionAsync(clientId, organizationId);

            if (!hasGraphAccess)
            {
                _logger.LogWarning("Microsoft Graph access not available for client {ClientId}, falling back to limited analysis", clientId);
                return await AnalyzeAsync(subscriptionIds, cancellationToken);
            }

            _logger.LogInformation("Microsoft Graph access confirmed for client {ClientId}, performing enhanced Users and Devices analysis", clientId);

            // Get inactive users (haven't signed in for 90+ days)
            var inactiveUsers = await _graphService.GetInactiveUsersAsync(clientId, organizationId, 90);
            var allDevices = await _graphService.GetDevicesAsync(clientId, organizationId);
            var nonCompliantDevices = await _graphService.GetNonCompliantDevicesAsync(clientId, organizationId);

            results.InactiveUsers = inactiveUsers.Count;
            results.UnmanagedDevices = nonCompliantDevices.Count;

            // Analyze inactive users
            foreach (var user in inactiveUsers.Take(10)) // Limit to top 10 for findings
            {
                var daysSinceSignIn = user.SignInActivity?.LastSignInDateTime.HasValue == true
                    ? (DateTime.UtcNow - user.SignInActivity.LastSignInDateTime.Value.DateTime).Days
                    : 999;

                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "InactiveUser",
                    ResourceId = user.Id ?? string.Empty,
                    ResourceName = user.DisplayName ?? user.UserPrincipalName ?? "Unknown User",
                    Severity = daysSinceSignIn > 180 ? "High" : "Medium",
                    Description = $"User has not signed in for {daysSinceSignIn} days",
                    Recommendation = "Review if user account is still needed or disable/remove inactive accounts",
                    BusinessImpact = "Inactive user accounts increase attack surface and compliance risks"
                });
            }

            // Analyze non-compliant devices
            foreach (var device in nonCompliantDevices.Take(10)) // Limit to top 10 for findings
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "NonCompliantDevice",
                    ResourceId = device.Id ?? string.Empty,
                    ResourceName = device.DisplayName ?? "Unknown Device",
                    Severity = "Medium",
                    Description = "Device does not meet compliance policies",
                    Recommendation = "Update device to meet compliance requirements or restrict access",
                    BusinessImpact = "Non-compliant devices may lack security controls and pose security risks"
                });
            }

            // Add summary finding if there are many inactive users
            if (inactiveUsers.Count > 10)
            {
                findings.Add(new IdentitySecurityFinding
                {
                    FindingType = "HighInactiveUserCount",
                    ResourceId = "users.inactive.summary",
                    ResourceName = "Inactive User Summary",
                    Severity = "Medium",
                    Description = $"Found {inactiveUsers.Count} inactive users (90+ days without sign-in)",
                    Recommendation = "Implement regular user lifecycle review process and automated cleanup policies",
                    BusinessImpact = "Large number of inactive users increases administrative overhead and security risks"
                });
            }

            results.SecurityFindings = findings;
            results.Score = CalculateScore(results);

            _logger.LogInformation("Users and Devices analysis completed with Graph. Inactive Users: {InactiveUsers}, Non-compliant Devices: {NonCompliantDevices}",
                results.InactiveUsers, results.UnmanagedDevices);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze users and devices with Graph for client {ClientId}", clientId);

            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "UserDeviceAnalysisError",
                ResourceId = "graph.userdevice.error",
                ResourceName = "User Device Analysis",
                Severity = "Medium",
                Description = "Failed to analyze users and devices using Microsoft Graph",
                Recommendation = "Review Microsoft Graph permissions and retry analysis",
                BusinessImpact = "Cannot assess user lifecycle and device security risks without Graph access"
            });

            results.SecurityFindings = findings;
            results.Score = 0;
            return results;
        }
    }

    private async Task AnalyzeVirtualMachineIdentityAsync(AzureResource vm, List<IdentitySecurityFinding> findings)
    {
        string vmName = vm.Name;
        string vmId = vm.Id;

        bool hasManagedIdentity = false;

        if (!string.IsNullOrEmpty(vm.Properties))
        {
            try
            {
                var properties = JsonDocument.Parse(vm.Properties);
                if (properties.RootElement.TryGetProperty("identity", out var identity))
                {
                    hasManagedIdentity = identity.ValueKind != JsonValueKind.Null;
                }
            }
            catch (JsonException)
            {
                // Properties parsing failed
            }
        }

        if (!hasManagedIdentity)
        {
            findings.Add(new IdentitySecurityFinding
            {
                FindingType = "VirtualMachineMissingManagedIdentity",
                ResourceId = vmId,
                ResourceName = vmName,
                Severity = "Medium",
                Description = "Virtual machine does not appear to use managed identity",
                Recommendation = "Enable system-assigned managed identity to eliminate credential management",
                BusinessImpact = "VMs without managed identity may require stored credentials, increasing security risks"
            });
        }

        await Task.CompletedTask;
    }

    private decimal CalculateScore(IdentityAccessResults results)
    {
        var score = 100m;

        // Penalty for inactive users and unmanaged devices
        var userDeviceIssues = results.InactiveUsers + results.UnmanagedDevices;
        if (userDeviceIssues > 0)
        {
            score = Math.Max(0, 100 - (userDeviceIssues * 5));
        }

        // Penalty for critical and high findings
        var criticalFindings = results.SecurityFindings.Count(f => f.Severity == "Critical");
        var highFindings = results.SecurityFindings.Count(f => f.Severity == "High");
        var penalty = (criticalFindings * 15) + (highFindings * 8);

        return Math.Max(0, score - penalty);
    }
}