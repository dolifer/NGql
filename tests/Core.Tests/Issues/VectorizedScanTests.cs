using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Path classification, identifier validation and string-escape detection moved from per-character
/// loops to vectorized <c>IndexOfAny</c> searches. Each test uses the former loop as the oracle.
/// </summary>
public class VectorizedScanTests
{
    private const string Alphabet = "ab_Z9 .:\t-é";

    [Fact]
    public void Classification_RandomStrings_MatchesCharacterLoop()
    {
        var random = new Random(20260924);
        for (var i = 0; i < 5_000; i++)
        {
            var text = RandomString(random, 12);
            bool hasSpace = false, hasDot = false, hasColon = false;
            foreach (var c in text)
            {
                hasSpace |= c == ' ';
                hasDot |= c == '.';
                hasColon |= c == ':';
            }

            text.AsSpan().IsSimpleField().Should().Be(!hasSpace && !hasDot && !hasColon, text);
            text.AsSpan().IsDottedField().Should().Be(hasDot && !hasSpace && !hasColon, text);
        }
    }

    [Fact]
    public void AddField_InvalidCharacter_ReportsFirstInvalidPosition()
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("user", b => b.AddField("ab-c$d"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Invalid GraphQL field name 'ab-c$d': contains invalid character '-' at position 2.");
    }

    [Fact]
    public void AddField_EveryAsciiCharacterAfterALetter_AcceptsOnlyGraphQlNameCharacters()
    {
        for (var c = (char)0x21; c < 0x7F; c++)
        {
            if (c is '.' or ':') continue; // path and alias separators, not name characters
            var name = "a" + c;
            var expectedValid = c == '_' || char.IsAsciiLetterOrDigit(c);

            var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("user", b => b.AddField(name));

            if (expectedValid) act.Should().NotThrow(name);
            else act.Should().Throw<ArgumentException>(name);
        }
    }

    [Fact]
    public void Render_StringArgument_EscapesExactlyQuoteBackslashAndControlCharacters()
    {
        for (var c = (char)0; c <= 0xFF; c++)
        {
            var value = "x" + c + "y";
            var rendered = QueryBuilder.CreateDefaultBuilder("Q")
                .AddField("user", new Dictionary<string, object?> { ["s"] = value })
                .ToString();

            var expectEscape = c < 0x20 || c == '"' || c == '\\';
            rendered.Contains("\"x" + c + "y\"", StringComparison.Ordinal).Should().Be(!expectEscape, $"U+{(int)c:X4}");
        }
    }

    private static string RandomString(Random random, int maxLength)
    {
        var chars = new char[random.Next(maxLength + 1)];
        for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[random.Next(Alphabet.Length)];
        return new string(chars);
    }
}
