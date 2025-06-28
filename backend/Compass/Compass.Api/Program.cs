using Azure.Extensions.AspNetCore.Configuration.Secrets;
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

// AZURE KEY VAULT INTEGRATION - Add this FIRST before other configurations
if (!builder.Environment.IsDevelopment() || !string.IsNullOrEmpty(builder.Configuration["AzureKeyVault:KeyVaultName"]))
{
    var keyVaultName = builder.Configuration["AzureKeyVault:KeyVaultName"];
    if (!string.IsNullOrEmpty(keyVaultName))
    {
        var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
        builder.Configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());

        // Log successful Key Vault integration (no secrets)
        builder.Services.AddLogging(logging => logging.AddConsole());
        var tempLogger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Startup");
        tempLogger.LogInformation("Azure Key Vault integrated: {KeyVaultName}", keyVaultName);
    }
}

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
        Version = "v1",
        Description = "Azure Governance Assessment Platform API"
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

// Organization Data Migration Service - NEW
builder.Services.AddScoped<OrganizationDataMigrationService>();

// Add DbContext - NOW SECURE (retrieves connection string from Key Vault)
builder.Services.AddDbContext<CompassDbContext>(options =>
{
    var connectionString = builder.Configuration["compass-database-connection"];
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Database connection string 'compass-database-connection' not found in Key Vault or configuration");
    }
    options.UseSqlServer(connectionString);
});

// Register OAuth services - SECURE (OAuth credentials from Key Vault)
builder.Services.AddScoped<IOAuthService, OAuthService>();
builder.Services.AddMemoryCache(); // For OAuth state management
builder.Services.AddHttpClient(); // For OAuth token exchange

// Register SecretClient for MSP Key Vault operations
builder.Services.AddSingleton<SecretClient>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var keyVaultName = configuration["AzureKeyVault:KeyVaultName"];
    var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");
    return new SecretClient(keyVaultUri, new DefaultAzureCredential());
});

// JWT Configuration - NOW SECURE (retrieves secret from Key Vault)
var jwtSection = builder.Configuration.GetSection("Jwt");
var secretKey = builder.Configuration["jwt-secret-key"] ?? throw new InvalidOperationException("JWT SecretKey 'jwt-secret-key' is required and must be configured in Key Vault");
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

// Email Configuration - NOW SECURE (retrieves client secret from Key Vault)
builder.Services.Configure<Compass.Core.Services.EmailOptions>(options =>
{
    var emailSection = builder.Configuration.GetSection("Email");

    // Manually bind each property, resolving Key Vault references
    options.TenantId = builder.Configuration["email-tenant-id"] ?? throw new InvalidOperationException("Email TenantId 'email-tenant-id' not found in Key Vault");
    options.ClientId = builder.Configuration["email-client-id"] ?? throw new InvalidOperationException("Email ClientId 'email-client-id' not found in Key Vault");
    options.ClientSecret = builder.Configuration["email-client-secret"] ?? throw new InvalidOperationException("Email ClientSecret 'email-client-secret' not found in Key Vault");

    // Bind remaining properties normally
    options.BaseUrl = emailSection["BaseUrl"] ?? "";
    options.NoReplyAddress = emailSection["NoReplyAddress"] ?? "";
    options.SupportAddress = emailSection["SupportAddress"] ?? "";
    options.NotificationsAddress = emailSection["NotificationsAddress"] ?? "";
});

// Register repositories
builder.Services.AddScoped<IAssessmentRepository, AssessmentRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IUsageMetricRepository, UsageMetricRepository>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();

// Register services
builder.Services.AddScoped<IAssessmentOrchestrator, AssessmentOrchestrator>();
builder.Services.AddScoped<IAzureResourceGraphService, AzureResourceGraphService>();
builder.Services.AddScoped<INamingConventionAnalyzer, NamingConventionAnalyzer>();
builder.Services.AddScoped<ITaggingAnalyzer, TaggingAnalyzer>();
builder.Services.AddScoped<IDependencyAnalyzer, DependencyAnalyzer>();
builder.Services.AddScoped<ILicenseValidationService, LicenseValidationService>();
builder.Services.AddScoped<IUsageTrackingService, UsageTrackingService>();
builder.Services.AddScoped<IClientService, ClientService>();

// Register authentication services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register MFA service
builder.Services.AddScoped<IMfaService, MfaService>();

// NEW: Register Team Activity Logger
//builder.Services.AddScoped<ITeamActivityLogger, TeamActivityLogger>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
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
    Version = "1.0.0"
}));

app.MapHealthChecks("/health/db");

// Organization migration status endpoint - NEW
app.MapGet("/admin/migration-status", async (OrganizationDataMigrationService migrationService) =>
{
    var status = await migrationService.GetMigrationStatus();
    return Results.Ok(status);
});

// Organization migration endpoint - NEW  
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

app.Run();