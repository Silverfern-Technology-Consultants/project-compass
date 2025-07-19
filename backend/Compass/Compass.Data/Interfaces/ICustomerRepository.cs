// Compass.Data/Repositories/ICustomerRepository.cs

// Compass.Data/Repositories/ICustomerRepository.cs
using Compass.Data.Entities;
using Compass.Data.Repositories;

namespace Compass.Data.Interfaces;

public interface ICustomerRepository
{
    // Existing methods
    Task<Customer?> GetByIdAsync(Guid customerId);
    Task<Customer?> GetByEmailAsync(string email);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer> CreateAsync(Customer customer);
    Task<Customer> UpdateAsync(Customer customer);
    Task DeleteAsync(Guid customerId);
    Task<bool> ExistsAsync(Guid customerId);
    Task<bool> EmailExistsAsync(string email);

    // NEW: Enhanced account management methods
    Task<bool> DeactivateAccountAsync(Guid customerId, string reason = "");
    Task<bool> ReactivateAccountAsync(Guid customerId);
    Task<bool> RemoveFromOrganizationAsync(Guid customerId);
    Task<bool> HasOtherOrganizationTiesAsync(string email, Guid? excludeOrganizationId = null);
    Task<bool> HasCreatedContentAsync(Guid customerId);
    Task<bool> TransferOwnershipAsync(Guid fromCustomerId, Guid toCustomerId);
    Task<CustomerAccountInfo?> GetAccountInfoAsync(Guid customerId);
    Task<List<Customer>> GetOrganizationMembersAsync(Guid organizationId);
    Task<bool> CanUserBeRemovedAsync(Guid customerId, Guid organizationId);
}