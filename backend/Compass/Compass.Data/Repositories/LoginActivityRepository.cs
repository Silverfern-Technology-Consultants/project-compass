// Compass.Data/Repositories/LoginActivityRepository.cs
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class LoginActivityRepository : ILoginActivityRepository
{
    private readonly CompassDbContext _context;

    public LoginActivityRepository(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<LoginActivity> CreateAsync(LoginActivity loginActivity)
    {
        _context.LoginActivities.Add(loginActivity);
        await _context.SaveChangesAsync();
        return loginActivity;
    }

    public async Task<LoginActivity?> GetByIdAsync(Guid loginActivityId)
    {
        return await _context.LoginActivities
            .Include(la => la.Customer)
            .FirstOrDefaultAsync(la => la.LoginActivityId == loginActivityId);
    }

    public async Task<LoginActivity?> GetActiveSessionAsync(Guid customerId, string sessionId)
    {
        return await _context.LoginActivities
            .FirstOrDefaultAsync(la => la.CustomerId == customerId
                                    && la.SessionId == sessionId
                                    && la.IsActive
                                    && la.Status == "Active");
    }

    public async Task<List<LoginActivity>> GetCustomerLoginHistoryAsync(Guid customerId, int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        return await _context.LoginActivities
            .Where(la => la.CustomerId == customerId && la.LoginTime >= cutoffDate)
            .OrderByDescending(la => la.LoginTime)
            .ToListAsync();
    }

    public async Task<List<LoginActivity>> GetOrganizationLoginHistoryAsync(Guid organizationId, int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        return await _context.LoginActivities
            .Include(la => la.Customer)
            .Where(la => la.Customer.OrganizationId == organizationId && la.LoginTime >= cutoffDate)
            .OrderByDescending(la => la.LoginTime)
            .ToListAsync();
    }

    public async Task<List<LoginActivity>> GetActiveSessionsAsync(Guid customerId)
    {
        return await _context.LoginActivities
            .Where(la => la.CustomerId == customerId
                      && la.IsActive
                      && la.Status == "Active"
                      && la.LogoutTime == null)
            .OrderByDescending(la => la.LastActivityTime ?? la.LoginTime)
            .ToListAsync();
    }

    public async Task<LoginActivity> UpdateAsync(LoginActivity loginActivity)
    {
        _context.LoginActivities.Update(loginActivity);
        await _context.SaveChangesAsync();
        return loginActivity;
    }

    public async Task<bool> RevokeSessionAsync(Guid loginActivityId)
    {
        var session = await GetByIdAsync(loginActivityId);
        if (session == null) return false;

        session.Status = "Revoked";
        session.IsActive = false;
        session.LogoutTime = DateTime.UtcNow;

        await UpdateAsync(session);
        return true;
    }

    public async Task<bool> RevokeAllSessionsAsync(Guid customerId, Guid? exceptSessionId = null)
    {
        var sessions = await _context.LoginActivities
            .Where(la => la.CustomerId == customerId
                      && la.IsActive
                      && la.Status == "Active"
                      && (exceptSessionId == null || la.LoginActivityId != exceptSessionId))
            .ToListAsync();

        if (!sessions.Any()) return false;

        foreach (var session in sessions)
        {
            session.Status = "Revoked";
            session.IsActive = false;
            session.LogoutTime = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> CleanupExpiredSessionsAsync(int daysOld = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

        var expiredSessions = await _context.LoginActivities
            .Where(la => la.LoginTime < cutoffDate)
            .ToListAsync();

        if (!expiredSessions.Any()) return 0;

        _context.LoginActivities.RemoveRange(expiredSessions);
        await _context.SaveChangesAsync();

        return expiredSessions.Count;
    }

    public async Task<bool> MarkSuspiciousActivityAsync(Guid loginActivityId, string notes)
    {
        var session = await GetByIdAsync(loginActivityId);
        if (session == null) return false;

        session.SuspiciousActivity = true;
        session.SecurityNotes = notes;

        await UpdateAsync(session);
        return true;
    }

    public async Task<List<LoginActivity>> GetSuspiciousActivityAsync(Guid organizationId, int days = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);

        return await _context.LoginActivities
            .Include(la => la.Customer)
            .Where(la => la.Customer.OrganizationId == organizationId
                      && la.SuspiciousActivity
                      && la.LoginTime >= cutoffDate)
            .OrderByDescending(la => la.LoginTime)
            .ToListAsync();
    }
}