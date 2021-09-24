using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NGql.Client.Tests.Fixtures;
using NGql.Core;
using Server.Data.Entities;
using Xunit;

namespace NGql.Client.Tests
{
    public class GraphQLClientTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _fixture;

        public GraphQLClientTests(ApiFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_Get_Users()
        {
            // arrange
            using var graphQLClient = GetClient();
            var query = new Query("getAllUsers")
                .Select(new Query("users")
                    .Select("name")
                );

            // act
            var response = await graphQLClient.QueryAsync<JObject>(query);
            var users = response.SelectToken("users")?.ToObject<User[]>();

            // assert
            users.Should().NotBeNull();
            users.Should().Contain(u => u.Name == "Ila Santana");
         }

        [Fact]
        public async Task Can_Get_User()
        {
            // arrange
            using var graphQLClient = GetClient();
            var query = new Query("getUser")
                .Select(new Query("user", "alias")
                    .Where("name", "Yoshi Lambert")
                    .Select("name")
                );

            // act
            var response = await graphQLClient.QueryAsync<JObject>(query);
            var user = response.SelectToken("alias")?.ToObject<User>();

            // assert
            user.Should().NotBeNull();
            user.Name.Should().Be("Yoshi Lambert");
        }

        [Fact]
        public async Task Can_Get_User_Using_Variable()
        {
            // arrange
            using var graphQLClient = GetClient();
            var nameVariable = new Variable("$name", "String!");
            var query = new Query("getUser")
                .Variable(nameVariable)
                .Select(new Query("user")
                    .Where("name", nameVariable)
                    .Select("name")
                );

            var variables = new
            {
                name = "Yoshi Lambert"
            };

            // act
            var response = await graphQLClient.QueryAsync<JObject>(query, variables);
            var user = response.SelectToken("user")?.ToObject<User>();

            // assert
            user.Should().NotBeNull();
            user.Name.Should().Be("Yoshi Lambert");
        }

        [Fact]
        public async Task Can_Create_User_Using_Mutation()
        {
            // arrange
            using var graphQLClient = GetClient();
            var nameVariable = new Variable("$name", "String!");
            var query = new Mutation("Create")
                .Variable(nameVariable)
                .Select(new Query("createUser", "user")
                    .Where("name", nameVariable)
                    .Select("name")
                );

            var variables = new
            {
                name = "Ryan Aguilar"
            };

            // act
            var response = await graphQLClient.QueryAsync<JObject>(query, variables);
            var user = response.SelectToken("user")?.ToObject<User>();

            // assert
            user.Should().NotBeNull();
            user.Name.Should().Be("Ryan Aguilar");
        }

        private INGqlClient GetClient() => new NGqlClient("https://localhost:5001/graphql", _fixture.CreateClient());
    }
}
