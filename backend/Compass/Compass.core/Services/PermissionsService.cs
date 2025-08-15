using Microsoft.Extensions.Logging;
using System.Text.Json;
using Compass.Core.Interfaces;
using Compass.Core.Models;
using Compass.Data.Interfaces;
using Compass.Data.Entities;

namespace Compass.Core.Services
{
    public class PermissionsService : IPermissionsService
    {
        private readonly IOAuthService _oauthService;
        private readonly IClientRepository _clientRepository;
        private readonly ILogger<PermissionsService> _logger;

        public PermissionsService(
            IOAuthService oauthService,
            IClientRepository clientRepository,
            ILogger<PermissionsService> logger)
        {
            _oauthService = oauthService;
            _clientRepository = clientRepository;
            _logger = logger;
        }

        public async Task<CostManagementPermissionStatus> CheckAndUpdateCostPermissionsAsync(
            Guid azureEnvironmentId, 
            Guid organizationId)
        {
            try
            {
                // Get Azure environment
                var azureEnvironment = await _clientRepository.GetAzureEnvironmentByIdAsync(
                    azureEnvironmentId, organizationId);
                
                if (azureEnvironment == null)
                {
                    throw new ArgumentException($"Azure environment {azureEnvironmentId} not found");
                }

                if (azureEnvironment.ClientId == null)
                {
                    throw new InvalidOperationException("Azure environment must be associated with a client");
                }

                // Test permissions for the first subscription
                var subscriptionId = azureEnvironment.SubscriptionIds.FirstOrDefault();
                if (string.IsNullOrEmpty(subscriptionId))
                {
                    throw new InvalidOperationException("No subscriptions found in Azure environment");
                }

                var permissionStatus = await _oauthService.TestCostManagementPermissionsAsync(
                    azureEnvironment.ClientId.Value, organizationId, subscriptionId);

                // Update database with results
                await UpdateAzureEnvironmentPermissions(azureEnvironment, permissionStatus);

                _logger.LogInformation(
                    "Cost permissions checked for Azure environment {AzureEnvironmentId}: {HasAccess}",
                    azureEnvironmentId, permissionStatus.HasCostAccess);

                return permissionStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to check cost permissions for Azure environment {AzureEnvironmentId}", 
                    azureEnvironmentId);
                throw;
            }
        }

        public async Task<EnvironmentPermissionStatus> GetEnvironmentPermissionStatusAsync(
            Guid azureEnvironmentId, 
            Guid organizationId)
        {
            try
            {
                var azureEnvironment = await _clientRepository.GetAzureEnvironmentByIdAsync(
                    azureEnvironmentId, organizationId);
                
                if (azureEnvironment == null)
                {
                    throw new ArgumentException($"Azure environment {azureEnvironmentId} not found");
                }

                return new EnvironmentPermissionStatus
                {
                    AzureEnvironmentId = azureEnvironmentId,
                    HasCostManagementAccess = azureEnvironment.HasCostManagementAccess,
                    CostManagementSetupStatus = azureEnvironment.CostManagementSetupStatus ?? "NotTested",
                    LastChecked = azureEnvironment.CostManagementLastChecked,
                    LastError = azureEnvironment.CostManagementLastError,
                    AvailablePermissions = ParseJsonArray(azureEnvironment.AvailablePermissions),
                    MissingPermissions = ParseJsonArray(azureEnvironment.MissingPermissions),
                    SubscriptionIds = azureEnvironment.SubscriptionIds
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to get permission status for Azure environment {AzureEnvironmentId}", 
                    azureEnvironmentId);
                throw;
            }
        }

