// Compass.Data/CompassDbContext.cs
using Microsoft.EntityFrameworkCore;
using Compass.Data.Entities;

namespace Compass.Data;

public class CompassDbContext : DbContext
{
    public CompassDbContext(DbContextOptions<CompassDbContext> options) : base(options)
    {
    }

    // EXISTING DBSETS
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Assessment> Assessments { get; set; }
    public DbSet<AssessmentFinding> AssessmentFindings { get; set; }

    // LICENSING DBSETS
    public DbSet<Subscription> Subscriptions { get; set; }
    public DbSet<UsageMetric> UsageMetrics { get; set; }
    public DbSet<UsageRecord> UsageRecords { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<LicenseFeature> LicenseFeatures { get; set; }
    public DbSet<SubscriptionFeature> SubscriptionFeatures { get; set; }

    // MISSING DBSET
    public DbSet<AzureEnvironment> AzureEnvironments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.CompanyName).IsRequired();
            entity.Property(e => e.Email).IsRequired();

            // Add indexes for new properties
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.CompanyName);
            entity.HasIndex(e => e.IsTrialAccount);
        });

        // Assessment configuration
        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne<Customer>()
                .WithMany(p => p.Assessments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // AzureEnvironment configuration
        modelBuilder.Entity<AzureEnvironment>(entity =>
        {
            entity.HasKey(e => e.AzureEnvironmentId);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.TenantId).IsRequired();

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.AzureEnvironments)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.TenantId);
        });

        // Subscription configuration
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.HasKey(e => e.SubscriptionId);
            entity.Property(e => e.MonthlyPrice).HasColumnType("decimal(10,2)");
            entity.Property(e => e.AnnualPrice).HasColumnType("decimal(10,2)");

            entity.HasOne(d => d.Customer)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.CustomerId, e.Status });
            entity.HasIndex(e => e.NextBillingDate);
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

        // UsageRecord configuration - FIXED CASCADE ISSUES
        modelBuilder.Entity<UsageRecord>(entity =>
        {
            entity.HasKey(e => e.UsageRecordId);

            entity.HasOne(d => d.Customer)
                .WithMany()
                .HasForeignKey(d => d.CustomerId)
                .OnDelete(DeleteBehavior.NoAction); // CHANGED TO NoAction

            entity.HasOne(d => d.Subscription)
                .WithMany(p => p.UsageRecords)
                .HasForeignKey(d => d.SubscriptionId)
                .OnDelete(DeleteBehavior.NoAction); // CHANGED TO NoAction

            entity.HasOne(d => d.Assessment)
                .WithMany()
                .HasForeignKey(d => d.AssessmentId)
                .OnDelete(DeleteBehavior.NoAction); // CHANGED TO NoAction

            entity.HasOne(d => d.AzureEnvironment)
                .WithMany()
                .HasForeignKey(d => d.EnvironmentId)
                .OnDelete(DeleteBehavior.NoAction); // CHANGED TO NoAction

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