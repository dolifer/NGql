using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FieldDirectiveBranchTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceName_Throws(string? name)
    {
        var act = () => new FieldDirective(name!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive name cannot be null or whitespace.*");
    }

    [Fact]
    public void Constructor_ReadOnlyDictionaryArguments_NormalizesWithoutMutatingSource()
    {
        IReadOnlyDictionary<string, object?> source = new ReadOnlyArguments(
            new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 });

        var directive = new FieldDirective("format", source);

        directive.Arguments.Should().NotBeNull();
        directive.Arguments!.Keys.Should().ContainInOrder("a", "b");
    }

    [Fact]
    public void Constructor_MutableDictionaryArguments_NormalizesInPlaceType()
    {
        var source = new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 };

        var directive = new FieldDirective("format", source);

        directive.Arguments!.Keys.Should().ContainInOrder("a", "b");
    }

    [Fact]
    public void Constructor_EmptyArguments_StoresNull()
    {
        var directive = new FieldDirective("deprecated", new Dictionary<string, object?>());

        directive.Arguments.Should().BeNull();
    }

    [Fact]
    public void IsStructurallyEqualTo_DifferentNames_ReturnsFalse()
    {
        var a = new FieldDirective("include", new Dictionary<string, object?> { ["if"] = new Variable("$x", "Boolean!") });
        var b = new FieldDirective("skip", new Dictionary<string, object?> { ["if"] = new Variable("$x", "Boolean!") });

        a.IsStructurallyEqualTo(b).Should().BeFalse();
    }

    [Fact]
    public void IsStructurallyEqualTo_BothWithoutArguments_ReturnsTrue()
    {
        var a = new FieldDirective("deprecated");
        var b = new FieldDirective("deprecated");

        a.IsStructurallyEqualTo(b).Should().BeTrue();
    }

    [Fact]
    public void IsStructurallyEqualTo_OnlyOtherHasArguments_ReturnsFalse()
    {
        var a = new FieldDirective("format");
        var b = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "ISO8601" });

        a.IsStructurallyEqualTo(b).Should().BeFalse();
    }

    [Fact]
    public void IsStructurallyEqualTo_OnlyThisHasArguments_ReturnsFalse()
    {
        var a = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "ISO8601" });
        var b = new FieldDirective("format");

        a.IsStructurallyEqualTo(b).Should().BeFalse();
    }

    [Fact]
    public void IsStructurallyEqualTo_DifferentArgumentCounts_ReturnsFalse()
    {
        var a = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "ISO8601", ["tz"] = "UTC" });
        var b = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "ISO8601" });

        a.IsStructurallyEqualTo(b).Should().BeFalse();
    }

    [Fact]
    public void IsStructurallyEqualTo_DifferentArgumentKeys_ReturnsFalse()
    {
        var a = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "ISO8601" });
        var b = new FieldDirective("format", new Dictionary<string, object?> { ["tz"] = "ISO8601" });

        a.IsStructurallyEqualTo(b).Should().BeFalse();
    }

    [Fact]
    public void IsStructurallyEqualTo_DifferentArgumentValues_ReturnsFalse()
    {
        var a = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "ISO8601" });
        var b = new FieldDirective("format", new Dictionary<string, object?> { ["as"] = "RFC3339" });

        a.IsStructurallyEqualTo(b).Should().BeFalse();
    }

    [Fact]
    public void IsStructurallyEqualTo_SameNameAndArguments_ReturnsTrue()
    {
        var a = new FieldDirective("include", new Dictionary<string, object?> { ["if"] = new Variable("$x", "Boolean!") });
        var b = new FieldDirective("include", new Dictionary<string, object?> { ["if"] = new Variable("$x", "Boolean!") });

        a.IsStructurallyEqualTo(b).Should().BeTrue();
    }

    private sealed class ReadOnlyArguments(Dictionary<string, object?> inner) : IReadOnlyDictionary<string, object?>
    {
        public object? this[string key] => inner[key];
        public IEnumerable<string> Keys => inner.Keys;
        public IEnumerable<object?> Values => inner.Values;
        public int Count => inner.Count;
        public bool ContainsKey(string key) => inner.ContainsKey(key);
        public bool TryGetValue(string key, out object? value) => inner.TryGetValue(key, out value);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
