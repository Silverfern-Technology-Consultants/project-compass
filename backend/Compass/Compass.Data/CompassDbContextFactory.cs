// Compass.Data/CompassDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;

namespace Compass.Data;

public class CompassDbContextFactory : IDesignTimeDbContextFactory<CompassDbContext>
{
    public CompassDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CompassDbContext>();

        // Build configuration to read from Azure Key Vault (like your Program.cs)
        var configBuilder = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<CompassDbContextFactory>(optional: true);

        // Add Azure Key Vault - using the correct vault name and URI format
        var keyVaultName = "kv-dev-7yu4s2pu";
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        configBuilder.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());

        var configuration = configBuilder.Build();

        // Try to get connection string from Key Vault first, then fall back to user secrets
        var connectionString = configuration["compass-database-connection"] ??
                              configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string not found. " +
                "Please ensure 'compass-database-connection' is available in Azure Key Vault or " +
                "set 'ConnectionStrings:DefaultConnection' in user secrets for local development."
            );
        }

        optionsBuilder.UseSqlServer(connectionString);

        return new CompassDbContext(optionsBuilder.Options);
    }
}