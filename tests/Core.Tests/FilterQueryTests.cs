using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests;

public class FilterQueryTests
{
    [Fact]
    public void Filter_Dictionary_Syntax()
    {
        // arrange
        var query = new Query("users")
            .Select(new List<string> { "id", "name" })
            .Where("filter", new Dictionary<string, object>
            {
                {
                    "playerId", new Dictionary<string, object>
                    {
                        { "in", new[] { "1", "2" } }
                    }
                },
                {
                    "or", new[]
                    {
                        new Dictionary<string, object>
                        {
                            {
                                "name", new Dictionary<string, object>
                                {
                                    { "isNull", true }

                                }
                            }
                        },
                        new Dictionary<string, object>
                        {
                            {
                                "name", new Dictionary<string, object>
                                {
                                    { "equals", "John" }
                                }
                            }
                        }
                    }
                }
            });

        // act
        string queryText = query;

        // assert
        queryText.Should().Be(@"query users(filter:{playerId:{in:[""1"", ""2""]}, or:[{name:{isNull:true}}, {name:{equals:""John""}}]}){
    id
    name
}");
    }

    [Fact]
    public void Filter_Simplified_Syntax()
    {
        // arrange
        var query = new Query("users")
            .Select("id", "name")
            .Where("filter", new
            {
                playerId = new
                {
                    @in = new[] { "1", "2" }
                },
                or = new object[]
                {
                    new
                    {
                        name = new
                        {
                            isNull = true
                        }
                    },
                    new
                    {
                        name = new
                        {
                            equals = "John"
                        }
                    }
                }
            });

        // act
        string queryText = query;

        // assert
        queryText.Should().Be(@"query users(filter:{playerId:{in:[""1"", ""2""]}, or:[{name:{isNull:true}}, {name:{equals:""John""}}]}){
    id
    name
}");
    }
}
