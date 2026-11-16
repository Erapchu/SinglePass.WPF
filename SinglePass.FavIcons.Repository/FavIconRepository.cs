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

        public Task Save(FavIcon favIcon)
        {
            _favIconDbContext.FavIcons.Update(favIcon);
            return _favIconDbContext.SaveChangesAsync();
        }

        public Task Migrate()
        {
            return _favIconDbContext.Database.MigrateAsync();
        }

        public Task<FavIcon?> Get(FavIconDto favIconDto)
        {
            return _favIconDbContext.FavIcons.FirstOrDefaultAsync(f => f.Host == favIconDto.Host && f.Size == favIconDto.Size);
        }

        public async Task<List<FavIcon>> GetMany(List<FavIconDto> favIconDtos)
        {
            var icons = new List<FavIcon>();
            foreach (var group in favIconDtos.GroupBy(icon => icon.Size))
            {
                var size = group.Key;
                var hosts = group.Select(icon => icon.Host).ToArray();
                icons.AddRange(await _favIconDbContext.FavIcons.AsNoTracking()
                    .Where(icon => icon.Size == size && hosts.Contains(icon.Host))
                    .ToListAsync());
            }
            return icons;
        }
    }
}
