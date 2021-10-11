using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Data.Entities;

namespace Server.Data
{
    public interface IUsersRepository
    {
        Task<IEnumerable<User>> GetUsers(int count);
        Task<User?> GetUser(string name);
        Task<User> CreateUser(User user);
    }
}
