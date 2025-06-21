// Compass.Data/CompassDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Compass.Data;

public class CompassDbContextFactory : IDesignTimeDbContextFactory<CompassDbContext>
{
    public CompassDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CompassDbContext>();

        // Build configuration to read from user secrets
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CompassDbContextFactory>(optional: true)
            .Build();

        // Get connection string from user secrets
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' not found. " +
                "Please set it using: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<your-connection-string>\""
            );
        }

        optionsBuilder.UseSqlServer(connectionString);

        return new CompassDbContext(optionsBuilder.Options);
    }
}