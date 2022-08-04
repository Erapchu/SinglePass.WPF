using System.Threading.Tasks;

namespace PasswordManager.Application
{
    public interface IFavIconRepository
    {
        public Task Add(FavIcon favIcon);
        public Task<FavIcon> Get(string host);
    }
}
