using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public interface IAssessmentRepository
{
    // Existing methods
    Task<Assessment?> GetByIdAsync(Guid id);
    Task<Assessment> CreateAsync(Assessment assessment);
    Task<List<Assessment>> GetByEnvironmentIdAsync(Guid environmentId, int limit = 10);
    Task<List<Assessment>> GetByCustomerIdAsync(Guid customerId, int limit = 10);
    Task<List<Assessment>> GetByOrganizationIdAsync(Guid organizationId, int limit = 10);
    Task<Assessment?> GetByIdAndOrganizationAsync(Guid assessmentId, Guid organizationId);
    Task UpdateAsync(Assessment assessment);
    Task UpdateStatusAsync(Guid assessmentId, string status);
    Task UpdateAssessmentAsync(Guid assessmentId, decimal score, string status, DateTime completedDate);
    Task<List<Assessment>> GetPendingAssessmentsAsync();
    Task<List<AssessmentFinding>> GetFindingsByAssessmentIdAsync(Guid assessmentId);
    Task CreateFindingsAsync(List<AssessmentFinding> findings);
    Task UpdateFindingStatusAsync(Guid findingId, string status);
    Task DeleteAsync(Guid assessmentId);

    // NEW: Client-scoped methods
    Task<List<Assessment>> GetByClientIdAsync(Guid clientId, int limit = 10);
    Task<Assessment?> GetByIdAndClientAsync(Guid assessmentId, Guid clientId);
    Task<List<Assessment>> GetByClientAndOrganizationAsync(Guid clientId, Guid organizationId, int limit = 10);
    Task<int> GetAssessmentCountByClientAsync(Guid clientId);
    Task<int> GetCompletedAssessmentCountByClientAsync(Guid clientId);
    Task<List<Assessment>> GetRecentAssessmentsByClientAsync(Guid clientId, int limit = 5);
}

// Updated AssessmentRepository.cs implementation
