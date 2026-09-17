using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class ArgumentMetadataReuseTests
{
    private sealed class Filter
    {
        public string Name { get; set; } = "Alice";
        public Variable Cursor { get; set; } = new("$first", "String");
    }

    [Fact]
    public void ReusedMetadataReadsCurrentValuesAndVariables()
    {
        var filter = new Filter();
        var firstVariables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(filter, firstVariables);
        var first = Helpers.SortArgumentValue(filter);

        filter.Name = "Bob";
        filter.Cursor = new Variable("$second", "String");
        var secondVariables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(filter, secondVariables);
        var second = Helpers.SortArgumentValue(filter);

        firstVariables.Should().ContainSingle().Which.Name.Should().Be("$first");
        secondVariables.Should().ContainSingle().Which.Name.Should().Be("$second");
        first.Should().BeOfType<SortedDictionary<string, object?>>()
            .Which["Name"].Should().Be("Alice");
        second.Should().BeOfType<SortedDictionary<string, object?>>()
            .Which["Name"].Should().Be("Bob");
    }
}
