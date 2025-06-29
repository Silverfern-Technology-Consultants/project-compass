using Microsoft.EntityFrameworkCore;
using Compass.Data.Entities;

namespace Compass.Data;

public class CompassDbContext : DbContext
{
    public CompassDbContext(DbContextOptions<CompassDbContext> options) : base(options)
    {
    }

    // ORGANIZATION DBSET
    public DbSet<Organization> Organizations { get; set; }

    // NEW: CLIENT MANAGEMENT DBSETS
    public DbSet<Client> Clients { get; set; }
    public DbSet<ClientAccess> ClientAccess { get; set; }

    // EXISTING DBSETS
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Assessment> Assessments { get; set; }
    public DbSet<AssessmentFinding> AssessmentFindings { get; set; }
    public DbSet<AssessmentResource> AssessmentResources { get; set; }

    // LICENSING DBSETS
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<UsageMetric> UsageMetrics { get; set; }
    public DbSet<UsageRecord> UsageRecords { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<LicenseFeature> LicenseFeatures { get; set; }
    public DbSet<SubscriptionFeature> SubscriptionFeatures { get; set; }

    public DbSet<AzureEnvironment> AzureEnvironments { get; set; }

    // TEAM MANAGEMENT DBSET
    public DbSet<TeamInvitation> TeamInvitations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Organization configuration
        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.OrganizationId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Active");
            entity.Property(e => e.OrganizationType).HasMaxLength(50).HasDefaultValue("MSP");

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => new { e.Status, e.OrganizationType });
        });

        // NEW: Client configuration
        modelBuilder.Entity<Client>(entity =>
        {
            entity.HasKey(e => e.ClientId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Active");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            // Organization relationship
            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Clients)  // Add Clients to Organization entity
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Created by relationship
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Last modified by relationship
            entity.HasOne(e => e.LastModifiedBy)
                .WithMany()
                .HasForeignKey(e => e.LastModifiedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => new { e.Name, e.OrganizationId });
            entity.HasIndex(e => new { e.Status, e.IsActive });
            entity.HasIndex(e => e.CreatedByCustomerId);
            entity.HasIndex(e => e.LastModifiedByCustomerId);
        });
        modelBuilder.Entity<AssessmentResource>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ResourceId)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255);

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ResourceTypeName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.ResourceGroup)
                .HasMaxLength(255);

            entity.Property(e => e.Location)
                .HasMaxLength(100);

            entity.Property(e => e.SubscriptionId)
                .HasMaxLength(50);

            entity.Property(e => e.Kind)
                .HasMaxLength(100);

            entity.Property(e => e.Environment)
                .HasMaxLength(50);

            entity.Property(e => e.Tags)
                .IsRequired()
                .HasDefaultValue("{}");

            entity.Property(e => e.Properties)
                .HasColumnType("nvarchar(max)"); // For large JSON data

            entity.Property(e => e.Sku)
                .HasColumnType("nvarchar(max)"); // For SKU JSON data

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("GETUTCDATE()");

            // Foreign key relationship
            entity.HasOne(e => e.Assessment)
                .WithMany(a => a.Resources)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes for performance
            entity.HasIndex(e => e.AssessmentId)
                .HasDatabaseName("IX_AssessmentResources_AssessmentId");

            entity.HasIndex(e => new { e.AssessmentId, e.ResourceTypeName })
                .HasDatabaseName("IX_AssessmentResources_AssessmentId_ResourceTypeName");

            entity.HasIndex(e => new { e.AssessmentId, e.ResourceGroup })
                .HasDatabaseName("IX_AssessmentResources_AssessmentId_ResourceGroup");

            entity.HasIndex(e => new { e.AssessmentId, e.Location })
                .HasDatabaseName("IX_AssessmentResources_AssessmentId_Location");
        });
        // NEW: ClientAccess configuration
        modelBuilder.Entity<ClientAccess>(entity =>
        {
            entity.HasKey(e => e.ClientAccessId);
            entity.Property(e => e.AccessLevel).IsRequired().HasMaxLength(50).HasDefaultValue("Read");
            entity.Property(e => e.CanViewAssessments).HasDefaultValue(true);
            entity.Property(e => e.CanCreateAssessments).HasDefaultValue(false);
            entity.Property(e => e.CanDeleteAssessments).HasDefaultValue(false);
            entity.Property(e => e.CanManageEnvironments).HasDefaultValue(false);
            entity.Property(e => e.CanViewReports).HasDefaultValue(true);
            entity.Property(e => e.CanExportData).HasDefaultValue(false);

            // Customer relationship
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Client relationship
            entity.HasOne(e => e.Client)
                .WithMany(c => c.ClientAccess)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Granted by relationship
            entity.HasOne(e => e.GrantedBy)
                .WithMany()
                .HasForeignKey(e => e.GrantedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint for customer-client combination
            entity.HasIndex(e => new { e.CustomerId, e.ClientId }).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.ClientId);
            entity.HasIndex(e => e.GrantedByCustomerId);
        });

        // Customer configuration - UPDATED
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.CompanyName).IsRequired();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).HasDefaultValue("Owner");

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Members)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.SentInvitations)
                .WithOne(ti => ti.InvitedBy)
                .HasForeignKey(ti => ti.InvitedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(e => e.AcceptedInvitations)
                .WithOne(ti => ti.AcceptedBy)
                .HasForeignKey(ti => ti.AcceptedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.CompanyName);
            entity.HasIndex(e => e.IsTrialAccount);
            entity.HasIndex(e => new { e.OrganizationId, e.Role });
        });

        // Assessment configuration - UPDATED for Client
        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Fix the decimal precision warning
            entity.Property(e => e.OverallScore)
                .HasColumnType("decimal(5,2)");

            entity.HasOne(e => e.Customer)
                .WithMany(p => p.Assessments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Organization)
                .WithMany(o => o.Assessments)
                .HasForeignKey(e => e.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: Client relationship
            entity.HasOne(e => e.Client)
                .WithMany(c => c.Assessments)
                .HasForeignKey(e => e.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.ClientId);  // NEW: Client index
        });

        // AssessmentFinding configuration
        modelBuilder.Entity<AssessmentFinding>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(d => d.Assessment)
                .WithMany(p => p.Findings)
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AzureEnvironment configuration - UPDATED for Client
        modelBuilder.Entity<AzureEnvironment>(entity =>
        {
            entity.HasKey(e => e.AzureEnvironmentId);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.TenantId).IsRequired();

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.AzureEnvironments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // NEW: Client relationship
            entity.HasOne(d => d.Client)
                .WithMany(c => c.AzureEnvironments)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.TenantId);
            entity.HasIndex(e => e.ClientId);  // NEW: Client index
        });

        // TeamInvitation configuration
        modelBuilder.Entity<TeamInvitation>(entity =>
        {
            entity.HasKey(e => e.InvitationId);
            entity.Property(e => e.InvitedEmail).IsRequired().HasMaxLength(255);
            entity.Property(e => e.InvitedRole).IsRequired().HasMaxLength(50);
            entity.Property(e => e.InvitationToken).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.InvitationMessage).HasMaxLength(500);

            entity.HasOne(d => d.Organization)
                .WithMany(o => o.TeamInvitations)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.InvitedBy)
                .WithMany(c => c.SentInvitations)
                .HasForeignKey(d => d.InvitedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.AcceptedBy)
                .WithMany(c => c.AcceptedInvitations)
                .HasForeignKey(d => d.AcceptedByCustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.InvitationToken).IsUnique();
            entity.HasIndex(e => e.InvitedEmail);
            entity.HasIndex(e => new { e.OrganizationId, e.Status });
            entity.HasIndex(e => e.ExpirationDate);
        });

        // Subscription configuration - UPDATED for Client
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId);
            entity.Property(e => e.MonthlyPrice).HasColumnType("decimal(10,2)");
            entity.Property(e => e.AnnualPrice).HasColumnType("decimal(10,2)");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Organization)
                .WithMany(o => o.Subscriptions)
                .HasForeignKey(d => d.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: Client relationship
            entity.HasOne(d => d.Client)
                .WithMany(c => c.Subscriptions)
                .HasForeignKey(d => d.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.CustomerId, e.Status });
            entity.HasIndex(e => e.NextBillingDate);
            entity.HasIndex(e => e.OrganizationId);
            entity.HasIndex(e => e.ClientId);  // NEW: Client index
        });

        // UsageMetric configuration
        modelBuilder.Entity<UsageMetric>(entity =>
        {
            entity.HasKey(e => e.UsageId);

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.UsageMetrics)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Subscription)
                .WithMany(p => p.UsageMetrics)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.CustomerId, e.BillingPeriod, e.MetricType });
            entity.HasIndex(e => e.RecordedDate);
        });

        // UsageRecord configuration
        modelBuilder.Entity<UsageRecord>(entity =>
        {
            entity.HasKey(e => e.UsageRecordId);

            entity.HasOne(d => d.Customer)
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.Subscription)
                .WithMany(p => p.UsageRecords)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.Assessment)
                .WithMany()
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(d => d.AzureEnvironment)
                .WithMany()
                .HasForeignKey(d => d.EnvironmentId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => new { e.CustomerId, e.BillingMonth, e.BillingYear });
            entity.HasIndex(e => e.UsageDate);
        });

        // Invoice configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();

            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TaxAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10,2)");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.Invoices)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Subscription)
                .WithMany(p => p.Invoices)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.CustomerId, e.Status });
            entity.HasIndex(e => e.DueDate);
        });

        // LicenseFeature configuration
        modelBuilder.Entity<LicenseFeature>(entity =>
        {
            entity.HasKey(e => e.FeatureId);
            entity.HasIndex(e => e.FeatureName).IsUnique();
        });

        // SubscriptionFeature configuration (Many-to-Many)
        modelBuilder.Entity<SubscriptionFeature>(entity =>
        {
            entity.HasKey(e => new { e.SubscriptionId, e.FeatureId });

            entity.HasOne(d => d.Subscription)
                .WithMany(p => p.SubscriptionFeatures)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.LicenseFeature)
                .WithMany(p => p.SubscriptionFeatures)
                .HasForeignKey(d => d.FeatureId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed default license features
        SeedLicenseFeatures(modelBuilder);
    }

    private void SeedLicenseFeatures(ModelBuilder modelBuilder)
    {
        var features = new[]
        {
            new LicenseFeature
            {
                FeatureId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                FeatureName = "unlimited-assessments",
                FeatureDescription = "Run unlimited governance assessments",
                FeatureType = "Toggle",
                DefaultValue = "false",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                FeatureName = "api-access",
                FeatureDescription = "Access to REST API endpoints",
                FeatureType = "Toggle",
                DefaultValue = "false",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                FeatureName = "white-label",
                FeatureDescription = "White-label portal with custom branding",
                FeatureType = "Toggle",
                DefaultValue = "false",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                FeatureName = "custom-branding",
                FeatureDescription = "Custom company branding and logos",
                FeatureType = "Toggle",
                DefaultValue = "false",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                FeatureName = "advanced-analytics",
                FeatureDescription = "Advanced reporting and analytics",
                FeatureType = "Toggle",
                DefaultValue = "false",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                FeatureName = "priority-support",
                FeatureDescription = "Priority customer support",
                FeatureType = "Value",
                DefaultValue = "email",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                FeatureName = "multi-tenant",
                FeatureDescription = "Multi-tenant management capabilities",
                FeatureType = "Toggle",
                DefaultValue = "false",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                FeatureName = "max-subscriptions",
                FeatureDescription = "Maximum Azure subscriptions allowed",
                FeatureType = "Limit",
                DefaultValue = "3",
                IsActive = true
            },
            new LicenseFeature
            {
                FeatureId = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                FeatureName = "max-assessments-monthly",
                FeatureDescription = "Maximum assessments per month",
                FeatureType = "Limit",
                DefaultValue = "1",
                IsActive = true
            }
        };

        modelBuilder.Entity<LicenseFeature>().HasData(features);
    }
}