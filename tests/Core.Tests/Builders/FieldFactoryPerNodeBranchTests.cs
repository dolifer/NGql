using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldFactoryPerNodeBranchTests
{
    [Fact]
    public void AddField_NestedDottedPathWithEmptySegments_CollapsesThem()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.AddField("profile..settings...privacy"));

        var result = query.ToString();

        result.Should().Contain("privacy");
        result.Should().NotContain("..");
    }

    [Fact]
    public void AddField_NestedDottedPathWithEmptySegmentsAndArguments_CollapsesThem()
    {
        var args = new Dictionary<string, object?> { ["id"] = 7 };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.AddField("profile..settings", args));

        var result = query.ToString();

        result.Should().Contain("settings");
        result.Should().Contain("id:7");
    }

    [Fact]
    public void AddField_NestedDottedPathWithEmptySegmentsAndMetadata_CollapsesThem()
    {
        var metadata = new Dictionary<string, object?> { ["tag"] = "x" };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.AddField("profile...settings", null, metadata));

        var result = query.ToString();

        result.Should().Contain("profile");
        result.Should().Contain("settings");
    }

    [Fact]
    public void AddField_NestedPathOfOnlyEmptySegmentsWithArguments_LeavesParentUnchanged()
    {
        var args = new Dictionary<string, object?> { ["id"] = 1 };
        FieldBuilder? inner = null;

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b =>
            {
                b.AddField("name");
                inner = b.AddField("..", args);
            });

        inner.Should().NotBeNull();
        query.ToString().Should().Contain("name");
    }

    [Fact]
    public void Create_RootPathOfOnlyEmptySegmentsWithMetadata_Throws()
    {
        var fields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
        var metadata = new Dictionary<string, object?> { ["tag"] = "x" };

        var act = () => FieldBuilder.Create(fields, "..", "String", null, metadata);

        act.Should().Throw<ArgumentException>().WithMessage("Field cannot be null or empty*");
    }

    [Fact]
    public void AddField_SameNameDifferentAliasesUnderSameParent_CreatesDistinctSiblings()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.aliasB:id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().Contain("aliasB:id");
    }

    [Fact]
    public void AddField_ThirdAliasAfterTwoAliasedSiblings_CreatesAnotherSibling()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.aliasB:id")
            .AddField("user.aliasC:id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().Contain("aliasB:id");
        result.Should().Contain("aliasC:id");
    }

    [Fact]
    public void AddField_RepeatedAliasAfterAnotherAliasedSibling_ReusesExistingSibling()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.aliasB:id")
            .AddField("user.aliasA:id");

        var result = query.ToString();

        result.Split("aliasA:id").Should().HaveCount(2);
    }

    [Fact]
    public void AddField_RepeatedSecondAliasAfterFirstAliasedSibling_ReusesSecondSibling()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.aliasB:id")
            .AddField("user.aliasB:id");

        var result = query.ToString();

        result.Split("aliasB:id").Should().HaveCount(2);
        result.Should().Contain("aliasA:id");
    }

    [Fact]
    public void AddField_AliasedSegmentAfterUnaliasedSibling_AdoptsAliasOnExistingField()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id")
            .AddField("user.aliasA:id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().NotMatchRegex(@"(?m)^\s+id\s*$");
    }

    [Fact]
    public void AddField_ComplexPathWithUnaliasedLastSegmentAfterAliasedSibling_CreatesUnaliasedSibling()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("u:user.id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().MatchRegex(@"(?m)^\s+id\s*$");
    }

    [Fact]
    public void AddField_ComplexPathWithUnaliasedLastSegmentAfterTwoAliasedSiblings_CreatesUnaliasedSibling()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.aliasB:id")
            .AddField("u:user.id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().Contain("aliasB:id");
        result.Should().MatchRegex(@"(?m)^\s+id\s*$");
    }

    [Fact]
    public void AddField_UnaliasedSegmentAfterAliasedSibling_ResolvesToAliasedField()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().NotMatchRegex(@"(?m)^\s+id\s*$");
    }

    [Fact]
    public void AddField_UnaliasedSegmentAfterTwoAliasedSiblings_ResolvesToFirstAliasedField()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.aliasA:id")
            .AddField("user.aliasB:id")
            .AddField("user.id");

        var result = query.ToString();

        result.Should().Contain("aliasA:id");
        result.Should().Contain("aliasB:id");
        result.Should().NotMatchRegex(@"(?m)^\s+id\s*$");
    }
}
