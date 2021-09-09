using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests
{
    public class MutationTests
    {
        [Fact]
        public void Select_String_AddsToSelectList()
        {
            // arrange
            var mutation = new Mutation("name");

            // act
            mutation.Select("id", "name");

            // assert
            mutation.FieldsList.Should().BeEquivalentTo(new[] { "id", "name" });
        }

        [Fact]
        public void Select_List_AddsToSelectList()
        {
            // arrange
            var mutation = new Mutation("name");

            // act
            mutation.Select(new List<string> {"id", "name"});

            // assert
            mutation.FieldsList.Should().BeEquivalentTo(new[] { "id", "name" });
        }

        [Fact]
        public void ToString_Returns_SubQuery()
        {
            // arrange
            var nestedMutation = new Query("createUser")
                .Where("name", "Name")
                .Where("password", "Password")
                .Select("id", "name");

            // act
            var queryText = new Mutation("CreateUser")
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
