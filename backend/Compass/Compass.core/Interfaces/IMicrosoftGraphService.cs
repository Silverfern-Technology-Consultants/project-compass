using Compass.Core.Models;
using Compass.Core.Models.Assessment;

namespace Compass.Core.Interfaces
{
    public interface IMicrosoftGraphService
    {
        // User and Group Analysis
        Task<List<Microsoft.Graph.Models.User>> GetUsersAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.Group>> GetGroupsAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.User>> GetInactiveUsersAsync(Guid clientId, Guid organizationId, int daysSinceLastSignIn = 90);
        Task<List<Microsoft.Graph.Models.User>> GetPrivilegedUsersAsync(Guid clientId, Guid organizationId);

        // Application and Service Principal Analysis
        Task<List<Microsoft.Graph.Models.Application>> GetApplicationsAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.ServicePrincipal>> GetServicePrincipalsAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.Application>> GetApplicationsWithExpiredCredentialsAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.ServicePrincipal>> GetOverprivilegedServicePrincipalsAsync(Guid clientId, Guid organizationId);

        // Conditional Access and Security Policies
        Task<List<Microsoft.Graph.Models.ConditionalAccessPolicy>> GetConditionalAccessPoliciesAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.User>> GetUsersNotCoveredByMfaAsync(Guid clientId, Guid organizationId);
        Task<ConditionalAccessCoverageReport> AnalyzeConditionalAccessCoverageAsync(Guid clientId, Guid organizationId);

        // Device Management
        Task<List<Microsoft.Graph.Models.Device>> GetDevicesAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.Device>> GetNonCompliantDevicesAsync(Guid clientId, Guid organizationId);

        // Security and Risk Assessment  
        Task<List<Microsoft.Graph.Models.RiskyUser>> GetRiskyUsersAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.SignIn>> GetFailedSignInsAsync(Guid clientId, Guid organizationId, DateTime since);
        // Note: SecurityScore might not exist in current SDK - using custom model
        Task<GraphSecurityScore> GetSecurityScoreAsync(Guid clientId, Guid organizationId);

        // Directory Roles and Permissions
        Task<List<Microsoft.Graph.Models.DirectoryRole>> GetDirectoryRolesAsync(Guid clientId, Guid organizationId);
        Task<List<Microsoft.Graph.Models.User>> GetUsersInRoleAsync(Guid clientId, Guid organizationId, string roleId);
        Task<RoleAssignmentReport> AnalyzeRoleAssignmentsAsync(Guid clientId, Guid organizationId);

        // Test Graph connectivity
        Task<bool> TestGraphConnectionAsync(Guid clientId, Guid organizationId);
    }
}