using GraphQL;
using GraphQL.Types;
using MediatR;

namespace Server.Schema;

public class ExtendedUsersType : ObjectGraphType
{
    public ExtendedUsersType(ISender sender)
    {
        FieldAsync<ListGraphType<UserType>>("extendedUsers", 
            arguments: new QueryArguments(
                new QueryArgument<StringGraphType> { Name = "name", Description = "name of the user", DefaultValue = null}
            ),
            resolve: async context => await sender.Send(new Commands.UsersQuery(context.GetArgument<string>("name"))));
    }
}
