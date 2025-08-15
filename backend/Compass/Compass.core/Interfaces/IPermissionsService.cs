using Compass.Core.Models;

namespace Compass.Core.Interfaces
{
    public interface IPermissionsService
    {
        /// <summary>
        /// Checks and updates cost management permissions for an Azure environment
        /// </summary>
        Task<CostManagementPermissionStatus> CheckAndUpdateCostPermissionsAsync(
            Guid azureEnvironmentId, 
            Guid organizationId);

        /// <summary>
        /// Gets the current permission status for an Azure environment from database
        /// </summary>
        Task<EnvironmentPermissionStatus> GetEnvironmentPermissionStatusAsync(
            Guid azureEnvironmentId, 
            Guid organizationId);

        /// <summary>
        /// Marks cost management as ready for an Azure environment
        /// </summary>
        Task MarkCostManagementReadyAsync(
            Guid azureEnvironmentId, 
            Guid organizationId);

        /// <summary>
        /// Runs comprehensive permissions check across all environments for a client
        /// </summary>
        Task<ClientPermissionMatrix> CheckClientPermissionsAsync(
            Guid clientId, 
            Guid organizationId);

        /// <summary>
        /// Gets setup instructions for cost management access
        /// </summary>
        Task<CostManagementSetupInstructions> GetCostSetupInstructionsAsync(
            Guid azureEnvironmentId, 
            Guid organizationId);
    }
}
