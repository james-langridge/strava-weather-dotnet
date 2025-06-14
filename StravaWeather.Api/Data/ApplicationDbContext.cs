using Microsoft.EntityFrameworkCore;
using StravaWeather.Api.Models.Entities;

namespace StravaWeather.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        public DbSet<User> Users { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasIndex(e => e.StravaAthleteId).IsUnique();
                
                entity.Property(e => e.Id)
                    .HasDefaultValueSql("gen_random_uuid()");
                    
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                    
                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(u => u.Preferences)
                    .WithOne(p => p.User)
                    .HasForeignKey<UserPreference>(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            
            // Configure UserPreference entity
            modelBuilder.Entity<UserPreference>(entity =>
            {
                entity.ToTable("user_preferences");
                
                entity.Property(e => e.Id)
                    .HasDefaultValueSql("gen_random_uuid()");
                    
                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
                    
                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
        
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }
        
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }
        
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is User || e.Entity is UserPreference)
                .Where(e => e.State == EntityState.Modified);
                
            foreach (var entry in entries)
            {
                if (entry.Entity is User user)
                {
                    user.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is UserPreference pref)
                {
                    pref.UpdatedAt = DateTime.UtcNow;
                }
            }
        }
    }
}