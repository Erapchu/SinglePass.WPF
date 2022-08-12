using Microsoft.EntityFrameworkCore;
using SinglePass.FavIcons.Application;

namespace SinglePass.FavIcons.Repository
{
    public class FavIconDbContext : DbContext
    {
        public DbSet<FavIcon> FavIcons { get; set; }

        public FavIconDbContext(DbContextOptions<FavIconDbContext> options) : base(options)
        {
            
        }
    }
}
