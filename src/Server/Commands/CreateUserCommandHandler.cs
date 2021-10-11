using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Server.Data;
using Server.Data.Entities;

namespace Server.Commands
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, User>
    {
        private readonly IUsersRepository _repository;

        public CreateUserCommandHandler(IUsersRepository repository) => _repository = repository;

        public Task<User> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var user = new User
            {
                Name = request.Name
            };

            return _repository.CreateUser(user);
        }
    }
}
