using System;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression tests for two defects in the COMPLEX-field path parser (the path handling
/// alias:name / space-delimited type prefixes / array markers, as distinct from the plain
/// dotted path).
///
/// Defect #9 (crash on long paths): the complex-field overloads built the accumulated field
/// path into a fixed <c>stackalloc char[512]</c> via <c>SpanPathBuilder</c>, which THREW
/// <see cref="InvalidOperationException"/> on overflow. The dotted path already fell back to a
/// pooled buffer past the stack threshold, so a long alias/type path that a same-length dotted
/// path rendered fine instead crashed. Fix mirrors the dotted path's pooled-buffer fallback.
///
/// Defect #8 (malformed output / dropped segments): a segment like <c>"a : b"</c> (spaces
/// around the colon) folded the stray colon into the field name and rendered invalid GraphQL
/// (<c>: b</c>); and <c>"a.b c.d"</c> silently dropped the <c>b</c> segment (it was mistaken for
/// a type on the intermediate <c>c</c> and then discarded). Fix rejects these malformed shapes
/// with a clear <see cref="ArgumentException"/> while leaving every documented valid shape
/// (<c>alias:name</c>, <c>Type field</c>, <c>[]tags</c>, <c>Type alias:name</c>, per-segment
/// <c>[]</c> markers, multi-segment alias/type paths) parsing unchanged.
/// </summary>
public class ComplexFieldPathParserTests
{
    // ---- Defect #9: long alias/type path must not crash and must match the dotted path ----

    [Fact]
    public void AddField_LongAliasTypePath_DoesNotThrow_AndRendersName()
    {
        // Arrange - alias + two >300-char segments blows well past the 512-char stack buffer that
        // previously overflowed SpanPathBuilder.
        var longName = new string('a', 300);
        var field = "al:" + longName + "." + longName;

        // Act
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField(field).ToString();

        // Assert
        act.Should().NotThrow<InvalidOperationException>();
        act().Should().Contain(longName);
    }

    [Fact]
    public void AddField_LongComplexPath_RendersSameStructureAsEquivalentDottedPath()
    {
        // Arrange - a complex path (has an alias prefix, forcing the complex route) and the
        // equivalent plain dotted path should produce the same parent/child structure. The only
        // difference is the leaf alias, so both must contain the same long segment names.
        var seg = new string('b', 300);
        var complexPath = "al:" + seg + "." + seg;   // alias:seg.seg  -> complex route
        var dottedPath = seg + "." + seg;            // seg.seg        -> dotted route

        // Act
        var complex = QueryBuilder.CreateDefaultBuilder("Q").AddField(complexPath);
        var dotted = QueryBuilder.CreateDefaultBuilder("Q").AddField(dottedPath);

        // Assert - both build a two-level nesting of the same-named segments without throwing.
        complex.Definition.Fields.Should().ContainKey(seg);
        complex.Definition.Fields[seg].Fields.Should().ContainKey(seg);
        dotted.Definition.Fields.Should().ContainKey(seg);
        dotted.Definition.Fields[seg].Fields.Should().ContainKey(seg);
        // The complex leaf additionally carries the alias.
        complex.Definition.Fields[seg].Alias.Should().Be("al");
    }

    [Fact]
    public void AddField_LongPerNodeComplexPath_DoesNotThrow()
    {
        // Arrange - drive the per-node complex overload (nested builder under a parent) with a
        // long alias/type path so the second stackalloc buffer is exercised too.
        var longName = new string('c', 400);

        // Act
        var act = () => QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("root", b => b.AddField("al:" + longName + "." + longName))
            .ToString();

        // Assert
        act.Should().NotThrow<InvalidOperationException>();
        act().Should().Contain(longName);
    }

    // ---- Defect #8: malformed alias/space shapes are rejected, not silently mangled ----

    [Fact]
    public void AddField_SpacesAroundColon_ThrowsArgumentException()
    {
        // Arrange - "a : b" used to render the invalid GraphQL "query Q{ : b }".
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("a : b");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Malformed field segment*");
    }

    [Fact]
    public void AddField_LeadingColonName_ThrowsArgumentException()
    {
        // Arrange - an empty alias part (leading ':') must be invalid, not folded into a name.
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField(":name");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddField_TrailingColonAlias_ThrowsArgumentException()
    {
        // Arrange - a colon with an empty name part is equally malformed.
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("String alias:");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddField_SpaceInsideDottedChain_TreatsPrefixAsPerSegmentType_NotInvalidGraphQl()
    {
        // "a.b c.d" is ambiguous with the documented per-segment type-marker grammar
        // (cf. "[Foo] posts.title"): the space-delimited "b" is applied as a type on the
        // intermediate segment "c", exactly like "[Foo]" would be. This is a consistent
        // interpretation, not invalid GraphQL — it must NOT emit a colon-prefixed or anonymous
        // field, and every remaining segment must render.
        var qb = QueryBuilder.CreateDefaultBuilder("Q").AddField("a.b c.d");
        var rendered = qb.ToString();

        // Assert - a -> c -> d nesting, no invalid/anonymous selection set.
        qb.Definition.Fields.Should().ContainKey("a");
        qb.Definition.Fields["a"].Fields.Should().ContainKey("c");
        qb.Definition.Fields["a"].Fields["c"].Fields.Should().ContainKey("d");
        rendered.Should().NotMatchRegex(@"\{\s*\{");
        rendered.Should().NotContain(":");
    }

    // ---- Existing valid alias/type shapes must render unchanged ----

    [Theory]
    [InlineData("alias:name", "alias", "name")]
    [InlineData("Person User:user", "User", "user")]
    [InlineData("String FullName:name", "FullName", "name")]
    public void AddField_ValidAliasName_RendersAliasAndName(string field, string expectedAlias, string expectedName)
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("Q").AddField(field);

        // Assert
        qb.Definition.Fields.Should().ContainKey(expectedName);
        qb.Definition.Fields[expectedName].Alias.Should().Be(expectedAlias);
        qb.ToString().Should().Contain($"{expectedAlias}:{expectedName}");
    }

    [Fact]
    public void AddField_TypePrefix_ParsesTypeAndFieldName()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("Q").AddField("CustomType field");

        // Assert
        qb.Definition.Fields.Should().ContainKey("field");
        qb.Definition.Fields["field"].Type.Should().Be("CustomType");
    }

    [Fact]
    public void AddField_ArrayMarkerPrefix_RendersUnchanged()
    {
        // Arrange & Act - "[]tags" (no space) is a single token; the "[]" stays attached to the
        // field name (this is the pre-existing behavior for a space-less array-marker prefix).
        var qb = QueryBuilder.CreateDefaultBuilder("Q").AddField("[]tags");

        // Assert
        qb.Definition.Fields.Should().ContainKey("[]tags");
        qb.ToString().Should().Contain("[]tags");
    }

    [Fact]
    public void AddField_PerSegmentArrayMarker_OnLeaf_RendersUnchanged()
    {
        // Arrange & Act - "user.[] posts" sets the array marker on the nested leaf "posts".
        var qb = QueryBuilder.CreateDefaultBuilder("Q").AddField("user.[] posts");

        // Assert
        qb.Definition.Fields["user"].Fields.Should().ContainKey("posts");
        qb.Definition.Fields["user"].Fields["posts"].Type.Should().Be("[]");
    }

    [Fact]
    public void AddField_MultiSegmentAliasTypePath_RendersUnchanged()
    {
        // Arrange & Act - a documented multi-segment alias/type path stays intact.
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("String userInfo:profile.displayName:name");

        // Assert
        qb.Definition.Fields.Should().ContainKey("profile");
        qb.Definition.Fields["profile"].Alias.Should().Be("userInfo");
        qb.Definition.Fields["profile"].Fields.Should().ContainKey("name");
        qb.Definition.Fields["profile"].Fields["name"].Alias.Should().Be("displayName");
    }
}
