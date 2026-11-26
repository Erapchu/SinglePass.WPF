using Microsoft.EntityFrameworkCore;
using Erapkey.FavIcons.Application;

namespace Erapkey.FavIcons.Repository
{
    public class FavIconDbContext : DbContext
    {
        public DbSet<FavIcon> FavIcons { get; set; }

        public FavIconDbContext() : base()
        {

        }

        public FavIconDbContext(DbContextOptions<FavIconDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FavIcon>().HasIndex(icon => new { icon.Host, icon.Size }).IsUnique();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var expanded = Environment.ExpandEnvironmentVariables("Data Source=%localappdata%/Erapkey/Cache/FavIcon.db");
            expanded = expanded.Replace('\\', '/');
            optionsBuilder.UseSqlite(expanded);
        }
    }
}
