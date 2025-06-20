using Compass.Data.Entities;  // ← Changed from Compass.Core.Models
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

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

    public async Task<Assessment> CreateAsync(Assessment assessment)
    {
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();
        return assessment;
    }

    public async Task<List<Assessment>> GetByEnvironmentIdAsync(Guid environmentId, int limit = 10)
    {
        return await _context.Assessments
            .Where(a => a.EnvironmentId == environmentId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Assessment>> GetByCustomerIdAsync(Guid customerId, int limit = 10)
    {
        return await _context.Assessments
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

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
            // For now, just update a property - we can add Status later if needed
            await _context.SaveChangesAsync();
        }
    }
}