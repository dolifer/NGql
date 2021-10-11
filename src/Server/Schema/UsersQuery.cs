﻿using GraphQL;
using GraphQL.Types;
using MediatR;

namespace Server.Schema
{
    public class UsersQuery : ObjectGraphType
    {
        public UsersQuery(ISender sender)
        {
            FieldAsync<ListGraphType<UserType>>("users", resolve: async context => await sender.Send(new Commands.UsersQuery()));

            FieldAsync<UserType>(
                "user",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>> { Name = "name", Description = "name of the user" }
                ),
                resolve: async context => await sender.Send(new Commands.UserQuery(context.GetArgument<string>("name")))
            );
        }
    }
}
