// Compass.Data/Repositories/ICustomerRepository.cs
using Compass.Data.Entities;

namespace Compass.Data.Repositories;

public interface ICustomerRepository
{
    Task<Customer> GetByIdAsync(Guid customerId);
    Task<Customer> GetByEmailAsync(string email);
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer> CreateAsync(Customer customer);
    Task<Customer> UpdateAsync(Customer customer);
    Task DeleteAsync(Guid customerId);
    Task<bool> ExistsAsync(Guid customerId);
    Task<bool> EmailExistsAsync(string email);
}