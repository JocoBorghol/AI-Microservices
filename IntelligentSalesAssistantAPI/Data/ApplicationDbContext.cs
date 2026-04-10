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
        }
    }
}