using Compass.Data.Entities;  // ← Changed from Compass.Core.Models

namespace Compass.Data.Repositories;

public interface IAssessmentRepository
{
    Task<Assessment?> GetByIdAsync(Guid id);
    Task<Assessment> CreateAsync(Assessment assessment);
    Task<List<Assessment>> GetByEnvironmentIdAsync(Guid environmentId, int limit = 10);
    Task<List<Assessment>> GetByCustomerIdAsync(Guid customerId, int limit = 10);
    Task UpdateAsync(Assessment assessment);
    Task UpdateStatusAsync(Guid assessmentId, string status);
    Task UpdateAssessmentAsync(Guid assessmentId, decimal score, string status, DateTime completedDate);
    Task<List<Assessment>> GetPendingAssessmentsAsync();
    Task<List<AssessmentFinding>> GetFindingsByAssessmentIdAsync(Guid assessmentId);
    Task CreateFindingsAsync(List<AssessmentFinding> findings);
    Task UpdateFindingStatusAsync(Guid findingId, string status); // Removed FindingStatus enum for now
}