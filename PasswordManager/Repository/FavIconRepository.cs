using Microsoft.EntityFrameworkCore;
using PasswordManager.Application;
using System.Threading.Tasks;

namespace PasswordManager.Repository
{
    internal class FavIconRepository : IFavIconRepository
    {
        private readonly FavIconDbContext _favIconDbContext;

        public FavIconRepository(FavIconDbContext favIconDbContext)
        {
            _favIconDbContext = favIconDbContext;
        }

        public Task Add(FavIcon favIcon)
        {
            _favIconDbContext.FavIcons.Add(favIcon);
            return _favIconDbContext.SaveChangesAsync();
        }

        public void EnsureCreated()
        {
            _favIconDbContext.Database.EnsureCreated();
        }

        public Task<FavIcon> Get(string host)
        {
            return _favIconDbContext.FavIcons.FirstOrDefaultAsync(f => f.Host == host);
        }
    }
}
