// Compass.Data/Repositories/ISubscriptionRepository.cs
using Compass.Data.Entities;

namespace Compass.Data.Repositories;

public interface ISubscriptionRepository
{
    Task<Subscription> GetByIdAsync(Guid subscriptionId);
    Task<Subscription> GetActiveByCustomerIdAsync(Guid customerId);
    Task<Subscription?> GetActiveByOrganizationIdAsync(Guid organizationId);
    Task<IEnumerable<Subscription>> GetByCustomerIdAsync(Guid customerId);
    Task<IEnumerable<Subscription>> GetByOrganizationIdAsync(Guid organizationId);
    Task<Subscription> CreateAsync(Subscription subscription);
    Task<Subscription> UpdateAsync(Subscription subscription);
    Task DeleteAsync(Guid subscriptionId);
    Task<IEnumerable<Subscription>> GetExpiringSubscriptionsAsync(DateTime date);
    Task<IEnumerable<Subscription>> GetByStatusAsync(string status);
}