        public async Task MarkCostManagementReadyAsync(
            Guid azureEnvironmentId, 
            Guid organizationId)
        {
            try
            {
                var azureEnvironment = await _clientRepository.GetAzureEnvironmentByIdAsync(
                    azureEnvironmentId, organizationId);
                
                if (azureEnvironment == null)
                {
                    throw new ArgumentException($"Azure environment {azureEnvironmentId} not found");
                }

                // Run a fresh permission check to confirm
                var clientId = azureEnvironment.ClientId ?? throw new InvalidOperationException(
                    "Azure environment must be associated with a client");
                
                var subscriptionId = azureEnvironment.SubscriptionIds.FirstOrDefault() ?? 
                    throw new InvalidOperationException("No subscriptions found in Azure environment");

                var permissionStatus = await _oauthService.TestCostManagementPermissionsAsync(
                    clientId, organizationId, subscriptionId);

                // Update database
                await UpdateAzureEnvironmentPermissions(azureEnvironment, permissionStatus);

                if (permissionStatus.HasCostAccess)
                {
                    _logger.LogInformation(
                        "Cost management marked as ready for Azure environment {AzureEnvironmentId}", 
                        azureEnvironmentId);
                }
                else
                {
                    _logger.LogWarning(
                        "Attempted to mark cost management ready but permissions still missing for Azure environment {AzureEnvironmentId}", 
                        azureEnvironmentId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to mark cost management ready for Azure environment {AzureEnvironmentId}", 
                    azureEnvironmentId);
                throw;
            }
        }

        public async Task<ClientPermissionMatrix> CheckClientPermissionsAsync(
            Guid clientId, 
            Guid organizationId)
        {
            try
            {
                // Get all Azure environments for the client
                var azureEnvironments = await _clientRepository.GetClientAzureEnvironmentsAsync(
                    clientId, organizationId);

                var clientMatrix = new ClientPermissionMatrix
                {
                    ClientId = clientId,
                    EnvironmentStatuses = new List<EnvironmentPermissionStatus>()
                };

                foreach (var env in azureEnvironments)
                {
                    try
                    {
                        // Check current permissions
                        var status = await CheckAndUpdateCostPermissionsAsync(
                            env.AzureEnvironmentId, organizationId);

                        clientMatrix.EnvironmentStatuses.Add(new EnvironmentPermissionStatus
                        {
                            AzureEnvironmentId = env.AzureEnvironmentId,
                            EnvironmentName = env.Name,
                            HasCostManagementAccess = status.HasCostAccess,
                            CostManagementSetupStatus = status.HasCostAccess ? "Ready" : "SetupRequired",
                            LastChecked = DateTime.UtcNow,
                            SubscriptionIds = env.SubscriptionIds,
                            AvailablePermissions = status.AvailableApis,
                            MissingPermissions = status.MissingPermissions
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            "Failed to check permissions for environment {EnvironmentId}", 
                            env.AzureEnvironmentId);

                        clientMatrix.EnvironmentStatuses.Add(new EnvironmentPermissionStatus
                        {
                            AzureEnvironmentId = env.AzureEnvironmentId,
                            EnvironmentName = env.Name,
                            HasCostManagementAccess = false,
                            CostManagementSetupStatus = "Error",
                            LastError = ex.Message,
                            SubscriptionIds = env.SubscriptionIds
                        });
                    }
                }

                // Calculate overall status
                var totalEnvironments = clientMatrix.EnvironmentStatuses.Count;
                var readyEnvironments = clientMatrix.EnvironmentStatuses.Count(e => e.HasCostManagementAccess);

                clientMatrix.OverallStatus = (readyEnvironments, totalEnvironments) switch
                {
                    (0, _) => "No cost access configured",
                    var (ready, total) when ready == total => "All environments ready",
                    _ => $"{readyEnvironments}/{totalEnvironments} environments ready"
                };

                return clientMatrix;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to check client permissions for client {ClientId}", clientId);
                throw;
            }
        }

        public async Task<CostManagementSetupInstructions> GetCostSetupInstructionsAsync(
            Guid azureEnvironmentId, 
            Guid organizationId)
        {
            try
            {
                var azureEnvironment = await _clientRepository.GetAzureEnvironmentByIdAsync(
                    azureEnvironmentId, organizationId);
                
                if (azureEnvironment == null)
                {
                    throw new ArgumentException($"Azure environment {azureEnvironmentId} not found");
                }

                var clientId = azureEnvironment.ClientId ?? throw new InvalidOperationException(
                    "Azure environment must be associated with a client");
                
                var subscriptionId = azureEnvironment.SubscriptionIds.FirstOrDefault() ?? 
                    throw new InvalidOperationException("No subscriptions found in Azure environment");

                return await _oauthService.GenerateCostManagementSetupAsync(
                    clientId, organizationId, subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to get cost setup instructions for Azure environment {AzureEnvironmentId}", 
                    azureEnvironmentId);
                throw;
            }
        }

        private async Task UpdateAzureEnvironmentPermissions(
            AzureEnvironment azureEnvironment, 
            CostManagementPermissionStatus permissionStatus)
        {
            azureEnvironment.HasCostManagementAccess = permissionStatus.HasCostAccess;
            azureEnvironment.CostManagementLastChecked = DateTime.UtcNow;
            azureEnvironment.CostManagementSetupStatus = permissionStatus.HasCostAccess ? "Ready" : "SetupRequired";
            azureEnvironment.CostManagementLastError = permissionStatus.HasCostAccess ? null : permissionStatus.StatusMessage;
            azureEnvironment.AvailablePermissions = JsonSerializer.Serialize(permissionStatus.AvailableApis);
            azureEnvironment.MissingPermissions = JsonSerializer.Serialize(permissionStatus.MissingPermissions);

            await _clientRepository.UpdateAzureEnvironmentAsync(azureEnvironment);
        }

        private List<string> ParseJsonArray(string? jsonArray)
        {
            if (string.IsNullOrEmpty(jsonArray))
                return new List<string>();

            try
            {
                return JsonSerializer.Deserialize<List<string>>(jsonArray) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
