using Microsoft.EntityFrameworkCore;
using PasswordManager.Application;
using System.Collections.Generic;
using System.Linq;
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

        public Task EnsureCreated()
        {
            return _favIconDbContext.Database.EnsureCreatedAsync();
        }

        public Task<FavIcon> Get(string host)
        {
            return _favIconDbContext.FavIcons.FirstOrDefaultAsync(f => f.Host == host);
        }

        public Task<List<FavIcon>> GetMany(List<string> hosts)
        {
            return _favIconDbContext.FavIcons.Where(fi => hosts.Contains(fi.Host)).ToListAsync();
        }
    }
}
