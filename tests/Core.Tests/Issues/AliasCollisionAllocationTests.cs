using System;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class AliasCollisionAllocationTests
{
    [Fact]
    public void GeneratedAlias_FieldSpanUsesEffectiveNames()
    {
        var fields = new[]
        {
            new FieldDefinition("first", alias: "ITEM"),
            new FieldDefinition("second", alias: "item_1")
        };
        KeyGenerator.GenerateUniqueKey("item", fields.AsSpan()).Should().Be("item_2");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(496)]
    [InlineData(497)]
    [InlineData(100000)]
    public void GeneratedAlias_HandlesShortAndLongNames(int length)
    {
        var name = new string('x', length);
        var result = KeyGenerator.GenerateUniqueKey(name, Aliased(name, name + "_1"));
        result.Should().Be(name + "_2");
    }

    [Fact]
    public void GeneratedAlias_UsesFirstFreeSuffixWithCaseInsensitiveLookup()
    {
        KeyGenerator.GenerateUniqueKey("item", Aliased("ITEM", "item_1", "ITEM_3"))
            .Should().Be("item_2");
        KeyGenerator.GenerateUniqueKey("item", Aliased("other")).Should().Be("item");
    }

    private static ReadOnlySpan<FieldDefinition> Aliased(params string[] aliases)
        => Array.ConvertAll(aliases, alias => new FieldDefinition("field", alias: alias));
}
