using GraphQL;
using GraphQL.Types;
using MediatR;
using Server.Commands;

namespace Server.Schema;

public sealed class UserMutation : ObjectGraphType<object>
{
    public UserMutation(ISender sender)
    {
        Field<UserType>("createUser")
            .Arguments(new QueryArguments(
                new QueryArgument<StringGraphType> { Name = "name" }
            ))
            .ResolveAsync(async context =>
            {
                var name = context.GetArgument<string>("name");
                return await sender.Send(new CreateUserCommand(name));
            });
    }
}
