using Compass.core.Interfaces;
using Compass.Core.Interfaces;
using Compass.Core.Services;
using Compass.Data;
using Compass.Data.Interfaces;
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
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IClientPreferencesRepository, ClientPreferencesRepository>();

        // Core Services - Enhanced Analysis Engine
        services.AddScoped<IAzureResourceGraphService, AzureResourceGraphService>();
        services.AddScoped<INamingConventionAnalyzer, NamingConventionAnalyzer>();
        services.AddScoped<ITaggingAnalyzer, TaggingAnalyzer>();
        services.AddScoped<IDependencyAnalyzer, DependencyAnalyzer>();
        services.AddScoped<IBusinessContinuityAssessmentAnalyzer, BusinessContinuityAssessmentAnalyzer>();
        services.AddScoped<ISecurityPostureAssessmentAnalyzer, SecurityPostureAssessmentAnalyzer>();

        // NEW: Sprint 6 - Identity Access Management Assessment
        services.AddScoped<IIdentityAccessAssessmentAnalyzer, IdentityAccessAssessmentAnalyzer>();
        services.AddScoped<IAssessmentOrchestrator, AssessmentOrchestrator>();

        // OAuth and Microsoft Graph services
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();

        // Other services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ILicenseValidationService, LicenseValidationService>();
        services.AddScoped<IUsageTrackingService, UsageTrackingService>();

        return services;
    }

    public static IServiceCollection AddAzureServices(this IServiceCollection services)
    {
        // Azure services are registered in AddCompassServices
        // The AzureResourceGraphService uses DefaultAzureCredential for authentication
        return services;
    }
}