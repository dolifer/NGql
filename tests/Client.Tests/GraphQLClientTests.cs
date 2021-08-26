using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core;
using Xunit;

namespace NGql.Client.Tests
{
    public class GraphQLClientTests
    {
        public class PersonAndFilmsResponse
        {
            public PersonContent Person { get; set; }

            public class PersonContent
            {
                public string Name { get; set; }

                public FilmConnectionContent FilmConnection { get; set; }

                public class FilmConnectionContent
                {
                    public List<FilmContent> Films { get; set; }

                    public class FilmContent
                    {
                        public string Title { get; set; }
                    }
                }
            }
        }

        [Fact]
        public async Task Can_Get_Films()
        {
            // arrange
            using var graphQLClient = GetClient();
            var query = new Query("PersonAndFilms")
                .Select(new Query("person")
                    .Where("id", "cGVvcGxlOjE=")
                    .Select("name")
                    .Select(new Query("filmConnection")
                        .Select(new Query("films")
                            .Select("title")))
                );

            // act
            var response = await graphQLClient.QueryAsync<PersonAndFilmsResponse>(query);

            // assert
            AssertResponse(response);
        }

        [Fact]
        public async Task Can_Get_Films_IncludeSyntax()
        {
            // arrange
            using var client = GetClient();
            var query = new Query("PersonAndFilms")
                .Include("person", p => p
                    .Where("id", "cGVvcGxlOjE=")
                    .Select("name")
                    .Include("filmConnection", m =>
                        m.Include("films", f => f.Select("title")))
                );

            // act
            var response = await client.QueryAsync<PersonAndFilmsResponse>(query);

            // assert
            AssertResponse(response);
        }

        private static INGqlClient GetClient() =>
            new NGqlClient("https://swapi-graphql.netlify.app/.netlify/functions/index");

        private static void AssertResponse(PersonAndFilmsResponse response)
        {
            response.Person.Name.Should().Be("Luke Skywalker");
            response.Person.FilmConnection.Films.Should().NotBeNull();
            response.Person.FilmConnection.Films.Select(m => m.Title)
                .Should()
                .BeEquivalentTo(new object[]
                {
                    "A New Hope",
                    "The Empire Strikes Back",
                    "Return of the Jedi",
                    "Revenge of the Sith"
                });
        }
    }
}
