// Compass.Data/Repositories/ILoginActivityRepository.cs
using Compass.Data.Entities;

namespace Compass.Data.Repositories;

public interface ILoginActivityRepository
{
    Task<LoginActivity> CreateAsync(LoginActivity loginActivity);
    Task<LoginActivity?> GetByIdAsync(Guid loginActivityId);
    Task<LoginActivity?> GetActiveSessionAsync(Guid customerId, string sessionId);
    Task<List<LoginActivity>> GetCustomerLoginHistoryAsync(Guid customerId, int days = 30);
    Task<List<LoginActivity>> GetOrganizationLoginHistoryAsync(Guid organizationId, int days = 30);
    Task<List<LoginActivity>> GetActiveSessionsAsync(Guid customerId);
    Task<LoginActivity> UpdateAsync(LoginActivity loginActivity);
    Task<bool> RevokeSessionAsync(Guid loginActivityId);
    Task<bool> RevokeAllSessionsAsync(Guid customerId, Guid? exceptSessionId = null);
    Task<int> CleanupExpiredSessionsAsync(int daysOld = 90);
    Task<bool> MarkSuspiciousActivityAsync(Guid loginActivityId, string notes);
    Task<List<LoginActivity>> GetSuspiciousActivityAsync(Guid organizationId, int days = 7);
}