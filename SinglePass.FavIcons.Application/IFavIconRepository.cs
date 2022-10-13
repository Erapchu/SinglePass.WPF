namespace SinglePass.FavIcons.Application
{
    public interface IFavIconRepository
    {
        public Task Add(FavIcon favIcon);
        public Task<FavIcon?> Get(FavIconDto favIcon);
        public Task<List<FavIcon>> GetMany(List<FavIconDto> favIcons);
        public Task EnsureCreated();
    }
}
