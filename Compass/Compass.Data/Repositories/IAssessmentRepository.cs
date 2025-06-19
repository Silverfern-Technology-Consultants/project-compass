using Compass.Core.Models;

namespace Compass.Data.Repositories
{
    public interface IAssessmentRepository
    {
        Task<Assessment?> GetByIdAsync(Guid id);
        Task<IEnumerable<Assessment>> GetByCustomerIdAsync(Guid customerId);
        Task<Assessment> CreateAsync(Assessment assessment);
        Task<Assessment> UpdateAsync(Assessment assessment);
        Task DeleteAsync(Guid id);

        // Finding operations
        Task<AssessmentFinding> AddFindingAsync(AssessmentFinding finding);
        Task<IEnumerable<AssessmentFinding>> GetFindingsByAssessmentIdAsync(Guid assessmentId);
        Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status);
    }
}