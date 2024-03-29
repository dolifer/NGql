﻿using System.Collections.Generic;
using MediatR;
using Server.Data.Entities;

namespace Server.Commands
{
    public class UsersQuery : IRequest<IEnumerable<User>>
    {
        public string? Name { get; }

        public UsersQuery(string? name = null)
        {
            Name = name;
        }
    }

    public class UserQuery : IRequest<User>
    {
        public string Name { get; }

        public UserQuery(string name) => Name = name;
    }
}
