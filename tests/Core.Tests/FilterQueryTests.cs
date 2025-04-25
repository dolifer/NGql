using System.Collections.Generic;
using System.Threading.Tasks;
using Shared;
using VerifyTests;
using VerifyXunit;
using Xunit;

namespace NGql.Core.Tests;

public class FilterQueryTests
{
    [Fact]
    public Task Filter_Dictionary_Syntax()
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

        // act & assert
        return query.Verify("filterQuery");
    }

    [Fact]
    public Task Filter_Simplified_Syntax()
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

        // act & assert
        return query.Verify("filterQuery");
    }
}
