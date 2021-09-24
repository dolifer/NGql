using System;
using MediatR;

namespace Server.Schema
{
    public class DemoSchema : GraphQL.Types.Schema
    {
        public DemoSchema(IMediator mediator, IServiceProvider provider) : base(provider)
        {
            Query = new UsersQuery(mediator);
            Mutation = new UserMutation(mediator);
        }
    }
}
