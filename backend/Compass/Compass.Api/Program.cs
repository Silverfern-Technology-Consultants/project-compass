﻿using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Compass.Api.Services;
using Compass.Core.Services;
using Compass.Core.Interfaces;
using Compass.Data;
using Compass.Data.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// AZURE KEY VAULT INTEGRATION - Always required
var keyVaultName = builder.Configuration["AzureKeyVault:KeyVaultName"] ?? "kv-dev-7yu4s2pu";
var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

// Optimized credential chain for faster development authentication
var credential = new ChainedTokenCredential(
    new AzureCliCredential(),           // Try Azure CLI first (fastest for dev)
    new VisualStudioCredential(),       // Then Visual Studio
    new DefaultAzureCredential()        // Finally full DefaultAzureCredential chain
);

builder.Configuration.AddAzureKeyVault(keyVaultUri, credential);

var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");
tempLogger.LogInformation("Azure Key Vault integrated: {KeyVaultName}", keyVaultName);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enforce PascalCase for all JSON serialization/deserialization
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // null = PascalCase
        options.JsonSerializerOptions.DictionaryKeyPolicy = null;

        // Optional: Make property names case-insensitive for better compatibility
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null; // PascalCase
    options.SerializerOptions.DictionaryKeyPolicy = null;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});
builder.Services.AddEndpointsApiExplorer();

// Enhanced Swagger configuration with JWT support
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Compass API",
        Version = "v2.0.0",
        Description = "Azure Governance Assessment Platform API with Enhanced Security Analysis & Real NSG Rule Parsing"
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add HTTP Context Accessor
builder.Services.AddHttpContextAccessor();

//DB Seeder
builder.Services.AddScoped<TestDataSeeder>();

// ===== LOGIN ACTIVITY SERVICES (NEW) =====
builder.Services.AddScoped<ILoginActivityRepository, LoginActivityRepository>();
builder.Services.AddScoped<LoginActivityService>();

// Organization Data Migration Service
builder.Services.AddScoped<OrganizationDataMigrationService>();

// Add DbContext - Key Vault connection string required
builder.Services.AddDbContext<CompassDbContext>(options =>
{
    var connectionString = builder.Configuration["compass-database-connection"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string 'compass-database-connection' not found in Key Vault");
    }
    options.UseSqlServer(connectionString);
});

// Register OAuth services
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddMemoryCache(); // For OAuth state management
builder.Services.AddHttpClient(); // For OAuth token exchange

// Register SecretClient for MSP Key Vault operations
builder.Services.AddSingleton<SecretClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var keyVaultName = configuration["AzureKeyVault:KeyVaultName"] ?? "kv-dev-7yu4s2pu";
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

    // Use same optimized credential chain
    var credential = new ChainedTokenCredential(
        new AzureCliCredential(),
        new VisualStudioCredential(),
        new DefaultAzureCredential()
    );

    return new SecretClient(keyVaultUri, credential);
});

// JWT Configuration - Key Vault required
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = builder.Configuration["jwt-secret-key"]
    ?? throw new InvalidOperationException("JWT SecretKey 'jwt-secret-key' is required in Key Vault");

var issuer = jwtSection["Issuer"] ?? "compass-api";
var audience = jwtSection["Audience"] ?? "compass-client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Email Configuration - Key Vault required
builder.Services.Configure<Compass.Core.Services.EmailOptions>(options =>
{
    var emailSection = builder.Configuration.GetSection("Email");

    // Key Vault configuration
    options.TenantId = builder.Configuration["email-tenant-id"]
        ?? throw new InvalidOperationException("Email TenantId 'email-tenant-id' not found in Key Vault");
    options.ClientId = builder.Configuration["email-client-id"]
        ?? throw new InvalidOperationException("Email ClientId 'email-client-id' not found in Key Vault");
    options.ClientSecret = builder.Configuration["email-client-secret"]
        ?? throw new InvalidOperationException("Email ClientSecret 'email-client-secret' not found in Key Vault");

    // Common configuration
    options.BaseUrl = emailSection["BaseUrl"] ?? "";
    options.NoReplyAddress = emailSection["NoReplyAddress"] ?? "";
    options.SupportAddress = emailSection["SupportAddress"] ?? "";
    options.NotificationsAddress = emailSection["NotificationsAddress"] ?? "";
});

