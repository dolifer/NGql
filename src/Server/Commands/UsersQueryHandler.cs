using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Server.Data;
using Server.Data.Entities;

namespace Server.Commands
{
    public class UserQueryHandler : IRequestHandler<UserQuery, User?>
    {
        private readonly IUsersRepository _repository;

        public UserQueryHandler(IUsersRepository repository)
            => _repository = repository;

        public async Task<User?> Handle(UserQuery request, CancellationToken cancellationToken)
            => await _repository.GetUser(request.Name);
    }

    public class UsersQueryHandler : IRequestHandler<UsersQuery, IEnumerable<User>>
    {
        private readonly IUsersRepository _repository;

        public UsersQueryHandler(IUsersRepository repository)
            => _repository = repository;

        public async Task<IEnumerable<User>> Handle(UsersQuery request, CancellationToken cancellationToken) 
            => await _repository.GetUsers(request.Name);
    }
}
