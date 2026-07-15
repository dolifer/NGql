using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: the dotted-path fast-path and metadata-path parsers sliced '.'-delimited
/// segments without an empty-segment guard, so inputs such as <c>"user..name"</c>,
/// <c>".user"</c>, <c>"a..b..c"</c> and <c>"user."</c> emitted syntactically invalid GraphQL
/// with anonymous selection sets (e.g. <c>user{ { name } }</c>).
///
/// The complex path already collapsed empty/whitespace segments, so identical input rendered
/// validly on one route and broken on another. The static <see cref="FieldBuilder.Create(Dictionary{string, FieldDefinition}, string, string, IDictionary{string, object?}?, Dictionary{string, object?}?)"/>
/// overload additionally bypassed segment validation entirely.
///
/// Chosen behavior is SKIP (collapse), not REJECT — matching the pre-existing complex-path
/// skip, the existing "handle gracefully without throwing" contract for empty dotted segments,
/// and the segment-skipping validator. All routes must agree.
/// </summary>
public class EmptyDottedSegmentTests
{
    [Theory]
    [InlineData("user..name", "user{ name }")]
    [InlineData(".user", "user")]
    [InlineData("user.", "user")]
    [InlineData("a..b..c", "a{ b{ c } }")]
    public void AddField_DottedPathWithEmptySegments_CollapsesToValidGraphQl(string path, string expectedShape)
    {
        // Arrange & Act
        var rendered = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField(path)
            .ToString();

        // Assert - no anonymous selection set (an empty "{" not immediately preceded by a name)
        rendered.Should().NotMatchRegex(@"\{\s*\{");
        // The surviving segments appear in the expected parent/child order.
        foreach (var segment in expectedShape.Replace("{", " ").Replace("}", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            rendered.Should().Contain(segment);
        }
    }

    [Fact]
    public void AddField_DoubleDot_RendersNestedFieldNotAnonymousSelectionSet()
    {
        // Arrange & Act
        var rendered = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user..name")
            .ToString();

        // Assert - the empty middle segment is collapsed: user directly contains name.
        var user = QueryBuilder.CreateDefaultBuilder("Q").AddField("user..name").Definition.Fields["user"];
        user.Fields.Should().ContainKey("name");
        rendered.Should().NotMatchRegex(@"\{\s*\{");
    }

    [Fact]
    public void AddField_LeadingDot_DropsEmptyRootSegment()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("Q").AddField(".user");

        // Assert - "user" is a root field, no empty-named parent wraps it.
        qb.Definition.Fields.Should().ContainKey("user");
        qb.Definition.Fields.Should().NotContainKey("");
    }

    [Fact]
    public void AddField_DoubleDotWithArguments_TakesMetadataPath_AndCollapses()
    {
        // Arrange & Act - non-null arguments route through the metadata dotted path, a distinct
        // parser from the argument-less fast path; it must collapse empty segments too.
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user..name", new Dictionary<string, object?> { ["x"] = 1 });

        // Assert
        var user = qb.Definition.Fields["user"];
        user.Fields.Should().ContainKey("name");
        user.Fields["name"].Arguments.Should().NotBeNull().And.ContainKey("x");
        qb.ToString().Should().NotMatchRegex(@"\{\s*\{");
    }

    [Fact]
    public void AllRoutes_Agree_AddField_And_StaticCreate_ProduceSameShape()
    {
        // Arrange - the same malformed input via the fluent AddField (fast path) and via the
        // static FieldBuilder.Create factory (previously bypassed validation) must agree.
        const string path = "user..profile..name";

        var viaAddField = QueryBuilder.CreateDefaultBuilder("Q").AddField(path);
        var viaAddFieldRendered = Normalize(viaAddField.ToString());

        var createFields = new Dictionary<string, FieldDefinition>();
        FieldBuilder.Create(createFields, path);

        // Assert - both collapse the empty segments into user -> profile -> name.
        viaAddField.Definition.Fields["user"].Fields["profile"].Fields.Should().ContainKey("name");
        createFields["user"].Fields["profile"].Fields.Should().ContainKey("name");
        viaAddFieldRendered.Should().NotContain("{ {");
    }

    [Fact]
    public void StaticCreate_DottedPathWithEmptySegments_NoLongerBypassesHandling()
    {
        // Arrange & Act - FieldBuilder.Create used to bypass segment handling entirely, so this
        // is the direct regression for that route.
        var fields = new Dictionary<string, FieldDefinition>();

        var act = () => FieldBuilder.Create(fields, "user..name");

        // Assert - handled gracefully, empty segment collapsed.
        act.Should().NotThrow();
        fields["user"].Fields.Should().ContainKey("name");
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void AddField_PathOfOnlyEmptySegments_Throws(string path)
    {
        // Arrange - a dotted path that collapses to nothing has no field to create; it is
        // rejected like a null/empty field name rather than dereferencing a null result.
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField(path);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private static string Normalize(string value)
        => value.Replace("\r", "").Replace("\n", "").Replace(" ", "");
}