// ===== REPOSITORY REGISTRATIONS =====
builder.Services.AddScoped<IAssessmentRepository, AssessmentRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IUsageMetricRepository, UsageMetricRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<ILoginActivityRepository, LoginActivityRepository>();
builder.Services.AddScoped<IClientPreferencesRepository, ClientPreferencesRepository>();

// ===== ASSESSMENT SERVICES WITH CLIENT PREFERENCES SUPPORT =====
// Assessment Orchestrator (with client preferences support)
builder.Services.AddScoped<IAssessmentOrchestrator, AssessmentOrchestrator>();

// Assessment Analyzers - Use preference-aware versions
builder.Services.AddScoped<INamingConventionAnalyzer, NamingConventionAnalyzer>();
builder.Services.AddScoped<IPreferenceAwareNamingAnalyzer, PreferenceAwareNamingAnalyzer>();

// Standard analyzers
builder.Services.AddScoped<ITaggingAnalyzer, TaggingAnalyzer>();
builder.Services.AddScoped<IIdentityAccessAssessmentAnalyzer, IdentityAccessAssessmentAnalyzer>();
builder.Services.AddScoped<IBusinessContinuityAssessmentAnalyzer, BusinessContinuityAssessmentAnalyzer>();
builder.Services.AddScoped<ISecurityPostureAssessmentAnalyzer, SecurityPostureAssessmentAnalyzer>();
builder.Services.AddScoped<IDependencyAnalyzer, DependencyAnalyzer>();

// ===== AZURE SERVICES =====
builder.Services.AddScoped<IAzureResourceGraphService, AzureResourceGraphService>();

// ===== BUSINESS SERVICES =====
builder.Services.AddScoped<ILicenseValidationService, LicenseValidationService>();
builder.Services.AddScoped<IUsageTrackingService, UsageTrackingService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<LoginActivityService>();

// ===== AUTHENTICATION SERVICES =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ===== MFA SERVICES =====
builder.Services.AddScoped<IMfaService, MfaService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition");
    });
});

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CompassDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Compass API v1");
        c.DisplayRequestDuration();
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

// Add authentication & authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoints
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0",
    Features = new[]
    {
        "Client Preferences System",
        "Preference-Aware Assessments",
        "OAuth Integration",
        "Multi-Tenant Architecture"
    }
}));

app.MapHealthChecks("/health/db");

// Organization migration status endpoint
app.MapGet("/admin/migration-status", async (OrganizationDataMigrationService migrationService) =>
{
    var status = await migrationService.GetMigrationStatus();
    return Results.Ok(status);
});

// Organization migration endpoint
app.MapPost("/admin/migrate-organizations", async (OrganizationDataMigrationService migrationService) =>
{
    try
    {
        await migrationService.MigrateExistingCustomersToOrganizations();
        return Results.Ok(new { message = "Organization migration completed successfully" });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Migration failed: {ex.Message}");
    }
});

// Client Preferences API status endpoint (for development/testing)
app.MapGet("/api/client-preferences/status", () => Results.Ok(new
{
    Status = "Client Preferences System Active",
    Features = new[]
    {
        "Template-based preference configuration",
        "Preference-aware assessment analysis",
        "Enhanced recommendations",
        "Organization-scoped security",
        "Real-time preference validation"
    },
    Endpoints = new[]
    {
        "GET /api/ClientPreferences/client/{clientId}",
        "POST /api/ClientPreferences/client/{clientId}",
        "DELETE /api/ClientPreferences/client/{clientId}",
        "GET /api/ClientPreferences/client/{clientId}/exists",
        "GET /api/ClientPreferences"
    }
}));

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Compass API starting up with Client Preferences integration");
logger.LogInformation("Key Vault: {KeyVaultName}", keyVaultName);
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Client Preferences System: ACTIVE");

app.Run();