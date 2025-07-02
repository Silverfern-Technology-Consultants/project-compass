using Compass.Data.Entities;

namespace Compass.Data.Repositories;

public interface IAssessmentRepository
{
    // Core CRUD operations
    Task<Assessment?> GetByIdAsync(Guid id);
    Task<Assessment> CreateAsync(Assessment assessment);
    Task UpdateAsync(Assessment assessment);
    Task DeleteAsync(Guid assessmentId);

    // Query methods
    Task<List<Assessment>> GetByEnvironmentIdAsync(Guid environmentId, int limit = 10);
    Task<List<Assessment>> GetByCustomerIdAsync(Guid customerId, int limit = 10);
    Task<List<Assessment>> GetByOrganizationIdAsync(Guid organizationId, int limit = 10);
    Task<Assessment?> GetByIdAndOrganizationAsync(Guid assessmentId, Guid organizationId);
    Task<List<Assessment>> GetByClientIdAsync(Guid clientId, int limit = 10);
    Task<Assessment?> GetByIdAndClientAsync(Guid assessmentId, Guid clientId);
    Task<List<Assessment>> GetByClientAndOrganizationAsync(Guid clientId, Guid organizationId, int limit = 10);
    Task<List<Assessment>> GetPendingAssessmentsAsync();

    // Client-specific methods
    Task<int> GetAssessmentCountByClientAsync(Guid clientId);
    Task<int> GetCompletedAssessmentCountByClientAsync(Guid clientId);
    Task<List<Assessment>> GetRecentAssessmentsByClientAsync(Guid clientId, int limit = 5);

    // Status and update methods
    Task UpdateStatusAsync(Guid assessmentId, string status);
    Task UpdateAssessmentAsync(Guid assessmentId, decimal score, string status, DateTime completedDate);

    // Findings methods
    Task<List<AssessmentFinding>> GetFindingsByAssessmentIdAsync(Guid assessmentId);
    Task CreateFindingsAsync(List<AssessmentFinding> findings);
    Task UpdateFindingStatusAsync(Guid findingId, string status);

    // Resources methods
    Task CreateResourcesAsync(List<AssessmentResource> resources);
    Task<List<AssessmentResource>> GetResourcesByAssessmentIdAsync(
        Guid assessmentId,
        int page = 1,
        int limit = 50,
        string? resourceType = null,
        string? resourceGroup = null,
        string? location = null,
        string? environmentFilter = null,
        string? search = null);
    Task<int> GetResourceCountByAssessmentIdAsync(Guid assessmentId);
    Task<Dictionary<string, string>> GetResourceFiltersByAssessmentIdAsync(Guid assessmentId);
    Task<List<AssessmentResource>> GetAllResourcesByAssessmentIdAsync(Guid assessmentId);

    // NEW: Environment lookup method for OAuth support
    Task<AzureEnvironment?> GetEnvironmentByIdAsync(Guid environmentId);
}