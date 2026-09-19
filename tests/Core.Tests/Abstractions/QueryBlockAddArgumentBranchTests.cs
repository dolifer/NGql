using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class QueryBlockAddArgumentBranchTests
{
    [Fact]
    public void AddArgument_SingleEntryDictionary_AddsArgument()
    {
        var block = new QueryBlock("user");

        block.AddArgument(new Dictionary<string, object> { ["id"] = 42 });

        block.Arguments.Should().ContainKey("id").WhoseValue.Should().Be(42);
    }

    [Fact]
    public void AddArgument_MultiEntryDictionary_AddsAllArguments()
    {
        var block = new QueryBlock("user");

        block.AddArgument(new Dictionary<string, object> { ["id"] = 42, ["name"] = "bob" });

        block.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void AddArgument_MultiEntryDictionaryCollidingWithStoredKeyCasing_Throws()
    {
        var block = new QueryBlock("user");
        block.AddArgument("Id", 1);

        var act = () => block.AddArgument(new Dictionary<string, object> { ["id"] = 2, ["name"] = "bob" });

        act.Should().Throw<ArgumentException>()
            .WithMessage("An item with the same key has already been added. Colliding key: 'id'.*");
    }

    [Fact]
    public void AddArgument_MultiEntryDictionaryCollidingWithStoredKeyCasing_LeavesBlockUntouched()
    {
        var block = new QueryBlock("user");
        block.AddArgument("Id", 1);

        var act = () => block.AddArgument(new Dictionary<string, object> { ["id"] = 2, ["name"] = "bob" });

        act.Should().Throw<ArgumentException>();
        block.Arguments.Should().HaveCount(1);
        block.Arguments.Should().ContainKey("Id").WhoseValue.Should().Be(1);
    }

    [Fact]
    public void AddArgument_MultiEntryDictionaryMatchingStoredKeyExactly_Overwrites()
    {
        var block = new QueryBlock("user");
        block.AddArgument("id", 1);

        block.AddArgument(new Dictionary<string, object> { ["id"] = 2, ["name"] = "bob" });

        block.Arguments.Should().ContainKey("id").WhoseValue.Should().Be(2);
    }

    [Fact]
    public void AddArgument_MultiEntryDictionaryWithInternallyCollidingKeys_Throws()
    {
        var block = new QueryBlock("user");

        var act = () => block.AddArgument(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["id"] = 1,
            ["ID"] = 2,
        });

        act.Should().Throw<ArgumentException>()
            .WithMessage("An item with the same key has already been added. Colliding key: 'ID'.*");
    }
}
