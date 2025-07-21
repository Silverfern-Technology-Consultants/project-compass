using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Compass.Api.Extensions;
using Compass.Api.Services;
using Compass.core.Interfaces;
using Compass.Core.Interfaces;
using Compass.Core.Services;
using Compass.Data;
using Compass.Data.Interfaces;
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

// Organization Data Migration Service
builder.Services.AddScoped<OrganizationDataMigrationService>();

// Register OAuth services (these are NOT in ServiceCollectionExtensions)
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

// ===== USE SERVICE COLLECTION EXTENSIONS FOR ALL COMPASS SERVICES =====
builder.Services.AddCompassServices(builder.Configuration);

// ===== AUTHENTICATION SERVICES (NOT in ServiceCollectionExtensions) =====
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// ===== MFA SERVICES (NOT in ServiceCollectionExtensions) =====
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

        // DARK THEME CONFIGURATION
        c.InjectStylesheet("/swagger-ui/custom.css");
        c.DocumentTitle = "Compass API - Silverfern Technology Consultants";

        // Optional: Custom JavaScript for additional theming
        c.InjectJavascript("/swagger-ui/custom.js");
    });
}
app.UseStaticFiles();
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
        "Multi-Tenant Architecture",
        "Modular Identity Assessment System"
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

// Identity Assessment System status endpoint
app.MapGet("/api/identity-assessment/status", () => Results.Ok(new
{
    Status = "Modular Identity Assessment System Active",
    Features = new[]
    {
        "Enterprise Applications Analysis",
        "Stale Users & Devices Detection",
        "Resource IAM/RBAC Analysis",
        "Conditional Access Policy Review",
        "Full Identity Assessment Orchestration",
        "OAuth-Enhanced Analysis",
        "Microsoft Graph Integration"
    },
    AssessmentTypes = new[]
    {
        "EnterpriseApplications",
        "StaleUsersDevices",
        "ResourceIamRbac",
        "ConditionalAccess",
        "IdentityFull"
    }
}));

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Compass API starting up with enhanced modular architecture");
logger.LogInformation("Key Vault: {KeyVaultName}", keyVaultName);
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
logger.LogInformation("Client Preferences System: ACTIVE");
logger.LogInformation("Modular Identity Assessment System: ACTIVE");

app.Run();