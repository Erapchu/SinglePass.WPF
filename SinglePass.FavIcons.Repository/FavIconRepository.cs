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

        public Task<FavIcon?> Get(FavIconDto favIconDto)
        {
            return _favIconDbContext.FavIcons.FirstOrDefaultAsync(f => f.Host == favIconDto.Host && f.Size == favIconDto.Size);
        }

        public Task<List<FavIcon>> GetMany(List<FavIconDto> favIconDtos)
        {
            if (favIconDtos.Count == 0)
                return Task.FromResult(new List<FavIcon>());

            IQueryable<FavIcon> query = _favIconDbContext.FavIcons.AsNoTracking().Where(f => f.Host == favIconDtos[0].Host && f.Size == favIconDtos[0].Size);
            var whereClause = _favIconDbContext.FavIcons.AsNoTracking();
            foreach (var favIcon in favIconDtos.Skip(1))
            {
                query = query.Concat(whereClause.Where(f => f.Host == favIcon.Host && f.Size == favIcon.Size));
            }
            return query.ToListAsync();
        }
    }
}
