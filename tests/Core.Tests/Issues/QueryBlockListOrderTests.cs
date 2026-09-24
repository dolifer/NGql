using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Adding a list of fields sorted it with LINQ <c>OrderBy</c> before inserting each item. The
/// sort is now an array sort; it must keep OrderBy's key, comparer and stability.
/// </summary>
public class QueryBlockListOrderTests
{
    [Fact]
    public void AddField_RandomMixedList_MatchesOrderByThenIndividualAdds()
    {
        string[] names = ["alpha", "Alpha", "beta", "_id", "Beta", "gamma", "alpha", "éclair", "Zeta", "10", "9"];
        var random = new Random(20260924);

        for (var round = 0; round < 200; round++)
        {
            var items = new List<object>();
            var count = random.Next(1, 12);
            for (var i = 0; i < count; i++)
            {
                var name = names[random.Next(names.Length)];
                items.Add(random.Next(3) == 0 ? CreateSubQuery(name, i) : name);
            }

            var actual = new QueryBlock("root");
            actual.AddField(items);

            var expected = new QueryBlock("root");
            foreach (var item in items.OrderBy(x => x is QueryBlock q ? q.Name : (string)x))
            {
                if (item is QueryBlock subQuery) expected.AddField(subQuery);
                else expected.AddField((string)item);
            }

            actual.ToString().Should().Be(expected.ToString());
        }
    }

    [Fact]
    public void AddField_ListWithNullItem_ThrowsNullReference()
    {
        var block = new QueryBlock("root");

        var act = () => block.AddField(new List<object> { "id", null! });

        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void ArgumentsAndVariables_ReadBeforeAdding_AreLiveViews()
    {
        var block = new QueryBlock("root");
        var arguments = block.Arguments;
        var variables = block.Variables;

        block.AddArgument("id", new Variable("$id", "ID"));

        arguments.Should().ContainKey("id");
        variables.Should().ContainSingle(v => v.Name == "$id");
    }

    private static QueryBlock CreateSubQuery(string name, int marker)
    {
        var subQuery = new QueryBlock(name);
        subQuery.AddField("marker" + marker);
        return subQuery;
    }
}
