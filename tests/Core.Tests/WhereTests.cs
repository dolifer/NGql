using System.Collections.Generic;
using FluentAssertions;
using NGql.Core;
using Xunit;

namespace Core.Tests
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
            query.Arguments.Should().ContainKey("id").WhichValue.Should().Be(42);

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
            query.Arguments.Should().ContainKey("name").WhichValue.Should().Be("John");
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
            var storedFilter = query.Arguments.Should().ContainKey("age").WhichValue as Dictionary<string, int>;

            storedFilter.Should().ContainKey("from").WhichValue.Should().Be(1);
            storedFilter.Should().ContainKey("to").WhichValue.Should().Be(100);
        }
    }
}
