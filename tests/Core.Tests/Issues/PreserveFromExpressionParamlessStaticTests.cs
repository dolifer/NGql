using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class PreserveFromExpressionParamlessStaticTests
{
    public sealed class Box
    {
        public int Val { get; set; }
    }

    public sealed class Model
    {
        public int a { get; set; }
        public int b { get; set; }
    }

    private static Box Give() => new();

    [Fact]
    public void PreserveFromExpression_ParamlessStaticCall_DoesNotThrow_And_PreservesMemberPath()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("a")
            .AddField("b");

        // Act - the predicate invokes a parameterless static method (Give()) in a binary
        // expression alongside a normal member-access path (x.a). The static call must not
        // crash extraction and must contribute no path of its own.
        var build = () => PreservationBuilder.Create(query)
            .PreserveFromExpression<Model>(x => Give().Val > 0 && x.a > 0)
            .Build();

        // Assert
        var result = build.Should().NotThrow().Which;
        var rendered = result.ToString();
        rendered.Should().Contain("a");
        rendered.Should().NotContain("b");
        rendered.Should().NotContain("Val");
    }
}
