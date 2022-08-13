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

        public Task<FavIcon?> GetCachedImage(string host)
        {
            return _favIconRepository.Get(host);
        }

        public Task<List<FavIcon>> GetManyCachedImages(List<string> hosts)
        {
            return _favIconRepository.GetMany(hosts);
        }

        public Task SetCachedImage(FavIcon favIcon)
        {
            return _favIconRepository.Add(favIcon);
        }
    }
}
