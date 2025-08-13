using FluentAssertions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Builders
{
    public class QueryBuilderGetPathToTests
    {
        [Theory]
        [InlineData("user.profile.edges", "edges", "user,profile")]
        [InlineData("user.profile.edges.id", "id", "user,profile,edges")]
        [InlineData("user.profile.edges.Id:id", "id", "user,profile,edges")]
        [InlineData("TUsers:user.PAlias:profile.edges.Id:id", "id", "TUsers,PAlias,edges")]
        public void GetPathTo_WithAliasedField_ShouldReturnAlias(string field, string query, string expected)
        {
            // Arrange
            var expectedItems = expected.Split(',');
            var builder = CreateDefaultBuilder("TestQuery");
            builder.AddField(field, ["String FullName:name", "Int age"]);

            // Act
            var path = builder.GetPathTo("TestQuery", query);

            // Assert
            path.Should().BeEquivalentTo(expectedItems);
        }
    }
}
