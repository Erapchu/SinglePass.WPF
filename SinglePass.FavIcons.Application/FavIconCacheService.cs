namespace SinglePass.FavIcons.Application
{
    public class FavIconCacheService
    {
        private readonly IFavIconRepository _favIconRepository;

        public FavIconCacheService(IFavIconRepository favIconRepository)
        {
            _favIconRepository = favIconRepository;
        }

        public Task EnsureCreated()
        {
            return _favIconRepository.EnsureCreated();
        }

        public Task<FavIcon?> GetCachedImage(FavIconDto favIconDto)
        {
            return _favIconRepository.Get(favIconDto);
        }

        public Task<List<FavIcon>> GetManyCachedImages(List<FavIconDto> favIconDtos)
        {
            return _favIconRepository.GetMany(favIconDtos);
        }

        public Task SetCachedImage(FavIcon favIcon)
        {
            return _favIconRepository.Add(favIcon);
        }
    }
}
