using Microsoft.EntityFrameworkCore;
using PasswordManager.Application;

namespace PasswordManager.Repository
{
    internal class FavIconDbContext : DbContext
    {
        public DbSet<FavIcon> FavIcons { get; set; }

        public FavIconDbContext(DbContextOptions<FavIconDbContext> options) : base(options)
        {

        }
    }
}
