using Microsoft.EntityFrameworkCore;
using SinglePass.FavIcons.Application;

namespace SinglePass.FavIcons.Repository
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

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var expanded = Environment.ExpandEnvironmentVariables("Data Source=%localappdata%/SinglePass/Cache/FavIcon.db");
            expanded = expanded.Replace('\\', '/');
            optionsBuilder.UseSqlite(expanded);
        }
    }
}
