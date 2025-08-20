using GraphQL.Types;
using Server.Data.Entities;

namespace Server.Schema;

public class UserType : ObjectGraphType<User>
{
    public UserType()
    {
        Field(o => o.Name);
    }
}