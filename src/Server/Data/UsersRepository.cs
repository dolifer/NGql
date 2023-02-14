using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Server.Data.Entities;

namespace Server.Data
{
    public class UsersRepository : IUsersRepository
    {
        private readonly string[] _users = {
           "Anne Ware",
           "Beverly Montgomery",
           "Chanda Gomez",
           "Clinton Wood",
           "Dawn English",
           "Dean David",
           "Ezra Boone",
           "Gisela Little",
           "Ila Santana",
           "Joseph Kim",
           "Laurel Gardner",
           "Lois Smith",
           "Maya Klein",
           "Quynn West",
           "Rowan Aguilar",
           "Walter Parrish",
           "Winter Bryant",
           "Winter Mccray",
           "Yoshi Lambert"
        };

        private readonly Dictionary<string, User> _store;

        public UsersRepository()
            => _store = _users.ToDictionary(x => x, x => new User { Name = x });

        public Task<User?> GetUser(string name)
        {
            if (_store.TryGetValue(name, out var user))
                return Task.FromResult<User?>(user);

            return Task.FromResult<User?>(null);
        }

        Task<IEnumerable<User>> IUsersRepository.GetUsers(string? name)
        {
            var users = _store.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                users = users.Where(x => x.Name.Contains(name, StringComparison.InvariantCultureIgnoreCase));
            }

            return Task.FromResult(users);
        }

        public Task<User> CreateUser(User user)
        {
            _store[user.Name] = user;
            return Task.FromResult(user);
        }
    }
}
