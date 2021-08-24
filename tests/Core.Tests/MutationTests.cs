using FluentAssertions;
using NGql.Core;
using Xunit;

namespace Core.Tests
{
    public class MutationTests
    {
        [Fact]
        public void ToString_Returns_SubQuery()
        {
            // arrange
            var mutation = new Mutation("CreateUser");
            var nestedMutation = new Query("createUser")
                .Where("name", "Name")
                .Where("password", "Password")
                .Select("id", "name");

            // act
            var queryText = mutation
                .Select(nestedMutation)
                .ToString();

            // assert
            queryText.Should().Be(@"mutation CreateUser{
    createUser(name:""Name"", password:""Password""){
        id
        name
    }
}");
        }
    }
}
