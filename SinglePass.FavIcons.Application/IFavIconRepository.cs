namespace SinglePass.FavIcons.Application
{
    public interface IFavIconRepository
    {
        public Task Save(FavIcon favIcon);
        public Task<FavIcon?> Get(FavIconDto favIcon);
        public Task<List<FavIcon>> GetMany(List<FavIconDto> favIcons);
        public Task Migrate();
    }
}
