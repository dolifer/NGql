using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Covers the UTF-8 render surface (<c>WriteUtf8</c>) added to <see cref="QueryBuilder"/>,
/// <see cref="Query"/>, and <see cref="Mutation"/>. The load-bearing invariant is that the bytes
/// written into a caller-supplied <see cref="IBufferWriter{Byte}"/> are identical to
/// <c>Encoding.UTF8.GetBytes(query.ToString())</c> across every query shape — including non-ASCII
/// (accents, CJK) and astral / surrogate-pair (emoji) content, which is the whole point of a
/// dedicated UTF-8 path. Also covers append-not-clobber semantics into a non-empty writer and
/// null-argument guards.
/// </summary>
public class WriteUtf8Tests
{
    private static byte[] Utf8Of(string s) => Encoding.UTF8.GetBytes(s);

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
        => QueryBuilder.CreateDefaultBuilder("Vars")
            .AddField("user", new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID!") });

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
    public void WriteUtf8_IntoEmptyWriter_MatchesToStringBytes(string scenario, Func<QueryBuilder> factory)
    {
        // Arrange
        _ = scenario;
        var query = factory();
        var expected = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();

        // Act
        query.WriteUtf8(writer);

        // Assert
        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Theory]
    [MemberData(nameof(AllShapes))]
    public void WriteUtf8_IntoNonEmptyWriter_AppendsWithoutClobbering(string scenario, Func<QueryBuilder> factory)
    {
        // Arrange
        _ = scenario;
        var query = factory();
        var prefix = Utf8Of("PREFIX>>");
        var expected = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();
        writer.Write(prefix); // pre-existing bytes must survive

        // Act
        query.WriteUtf8(writer);

        // Assert
        var combined = new byte[prefix.Length + expected.Length];
        Buffer.BlockCopy(prefix, 0, combined, 0, prefix.Length);
        Buffer.BlockCopy(expected, 0, combined, prefix.Length, expected.Length);
        writer.WrittenSpan.ToArray().Should().Equal(combined);
    }

    [Fact]
    public void WriteUtf8_CalledTwice_AppendsBoth()
    {
        // Guards against the pooled builder or encoder leaking state between calls.
        var query = Simple();
        var once = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();

        query.WriteUtf8(writer);
        query.WriteUtf8(writer);

        var expected = new byte[once.Length * 2];
        Buffer.BlockCopy(once, 0, expected, 0, once.Length);
        Buffer.BlockCopy(once, 0, expected, once.Length, once.Length);
        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    // ----- Non-ASCII: accents, CJK -----

    [Theory]
    [InlineData("Accents", "café", "naïve")]
    [InlineData("Cjk", "ユーザー", "用户")]
    public void WriteUtf8_NonAsciiArgValues_MatchesToStringBytes(string scenario, string first, string second)
    {
        // Arrange
        _ = scenario;
        var query = QueryBuilder.CreateDefaultBuilder("Intl")
            .AddField("users", new Dictionary<string, object?>
            {
                ["name"] = first,
                ["city"] = second,
            });
        var expected = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();

        // Act
        query.WriteUtf8(writer);

        // Assert
        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    // ----- Astral / surrogate-pair (emoji): the surrogate-split-across-chunk guard -----

    [Theory]
    [InlineData("Emoji", "😀")]
    [InlineData("MathAstral", "𝓊")]
    [InlineData("EmojiZwjSequence", "👨‍👩‍👧")]
    public void WriteUtf8_AstralCodepointArgValue_MatchesToStringBytes(string scenario, string astral)
    {
        // Arrange
        _ = scenario;
        var query = QueryBuilder.CreateDefaultBuilder("Astral")
            .AddField("users", new Dictionary<string, object?> { ["label"] = astral });
        var expected = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();

        // Act
        query.WriteUtf8(writer);

        // Assert
        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void WriteUtf8_LongQueryWithEmojiDeepInside_MatchesToStringBytes()
    {
        // Force a render long enough that its StringBuilder spans multiple internal chunks, with an
        // emoji placed deep in the middle so its surrogate pair is likely to straddle a chunk
        // boundary. The stateful encoder must carry the pending high surrogate across the boundary.
        // A per-chunk stateless transcode would instead emit two replacement chars and fail parity.
        var builder = QueryBuilder.CreateDefaultBuilder("LongEmoji");
        for (var i = 0; i < 400; i++)
        {
            builder.AddField($"field{i}.nested.value{i}");
        }
        builder.AddField("marker", new Dictionary<string, object?> { ["label"] = "prefix😀middle𝓊suffix" });
        for (var i = 400; i < 800; i++)
        {
            builder.AddField($"tail{i}.leaf{i}");
        }

        var expected = Utf8Of(builder.ToString());
        var writer = new ArrayBufferWriter<byte>();

        builder.WriteUtf8(writer);

        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void WriteUtf8_NullBufferWriter_Throws()
    {
        var query = Simple();

        var act = () => query.WriteUtf8(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- Classic Query parity -----

    private static Query ClassicQuery()
        => new Query("GetUser", new Variable("$id", "ID!"))
            .Where("id", "$id")
            .Select("name", "email")
            .Select(new Query("address").Select("city", "zip"));

    [Fact]
    public void Query_WriteUtf8_MatchesToStringBytes()
    {
        var query = ClassicQuery();
        var expected = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();

        query.WriteUtf8(writer);

        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void Query_WriteUtf8_WithNonAsciiArg_MatchesToStringBytes()
    {
        var query = new Query("Search").Where("term", "café用户😀").Select("id");
        var expected = Utf8Of(query.ToString());
        var writer = new ArrayBufferWriter<byte>();

        query.WriteUtf8(writer);

        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void Query_WriteUtf8_NullBufferWriter_Throws()
    {
        var query = ClassicQuery();

        var act = () => query.WriteUtf8(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----- Classic Mutation parity -----

    private static Mutation ClassicMutation()
        => new Mutation("CreateUser", new Variable("$name", "String!"))
            .Select(new Query("createUser").Where("name", "$name").Select("id"));

    [Fact]
    public void Mutation_WriteUtf8_MatchesToStringBytes()
    {
        var mutation = ClassicMutation();
        var expected = Utf8Of(mutation.ToString());
        var writer = new ArrayBufferWriter<byte>();

        mutation.WriteUtf8(writer);

        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void Mutation_WriteUtf8_NullBufferWriter_Throws()
    {
        var mutation = ClassicMutation();

        var act = () => mutation.WriteUtf8(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
