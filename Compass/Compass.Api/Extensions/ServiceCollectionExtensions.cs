using Compass.Core.Services;
using Compass.Data;
using Compass.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Compass.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCompassServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database
        services.AddDbContext<CompassDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        // Repositories
        services.AddScoped<IAssessmentRepository, AssessmentRepository>();

        // Core Services
        services.AddScoped<IAzureResourceGraphService, AzureResourceGraphService>();
        services.AddScoped<INamingConventionAnalyzer, NamingConventionAnalyzer>();
        services.AddScoped<ITaggingAnalyzer, TaggingAnalyzer>();
        services.AddScoped<IAssessmentOrchestrator, AssessmentOrchestrator>();

        return services;
    }

    public static IServiceCollection AddAzureServices(this IServiceCollection services)
    {
        // Azure services are registered in AddCompassServices
        // The AzureResourceGraphService uses DefaultAzureCredential for authentication
        return services;
    }
}