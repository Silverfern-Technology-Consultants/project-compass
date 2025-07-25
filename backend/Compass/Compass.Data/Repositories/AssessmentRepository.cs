﻿using Compass.Data.Entities;
using Compass.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Compass.Data.Repositories;

public class AssessmentRepository : IAssessmentRepository
{
    private readonly CompassDbContext _context;

    public AssessmentRepository(CompassDbContext context)
    {
        _context = context;
    }

    // Core CRUD operations
    public async Task<Assessment?> GetByIdAsync(Guid id)
    {
        return await _context.Assessments
            .Include(a => a.Findings)
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Assessment> CreateAsync(Assessment assessment)
    {
        _context.Assessments.Add(assessment);
        await _context.SaveChangesAsync();
        return assessment;
    }

    public async Task UpdateAsync(Assessment assessment)
    {
        _context.Assessments.Update(assessment);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid assessmentId)
    {
        var assessment = await _context.Assessments
            .Include(a => a.Findings)
            .Include(a => a.Resources)
            .FirstOrDefaultAsync(a => a.Id == assessmentId);

        if (assessment != null)
        {
            if (assessment.Findings.Any())
            {
                _context.AssessmentFindings.RemoveRange(assessment.Findings);
            }

            if (assessment.Resources.Any())
            {
                _context.AssessmentResources.RemoveRange(assessment.Resources);
            }

            _context.Assessments.Remove(assessment);
            await _context.SaveChangesAsync();
        }
    }

    // Query methods
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

    // NEW: Category-based filtering methods for Sprint 6
    public async Task<List<Assessment>> GetByOrganizationAndCategoryAsync(Guid organizationId, string category, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.OrganizationId == organizationId && a.AssessmentCategory == category)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Assessment>> GetByClientAndCategoryAsync(Guid clientId, string category, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.ClientId == clientId && a.AssessmentCategory == category)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<Assessment>> GetByOrganizationCategoryAndTypeAsync(Guid organizationId, string category, string assessmentType, int limit = 10)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Organization)
            .Include(a => a.Client)
            .Where(a => a.OrganizationId == organizationId &&
                       a.AssessmentCategory == category &&
                       a.AssessmentType == assessmentType)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetAssessmentCountsByCategoryAsync(Guid organizationId)
    {
        return await _context.Assessments
            .Where(a => a.OrganizationId == organizationId)
            .GroupBy(a => a.AssessmentCategory)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    public async Task<Dictionary<string, int>> GetAssessmentCountsByTypeAsync(Guid organizationId, string category)
    {
        return await _context.Assessments
            .Where(a => a.OrganizationId == organizationId && a.AssessmentCategory == category)
            .GroupBy(a => a.AssessmentType)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    // Client-specific methods
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

    // NEW: Category-specific client methods
    public async Task<int> GetAssessmentCountByClientAndCategoryAsync(Guid clientId, string category)
    {
        return await _context.Assessments
            .CountAsync(a => a.ClientId == clientId && a.AssessmentCategory == category);
    }

    public async Task<List<Assessment>> GetRecentAssessmentsByClientAndCategoryAsync(Guid clientId, string category, int limit = 5)
    {
        return await _context.Assessments
            .Include(a => a.Customer)
            .Include(a => a.Client)
            .Where(a => a.ClientId == clientId && a.AssessmentCategory == category)
            .OrderByDescending(a => a.StartedDate)
            .Take(limit)
            .ToListAsync();
    }

    // Status and update methods
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

    // Findings methods
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

    // NEW: Category-based findings methods
    public async Task<List<AssessmentFinding>> GetFindingsByAssessmentAndCategoryAsync(Guid assessmentId, string category)
    {
        return await _context.AssessmentFindings
            .Where(f => f.AssessmentId == assessmentId && f.Category == category)
            .OrderBy(f => f.Severity)
            .ThenBy(f => f.ResourceName)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetFindingCountsByCategoryAsync(Guid assessmentId)
    {
        return await _context.AssessmentFindings
            .Where(f => f.AssessmentId == assessmentId)
            .GroupBy(f => f.Category)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    // Resources methods
    public async Task CreateResourcesAsync(List<AssessmentResource> resources)
    {
        if (!resources.Any()) return;

        _context.AssessmentResources.AddRange(resources);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AssessmentResource>> GetResourcesByAssessmentIdAsync(
        Guid assessmentId,
        int page = 1,
        int limit = 50,
        string? resourceType = null,
        string? resourceGroup = null,
        string? location = null,
        string? environmentFilter = null,
        string? search = null)
    {
        var query = _context.AssessmentResources
            .Where(r => r.AssessmentId == assessmentId);

        // Apply filters
        if (!string.IsNullOrEmpty(resourceType))
        {
            query = query.Where(r => r.ResourceTypeName == resourceType);
        }

        if (!string.IsNullOrEmpty(resourceGroup))
        {
            query = query.Where(r => r.ResourceGroup == resourceGroup);
        }

        if (!string.IsNullOrEmpty(location))
        {
            query = query.Where(r => r.Location == location);
        }

        if (!string.IsNullOrEmpty(environmentFilter))
        {
            query = query.Where(r => r.Environment == environmentFilter);
        }

        if (!string.IsNullOrEmpty(search))
        {
            var searchLower = search.ToLowerInvariant();
            query = query.Where(r =>
                r.Name.ToLower().Contains(searchLower) ||
                r.ResourceTypeName.ToLower().Contains(searchLower) ||
                (r.ResourceGroup != null && r.ResourceGroup.ToLower().Contains(searchLower)));
        }

        // Apply pagination and ordering
        return await query
            .OrderBy(r => r.ResourceTypeName)
            .ThenBy(r => r.Name)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetResourceCountByAssessmentIdAsync(Guid assessmentId)
    {
        return await _context.AssessmentResources
            .CountAsync(r => r.AssessmentId == assessmentId);
    }

    public async Task<Dictionary<string, string>> GetResourceFiltersByAssessmentIdAsync(Guid assessmentId)
    {
        var resources = await _context.AssessmentResources
            .Where(r => r.AssessmentId == assessmentId)
            .ToListAsync();

        var filters = new Dictionary<string, string>();

        // Resource Types filter
        var resourceTypes = resources
            .GroupBy(r => r.ResourceTypeName)
            .ToDictionary(g => g.Key, g => g.Count());
        filters["ResourceTypes"] = JsonSerializer.Serialize(resourceTypes);

        // Resource Groups filter
        var resourceGroups = resources
            .Where(r => !string.IsNullOrEmpty(r.ResourceGroup))
            .GroupBy(r => r.ResourceGroup!)
            .ToDictionary(g => g.Key, g => g.Count());
        filters["ResourceGroups"] = JsonSerializer.Serialize(resourceGroups);

        // Locations filter
        var locations = resources
            .Where(r => !string.IsNullOrEmpty(r.Location))
            .GroupBy(r => r.Location!)
            .ToDictionary(g => g.Key, g => g.Count());
        filters["Locations"] = JsonSerializer.Serialize(locations);

        // Environments filter
        var environments = resources
            .Where(r => !string.IsNullOrEmpty(r.Environment))
            .GroupBy(r => r.Environment!)
            .ToDictionary(g => g.Key, g => g.Count());
        filters["Environments"] = JsonSerializer.Serialize(environments);

        return filters;
    }

    public async Task<List<AssessmentResource>> GetAllResourcesByAssessmentIdAsync(Guid assessmentId)
    {
        return await _context.AssessmentResources
            .Where(r => r.AssessmentId == assessmentId)
            .OrderBy(r => r.ResourceTypeName)
            .ThenBy(r => r.Name)
            .ToListAsync();
    }

    // Environment lookup method for OAuth support
    public async Task<AzureEnvironment?> GetEnvironmentByIdAsync(Guid environmentId)
    {
        return await _context.AzureEnvironments
            .Include(e => e.Client)
            .FirstOrDefaultAsync(e => e.AzureEnvironmentId == environmentId);
    }
}