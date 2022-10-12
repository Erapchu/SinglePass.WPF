using Microsoft.EntityFrameworkCore;
using SinglePass.FavIcons.Application;

namespace SinglePass.FavIcons.Repository
{
    public class FavIconRepository : IFavIconRepository
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
            return _favIconDbContext.Database.MigrateAsync();
        }

        public Task<FavIcon?> Get(string host)
        {
            return _favIconDbContext.FavIcons.FirstOrDefaultAsync(f => f.Host == host);
        }

        public Task<List<FavIcon>> GetMany(List<FavIconDto> favIcons)
        {
            IQueryable<FavIcon> query = _favIconDbContext.FavIcons.Where(f => f.Host == favIcons[0].Host && f.Size == favIcons[0].Size);
            var whereClause = _favIconDbContext.FavIcons.AsNoTracking();
            foreach (var favIcon in favIcons.Skip(1))
            {
                query = query.Concat(whereClause.Where(f => f.Host == favIcon.Host && f.Size == favIcon.Size));
            }
            return query.ToListAsync();
        }
    }
}
