using GraphQL;
using GraphQL.Types;
using MediatR;

namespace Server.Schema;

public sealed class ExtendedUsersType : ObjectGraphType
{
    public ExtendedUsersType(ISender sender)
    {
        Field<ListGraphType<UserType>>("extendedUsers")
            .Arguments(new QueryArguments(
                new QueryArgument<StringGraphType> { Name = "name", Description = "name of the user", DefaultValue = null }
            ))
            .ResolveAsync(async context => await sender.Send(new Commands.UsersQuery(context.GetArgument<string>("name"))));
    }
}
