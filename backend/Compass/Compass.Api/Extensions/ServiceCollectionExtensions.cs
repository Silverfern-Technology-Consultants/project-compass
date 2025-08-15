using Compass.core.Interfaces;
using Compass.Core.Interfaces;
using Compass.Core.Services;
using Compass.Core.Services.Identity;
using Compass.Core.Services.BusinessContinuity;
using Compass.Core.Services.Security;
using Compass.Data;
using Compass.Data.Interfaces;
using Compass.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Compass.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCompassServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - Use Key Vault connection string
        var connectionString = configuration["compass-database-connection"];
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'compass-database-connection' not found in Key Vault");
        }

        services.AddDbContext<CompassDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Repositories
        services.AddScoped<IAssessmentRepository, AssessmentRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IClientPreferencesRepository, ClientPreferencesRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IUsageMetricRepository, UsageMetricRepository>();
        services.AddScoped<ILoginActivityRepository, LoginActivityRepository>();

        // Core Services - Enhanced Analysis Engine
        services.AddScoped<IAzureResourceGraphService, AzureResourceGraphService>();
        services.AddScoped<INamingConventionAnalyzer, NamingConventionAnalyzer>();
        services.AddScoped<ITaggingAnalyzer, TaggingAnalyzer>();
        services.AddScoped<IDependencyAnalyzer, DependencyAnalyzer>();

        // Security Posture Assessment - Individual modular analyzers
        services.AddScoped<INetworkSecurityAnalyzer, NetworkSecurityAnalyzer>();
        services.AddScoped<IPrivateEndpointAnalyzer, PrivateEndpointAnalyzer>();
        services.AddScoped<IDataEncryptionAnalyzer, DataEncryptionAnalyzer>();
        services.AddScoped<IAdvancedThreatProtectionAnalyzer, AdvancedThreatProtectionAnalyzer>();
        services.AddScoped<IDefenderForCloudAnalyzer, DefenderForCloudAnalyzer>();
        services.AddScoped<ISecurityFullAnalyzer, SecurityFullAnalyzer>();

        // Main Security Posture orchestrator
        services.AddScoped<ISecurityPostureAssessmentAnalyzer, SecurityPostureAssessmentAnalyzer>();

        // Identity Access Management Assessment - Individual analyzers
        services.AddScoped<IEnterpriseApplicationsAnalyzer, EnterpriseApplicationsAnalyzer>();
        services.AddScoped<IStaleUsersDevicesAnalyzer, StaleUsersDevicesAnalyzer>();
        services.AddScoped<IResourceIamRbacAnalyzer, ResourceIamRbacAnalyzer>();
        services.AddScoped<IConditionalAccessAnalyzer, ConditionalAccessAnalyzer>();
        services.AddScoped<IIdentityFullAnalyzer, IdentityFullAnalyzer>();

        // Main Identity Access Management orchestrator
        services.AddScoped<IIdentityAccessAssessmentAnalyzer, IdentityAccessAssessmentAnalyzer>();

        // Business Continuity Assessment - Individual analyzers
        services.AddScoped<IBackupCoverageAnalyzer, BackupCoverageAnalyzer>();
        services.AddScoped<IRecoveryConfigurationAnalyzer, RecoveryConfigurationAnalyzer>();
        services.AddScoped<IBusinessContinuityFullAnalyzer, BusinessContinuityFullAnalyzer>();

        // Main Business Continuity orchestrator
        services.AddScoped<IBusinessContinuityAssessmentAnalyzer, BusinessContinuityAssessmentAnalyzer>();

        // Assessment orchestrator
        services.AddScoped<IAssessmentOrchestrator, AssessmentOrchestrator>();

        // OAuth and Microsoft Graph services
        services.AddScoped<IOAuthService, OAuthService>();
        services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();

        // Cost Analysis service
        services.AddScoped<ICostAnalysisService, CostAnalysisService>();
        services.AddHttpClient<CostAnalysisService>();

        // NEW: Permissions service
        services.AddScoped<IPermissionsService, PermissionsService>();

        // Other services
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ILicenseValidationService, LicenseValidationService>();
        services.AddScoped<IUsageTrackingService, UsageTrackingService>();
        services.AddScoped<LoginActivityService>();

        return services;
    }

    public static IServiceCollection AddAzureServices(this IServiceCollection services)
    {
        // Azure services are registered in AddCompassServices
        // The AzureResourceGraphService uses DefaultAzureCredential for authentication
        return services;
    }
}