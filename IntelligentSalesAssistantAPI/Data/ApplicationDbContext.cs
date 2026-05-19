using Microsoft.EntityFrameworkCore;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
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