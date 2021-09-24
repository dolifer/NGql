using GraphQL;
using GraphQL.Types;
using MediatR;
using Server.Commands;

namespace Server.Schema
{
    public class UserMutation : ObjectGraphType<object>
    {
        public UserMutation(ISender sender)
        {
            FieldAsync<UserType>("createUser",
                arguments: new QueryArguments(
                    new QueryArgument<StringGraphType> { Name = "name" }
                ),
                resolve: async context =>
                {
                    var name = context.GetArgument<string>("name");
                    return await sender.Send(new CreateUserCommand(name));
                });
        }
    }
}
