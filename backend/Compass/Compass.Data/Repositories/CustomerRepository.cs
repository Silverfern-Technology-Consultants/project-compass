// Compass.Data/Repositories/CustomerRepository.cs
using Compass.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compass.Data.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly CompassDbContext _context;

    public CustomerRepository(CompassDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByIdAsync(Guid customerId)
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .Include(c => c.Assessments)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant());
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        return await _context.Customers
            .Include(c => c.Subscriptions)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();
    }

    public async Task<Customer> CreateAsync(Customer customer)
    {
        customer.CreatedDate = DateTime.UtcNow;
        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task<Customer> UpdateAsync(Customer customer)
    {
        _context.Customers.Update(customer);
        await _context.SaveChangesAsync();
        return customer;
    }

    public async Task DeleteAsync(Guid customerId)
    {
        var customer = await GetByIdAsync(customerId);
        if (customer != null)
        {
            customer.IsActive = false; // Soft delete
            await UpdateAsync(customer);
        }
    }

    public async Task<bool> ExistsAsync(Guid customerId)
    {
        return await _context.Customers
            .AnyAsync(c => c.CustomerId == customerId && c.IsActive);
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Customers
            .AnyAsync(c => c.Email == email.ToLowerInvariant() && c.IsActive);
    }
}