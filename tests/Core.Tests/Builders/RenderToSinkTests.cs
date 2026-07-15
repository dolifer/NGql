using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Covers the render-to-sink surface (<c>AppendTo</c> / <c>WriteTo</c>) added to
/// <see cref="QueryBuilder"/>, <see cref="Query"/>, and <see cref="Mutation"/>. The load-bearing
/// invariant is byte-for-byte parity with <c>ToString()</c> across every query shape, plus append
/// (not clobber) semantics into a non-empty sink and null-argument guards.
/// </summary>
public class RenderToSinkTests
{
    private static QueryBuilder Simple()
        => QueryBuilder.CreateDefaultBuilder("Simple")
            .AddField("user.name")
            .AddField("user.email");

    private static QueryBuilder Nested()
        => QueryBuilder.CreateDefaultBuilder("Nested")
            .AddField("user.profile.settings.privacy.notifications")
            .AddField("user.posts.title");

    private static QueryBuilder WithScalarArgs()
        => QueryBuilder.CreateDefaultBuilder("Args")
            .AddField("users", new Dictionary<string, object?> { ["limit"] = 10, ["role"] = "admin" });

    private static QueryBuilder WithDictionaryArgs()
        => QueryBuilder.CreateDefaultBuilder("DictArgs")
            .AddField("users", new Dictionary<string, object?>
            {
                ["filter"] = new Dictionary<string, object?> { ["status"] = "active", ["tier"] = "gold" },
                ["limit"] = 25,
            });

    private static QueryBuilder WithFragments()
        => QueryBuilder.CreateDefaultBuilder("Frag")
            .AddField("hero", b =>
            {
                b.AddField("name");
                b.SpreadFragment("HeroFields");
            })
            .AddFragment("HeroFields", "Character", f =>
            {
                f.AddField("id");
                f.AddField("appearsIn");
            });

    private static QueryBuilder MutationBuilder()
        => QueryBuilder.CreateMutationBuilder("CreateUser")
            .AddField("createUser", new Dictionary<string, object?> { ["name"] = "alice" });

    private static QueryBuilder WithVariables()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Vars")
            .AddField("user", new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID!") });
        return builder;
    }

    public static IEnumerable<object[]> AllShapes()
    {
        yield return new object[] { "Simple", (Func<QueryBuilder>)Simple };
        yield return new object[] { "Nested", (Func<QueryBuilder>)Nested };
        yield return new object[] { "ScalarArgs", (Func<QueryBuilder>)WithScalarArgs };
        yield return new object[] { "DictionaryArgs", (Func<QueryBuilder>)WithDictionaryArgs };
        yield return new object[] { "Fragments", (Func<QueryBuilder>)WithFragments };
        yield return new object[] { "Mutation", (Func<QueryBuilder>)MutationBuilder };
        yield return new object[] { "Variables", (Func<QueryBuilder>)WithVariables };
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void AppendTo_IntoEmptyBuilder_MatchesToString(string scenario, Func<QueryBuilder> factory)
    {
        // Arrange
        _ = scenario;
        var query = factory();
        var expected = query.ToString();
        var sb = new StringBuilder();

        // Act
        query.AppendTo(sb);

        // Assert
        sb.ToString().Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void AppendTo_IntoNonEmptyBuilder_PreservesExistingContent(string scenario, Func<QueryBuilder> factory)
    {
        // Arrange
        _ = scenario;
        var query = factory();
        var expected = query.ToString();
        const string prefix = "PREFIX>>";
        var sb = new StringBuilder(prefix);

        // Act
        query.AppendTo(sb);

        // Assert
        sb.ToString().Should().Be(prefix + expected);
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void WriteTo_IntoStringWriter_MatchesToString(string scenario, Func<QueryBuilder> factory)
    {
        // Arrange
        _ = scenario;
        var query = factory();
        var expected = query.ToString();
        using var writer = new StringWriter();

        // Act
        query.WriteTo(writer);

        // Assert
        writer.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendTo_NullBuilder_Throws()
    {
        var query = Simple();

        var act = () => query.AppendTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WriteTo_NullWriter_Throws()
    {
        var query = Simple();

        var act = () => query.WriteTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AppendTo_CalledTwice_AppendsBoth()
    {
        // Guards against the pooled builder leaking state between calls.
        var query = Simple();
        var rendered = query.ToString();
        var sb = new StringBuilder();

        query.AppendTo(sb);
        query.AppendTo(sb);

        sb.ToString().Should().Be(rendered + rendered);
    }

    // ----- Classic Query parity -----

    private static Query ClassicQuery()
        => new Query("GetUser", new Variable("$id", "ID!"))
            .Where("id", "$id")
            .Select("name", "email")
            .Select(new Query("address").Select("city", "zip"));

    [Fact]
    public void Query_AppendTo_MatchesToString()
    {
        var query = ClassicQuery();
        var expected = query.ToString();
        var sb = new StringBuilder("head|");

        query.AppendTo(sb);

        sb.ToString().Should().Be("head|" + expected);
    }

    [Fact]
    public void Query_WriteTo_MatchesToString()
    {
        var query = ClassicQuery();
        var expected = query.ToString();
        using var writer = new StringWriter();

        query.WriteTo(writer);

        writer.ToString().Should().Be(expected);
    }

    [Fact]
    public void Query_AppendTo_NullBuilder_Throws()
    {
        var query = ClassicQuery();

        var act = () => query.AppendTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Query_WriteTo_NullWriter_Throws()
    {
        var query = ClassicQuery();

        var act = () => query.WriteTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- Classic Mutation parity -----

    private static Mutation ClassicMutation()
        => new Mutation("CreateUser", new Variable("$name", "String!"))
            .Select(new Query("createUser").Where("name", "$name").Select("id"));

    [Fact]
    public void Mutation_AppendTo_MatchesToString()
    {
        var mutation = ClassicMutation();
        var expected = mutation.ToString();
        var sb = new StringBuilder("head|");

        mutation.AppendTo(sb);

        sb.ToString().Should().Be("head|" + expected);
    }

    [Fact]
    public void Mutation_WriteTo_MatchesToString()
    {
        var mutation = ClassicMutation();
        var expected = mutation.ToString();
        using var writer = new StringWriter();

        mutation.WriteTo(writer);

        writer.ToString().Should().Be(expected);
    }

    [Fact]
    public void Mutation_AppendTo_NullBuilder_Throws()
    {
        var mutation = ClassicMutation();

        var act = () => mutation.AppendTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Mutation_WriteTo_NullWriter_Throws()
    {
        var mutation = ClassicMutation();

        var act = () => mutation.WriteTo(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
