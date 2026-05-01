using System.Linq;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: <c>QueryDefinition.Fields</c> and <c>FieldDefinition.Fields</c> must enumerate
/// case-insensitively sorted by key regardless of insertion sequence.
///
/// Adapter code in production relies on <c>FirstOrDefault()</c> and <c>foreach</c> over these
/// collections returning a deterministic alphabetical first element to identify the primary
/// trait field after a merge. An earlier perf change swapped the storage to a hash-based
/// dictionary which silently changed iteration order to insertion order and broke the contract.
/// </summary>
public class FieldsSortedIterationTests
{
    [Fact]
    public void QueryDefinition_Fields_FirstOrDefault_ReturnsAlphabeticallyFirstKey()
    {
        // Insert in reverse-alphabetical order; expect "alpha" first when iterated.
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        qb.AddField("zeta");
        qb.AddField("mike");
        qb.AddField("alpha");

        var first = System.Linq.Enumerable.FirstOrDefault(qb.Definition.Fields);

        first.Key.Should().Be("alpha");
    }

    [Fact]
    public void QueryDefinition_Fields_Foreach_IteratesInSortedOrder()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        qb.AddField("zeta");
        qb.AddField("mike");
        qb.AddField("alpha");
        qb.AddField("bravo");

        var keys = qb.Definition.Fields.Select(kvp => kvp.Key).ToArray();

        keys.Should().Equal("alpha", "bravo", "mike", "zeta");
    }

    [Fact]
    public void FieldDefinition_Fields_Foreach_IteratesInSortedOrder()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("parent.zeta")
            .AddField("parent.mike")
            .AddField("parent.alpha");

        var parent = qb.Definition.Fields["parent"];
        var keys = parent.Fields.Select(kvp => kvp.Key).ToArray();

        keys.Should().Equal("alpha", "mike", "zeta");
    }
}
