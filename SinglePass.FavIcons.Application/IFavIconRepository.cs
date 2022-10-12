namespace SinglePass.FavIcons.Application
{
    public interface IFavIconRepository
    {
        public Task Add(FavIcon favIcon);
        public Task<FavIcon?> Get(string host);
        public Task<List<FavIcon>> GetMany(List<FavIconDto> favIcons);
        public Task EnsureCreated();
    }
}
