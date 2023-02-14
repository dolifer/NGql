using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Data.Entities;

namespace Server.Data
{
    public interface IUsersRepository
    {
        Task<IEnumerable<User>> GetUsers(string? name);
        Task<User?> GetUser(string name);
        Task<User> CreateUser(User user);
    }
}
