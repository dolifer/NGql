using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests
{
    public class MutationTests
    {
        [Fact]
        public void Ctor_Sets_Name()
        {
            // arrange
            var mutation = new Mutation("name");

            // assert
            mutation.Name.Should().Be("name");
        }

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
        public void Variable_AddsToVariableList()
        {
            // arrange
            var mutation = new Mutation("name");

            // act
            mutation
                .Variable("$name", "String")
                .Select("id");

            // assert
            mutation.Variables.Should().ContainSingle(x => x.Name == "$name" && x.Type == "String");
        }

        [Fact]
        public void Variable_Instance_AddsToVariableList()
        {
            // arrange
            var mutation = new Mutation("name");
            var variable = new Variable("$name", "String");

            // act
            mutation
                .Variable(variable)
                .Select("id");

            // assert
            mutation.Variables.Should().ContainSingle(x => x.Name == "$name" && x.Type == "String");
        }

        [Fact]
        public void ToString_UsesVariables()
        {
            // arrange
            var nameVar = new Variable("$name", "String");
            var passVar = new Variable("$password", "String");
            var nestedMutation = new Query("createUser")
                .Where("name", nameVar)
                .Where("password", passVar)
                .Select("id", "name");

            // act
            var queryText = new Mutation("CreateUser")
                .Variable(nameVar)
                .Variable(passVar)
                .Select(nestedMutation)
                .ToString();

            // assert
            queryText.Should().Be(@"mutation CreateUser($name:String, $password:String){
    createUser(name:$name, password:$password){
        id
        name
    }
}");
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
            string queryText = new Mutation("CreateUser")
                .Select(nestedMutation);

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
