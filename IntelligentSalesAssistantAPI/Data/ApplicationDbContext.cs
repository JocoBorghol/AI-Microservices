using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using IntelligentSalesAssistantAPI.Models;

namespace IntelligentSalesAssistantAPI.Data
{
    // Databascontext för applikationen
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tabeller i databasen
        public DbSet<CompanyWebsite> CompanyWebsites { get; set; }
        public DbSet<ContentDraft> ContentDrafts { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Supprimera PendingModelChangesWarning som kan ge falskt positiva varningar
            // i EF9 när snapshot-jämförelsen inte stämmer exakt med den faktiska modellen.
            // Se: https://learn.microsoft.com/ef/core/what-is-new/ef-core-9.0/breaking-changes
            optionsBuilder.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Konfigurera User
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // Konfigurera index för CompanyWebsite
            modelBuilder.Entity<CompanyWebsite>(entity =>
            {
                // Index på OrgNumber för snabb sökning
                entity.HasIndex(e => e.OrgNumber);

                // Index på Category för filtrering
                entity.HasIndex(e => e.Category);

                // Index på CreatedAt för sortering
                entity.HasIndex(e => e.CreatedAt);
            });

            // Konfigurera ContentDraft
            modelBuilder.Entity<ContentDraft>(entity =>
            {
                entity.HasKey(e => e.Id);

                // En hemsida har många utkast
                entity.HasOne(e => e.Website)
                    .WithMany(w => w.ContentDrafts)
                    .HasForeignKey(e => e.WebsiteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Sammansatt index för effektiv kontexthämtning
                entity.HasIndex(e => new { e.WebsiteId, e.CreatedAt });

                // Ytterligare index för sortering/sökning
                entity.HasIndex(e => e.CreatedAt);
            });
        }
    }
}