﻿using System;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using NGql.Client.Tests.Extensions;
using NGql.Client.Tests.Fixtures;
using NGql.Core;
using Server.Data.Entities;
using Xunit;

namespace NGql.Client.Tests
{
    public class QueryTests : IClassFixture<ApiFixture>
    {
        private readonly ApiFixture _fixture;

        public QueryTests(ApiFixture fixture) => _fixture = fixture;

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
            var request = new GraphQLRequest
            {
                Query = query
            };
            var response = await graphQLClient.SendQueryAsync<JsonElement>(request);
            var users = response.Data.ToObject<User[]>("users");

            // assert
            users.Should().NotBeNull();
            users.Should().Contain(u => u.Name == "Ila Santana");
            users.Should().Contain(u => u.Name == "Laurel Gardner");
            users.Should().Contain(u => u.Name == "Winter Bryant");
        }
        
        [Fact]
        public async Task Can_Get_Nested_Users()
        {
            // arrange
            using var graphQLClient = GetClient();
            var query = new Query("getAllUsers")
                .Select(new Query("foo")
                    .Select(new Query("extendedUsers", "bar")
                        .Where("name", "Yoshi Lambert")
                        .Select("name")
                    ));

            // act
            var request = new GraphQLRequest
            {
                Query = query
            };
            var response = await graphQLClient.SendQueryAsync<JsonElement>(request);
            var users = response.Data.GetProperty("foo").ToObject<User[]>("bar");

            // assert
            users.Should().NotBeNull();
            users.Should().Contain(u => u.Name == "Yoshi Lambert");
            users.Should().NotContain(u => u.Name == "Laurel Gardner");
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
            var request = new GraphQLRequest
            {
                Query = query
            };
            var response = await graphQLClient.SendQueryAsync<JsonElement>(request);
            var user = response.Data.ToObject<User>("alias");

            // assert
            user.Should().NotBeNull();
            user?.Name.Should().Be("Yoshi Lambert");
        }

        [Fact]
        public async Task Can_Get_User_Returns_Null()
        {
            // arrange
            using var graphQLClient = GetClient();
            var query = new Query("getUser")
                .Select(new Query("user", "alias")
                    .Where("name", "Ezra Smith")
                    .Select("name")
                );

            // act
            var request = new GraphQLRequest
            {
                Query = query
            };
            var response = await graphQLClient.SendQueryAsync<JsonElement>(request);
            var user = response.Data.ToObject<User>("alias");

            // assert
            user.Should().BeNull();
        }

        [Theory]
        [InlineData("Yoshi Lambert")]
        [InlineData("Dean David")]
        [InlineData("Maya Klein")]
        public async Task Can_Get_User_Using_Variable(string name)
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

            // act
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = new
                {
                    name
                }
            };
            var response = await graphQLClient.SendQueryAsync<JsonElement>(request);
            var user = response.Data.ToObject<User>("user");

            // assert
            user.Should().NotBeNull();
            user!.Name.Should().Be(name);
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

            // act
            var request = new GraphQLRequest
            {
                Query = query,
                Variables = new
                {
                    name = "Ryan Aguilar"
                }
            };
            var response = await graphQLClient.SendQueryAsync<JsonElement>(request);
            var user = response.Data.ToObject<User>("user");

            // assert
            user.Should().NotBeNull();
            user!.Name.Should().Be("Ryan Aguilar");
        }

        private GraphQLHttpClient GetClient() => new(
            new GraphQLHttpClientOptions
            {
                EndPoint = new Uri("https://localhost:5001/graphql")
            },
            new SystemTextJsonSerializer(),
            _fixture.CreateClient()
        );
    }
}
