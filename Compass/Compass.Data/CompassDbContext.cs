using Microsoft.EntityFrameworkCore;
using Compass.Core.Models;

namespace Compass.Data
{
    public class CompassDbContext : DbContext
    {
        public CompassDbContext(DbContextOptions<CompassDbContext> options) : base(options)
        {
        }

        public DbSet<Assessment> Assessments { get; set; }
        public DbSet<AssessmentFinding> AssessmentFindings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Assessment configuration
            modelBuilder.Entity<Assessment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CustomerName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.ReportBlobUrl).HasMaxLength(500);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.StartedDate);
            });

            // AssessmentFinding configuration
            modelBuilder.Entity<AssessmentFinding>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ResourceId).HasMaxLength(500).IsRequired();
                entity.Property(e => e.ResourceName).HasMaxLength(255).IsRequired();
                entity.Property(e => e.ResourceType).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Issue).HasMaxLength(1000).IsRequired();
                entity.Property(e => e.Recommendation).HasMaxLength(2000).IsRequired();
                entity.Property(e => e.EstimatedEffort).HasMaxLength(50);

                // Foreign key relationship
                entity.HasOne(e => e.Assessment)
                      .WithMany(a => a.Findings)
                      .HasForeignKey(e => e.AssessmentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.AssessmentId);
                entity.HasIndex(e => e.Category);
                entity.HasIndex(e => e.Severity);
            });
        }
    }
}