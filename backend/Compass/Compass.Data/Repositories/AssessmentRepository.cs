using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class AssessmentRepository : IAssessmentRepository
{
    private readonly CompassDbContext _context;

    public AssessmentRepository(CompassDbContext context)
    {
        _context = context;
    }

    // Existing methods remain the same...
    public async Task<Assessment?> GetByIdAsync(Guid id)
    {
        return await _context.Assessments
            .Include(a => a.Findings)
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client) // Include client information
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Assessment> CreateAsync(Assessment assessment)
    {
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();
        return assessment;
    }

    public async Task<List<Assessment>> GetByEnvironmentIdAsync(Guid environmentId, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.EnvironmentId == environmentId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Assessment>> GetByCustomerIdAsync(Guid customerId, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Assessment>> GetByOrganizationIdAsync(Guid organizationId, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.OrganizationId == organizationId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Assessment?> GetByIdAndOrganizationAsync(Guid assessmentId, Guid organizationId)
    {
        return await _context.Assessments
            .Include(a => a.Findings)
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.Id == assessmentId && a.OrganizationId == organizationId)
            .FirstOrDefaultAsync();
    }

    // NEW: Client-scoped methods
    public async Task<List<Assessment>> GetByClientIdAsync(Guid clientId, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Assessment?> GetByIdAndClientAsync(Guid assessmentId, Guid clientId)
    {
        return await _context.Assessments
            .Include(a => a.Findings)
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.Id == assessmentId && a.ClientId == clientId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Assessment>> GetByClientAndOrganizationAsync(Guid clientId, Guid organizationId, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.ClientId == clientId && a.OrganizationId == organizationId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetAssessmentCountByClientAsync(Guid clientId)
    {
        return await _context.Assessments
            .CountAsync(a => a.ClientId == clientId);
    }

    public async Task<int> GetCompletedAssessmentCountByClientAsync(Guid clientId)
    {
        return await _context.Assessments
            .CountAsync(a => a.ClientId == clientId && a.Status == "Completed");
    }

    public async Task<List<Assessment>> GetRecentAssessmentsByClientAsync(Guid clientId, int limit = 5)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    // Existing update/delete methods remain the same...
    public async Task UpdateAsync(Assessment assessment)
    {
        _context.Assessments.Update(assessment);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid assessmentId, string status)
    {
        var assessment = await _context.Assessments.FindAsync(assessmentId);
        if (assessment != null)
        {
            assessment.Status = status;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateAssessmentAsync(Guid assessmentId, decimal score, string status, DateTime completedDate)
    {
        var assessment = await _context.Assessments.FindAsync(assessmentId);
        if (assessment != null)
        {
            assessment.OverallScore = score;
            assessment.Status = status;
            assessment.CompletedDate = completedDate;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<Assessment>> GetPendingAssessmentsAsync()
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.Status == "Pending" || a.Status == "InProgress")
            .OrderBy(a => a.StartedDate)
            .ToListAsync();
    }

    public async Task<List<AssessmentFinding>> GetFindingsByAssessmentIdAsync(Guid assessmentId)
    {
        return await _context.AssessmentFindings
            .Where(f => f.AssessmentId == assessmentId)
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.Category)
            .ToListAsync();
    }

    public async Task CreateFindingsAsync(List<AssessmentFinding> findings)
    {
        _context.AssessmentFindings.AddRange(findings);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateFindingStatusAsync(Guid findingId, string status)
    {
        var finding = await _context.AssessmentFindings.FindAsync(findingId);
        if (finding != null)
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(Guid assessmentId)
    {
        var assessment = await _context.Assessments
            .Include(a => a.Findings)
            .FirstOrDefaultAsync(a => a.Id == assessmentId);

        if (assessment != null)
        {
            if (assessment.Findings.Any())
            {
                _context.AssessmentFindings.RemoveRange(assessment.Findings);
            }

            _context.Assessments.Remove(assessment);
            await _context.SaveChangesAsync();
        }
    }
}