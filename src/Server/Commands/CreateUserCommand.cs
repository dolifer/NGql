using MediatR;
using Server.Data.Entities;

namespace Server.Commands;

public class CreateUserCommand : IRequest<User>
{
    public CreateUserCommand(string name) => Name = name;

    public string Name { get; }
}