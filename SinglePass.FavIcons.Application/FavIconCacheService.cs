namespace SinglePass.FavIcons.Application
{
    public class FavIconCacheService
    {
        private readonly IFavIconRepository _favIconRepository;

        public FavIconCacheService(IFavIconRepository favIconRepository)
        {
            _favIconRepository = favIconRepository;
        }

        public Task Migrate()
        {
            return _favIconRepository.Migrate();
        }

        public Task<FavIcon?> Get(FavIconDto favIconDto)
        {
            return _favIconRepository.Get(favIconDto);
        }

        public Task<List<FavIcon>> GetMany(List<FavIconDto> favIconDtos)
        {
            return _favIconRepository.GetMany(favIconDtos);
        }

        public Task Add(FavIcon favIcon)
        {
            return _favIconRepository.Add(favIcon);
        }
    }
}
