using Microsoft.EntityFrameworkCore;
using Compass.Core.Models;

namespace Compass.Data.Repositories
{
    public class AssessmentRepository : IAssessmentRepository
    {
        private readonly CompassDbContext _context;

        public AssessmentRepository(CompassDbContext context)
        {
            _context = context;
        }

        public async Task<Assessment?> GetByIdAsync(Guid id)
        {
            return await _context.Assessments
                .Include(a => a.Findings)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<IEnumerable<Assessment>> GetByCustomerIdAsync(Guid customerId)
        {
            return await _context.Assessments
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.StartedDate)
                .ToListAsync();
        }

        public async Task<Assessment> CreateAsync(Assessment assessment)
        {
            assessment.Id = Guid.NewGuid();
            assessment.StartedDate = DateTime.UtcNow;

            _context.Assessments.Add(assessment);
            await _context.SaveChangesAsync();
            return assessment;
        }

        public async Task<Assessment> UpdateAsync(Assessment assessment)
        {
            _context.Assessments.Update(assessment);
            await _context.SaveChangesAsync();
            return assessment;
        }

        public async Task DeleteAsync(Guid id)
        {
            var assessment = await _context.Assessments.FindAsync(id);
            if (assessment != null)
            {
                _context.Assessments.Remove(assessment);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<AssessmentFinding> AddFindingAsync(AssessmentFinding finding)
        {
            finding.Id = Guid.NewGuid();
            finding.CreatedDate = DateTime.UtcNow;
            finding.Status = FindingStatus.New;

            _context.AssessmentFindings.Add(finding);
            await _context.SaveChangesAsync();
            return finding;
        }

        public async Task<IEnumerable<AssessmentFinding>> GetFindingsByAssessmentIdAsync(Guid assessmentId)
        {
            return await _context.AssessmentFindings
                .Where(f => f.AssessmentId == assessmentId)
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.Category)
                .ToListAsync();
        }

        public async Task UpdateFindingStatusAsync(Guid findingId, FindingStatus status)
        {
            var finding = await _context.AssessmentFindings.FindAsync(findingId);
            if (finding != null)
            {
                finding.Status = status;
                await _context.SaveChangesAsync();
            }
        }
    }
}