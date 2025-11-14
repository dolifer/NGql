using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using MediatR;

namespace Server.Schema;

public sealed class UsersQuery : ObjectGraphType
{
    public UsersQuery(ISender sender)
    {
        Field<ExtendedUsersType>("foo").ResolveAsync(context => Task.FromResult<object>(new { })!);
        Field<ExtendedUsersType>("bar").ResolveAsync(context => Task.FromResult<object>(new { })!);

        Field<ListGraphType<UserType>>("users").ResolveAsync(async context => await sender.Send(new Commands.UsersQuery()));

        Field<UserType>("user")
            .Arguments(new QueryArguments(
                new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "name", Description = "name of the user" }
            ))
            .ResolveAsync(async context => await sender.Send(new Commands.UserQuery(context.GetArgument<string>("name"))));
    }
}
