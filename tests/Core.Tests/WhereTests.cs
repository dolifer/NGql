using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests
{
    public class WhereTests
    {
        [Fact]
        public void Where_NumberArgument_AddsToWhere()
        {
            // arrange
            var query = new Query("name");

            // act
            query.Where("id", 42);

            // assert
            query.Arguments.Should().ContainKey("id").WhoseValue.Should().Be(42);

            // act
            var queryText = query.ToString();

            // assert
            queryText.Should().Be(@"query name(id:42){
}");
        }

        [Fact]
        public void Where_StringArgument_AddsToWhere()
        {
            // arrange
            var query = new Query("name");

            // act
            query.Where("name", "John");

            // assert
            query.Arguments.Should().ContainKey("name").WhoseValue.Should().Be("John");
        }

        [Fact]
        public void Where_Dictionary_AddsToWhere()
        {
            // arrange
            var query = new Query("name");
            Dictionary<string, object> ageFilter = new()
            {
                {"from", 1},
                {"to", 100}
            };

            // act
            query.Where(ageFilter);

            // assert
            query.Arguments.Should().ContainKey("from").WhoseValue.Should().Be(1);
            query.Arguments.Should().ContainKey("to").WhoseValue.Should().Be(100);
        }

        [Fact]
        public void Where_DictionaryArgument_AddsToWhere()
        {
            // arrange
            var query = new Query("name");
            Dictionary<string, int> ageFilter = new()
            {
                {"from", 1},
                {"to", 100}
            };

            // act
            query.Where("age", ageFilter);

            // assert
            var storedFilter = query.Arguments.Should().ContainKey("age").WhoseValue as Dictionary<string, int>;

            storedFilter.Should().ContainKey("from").WhoseValue.Should().Be(1);
            storedFilter.Should().ContainKey("to").WhoseValue.Should().Be(100);
        }
    }
}
