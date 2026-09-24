using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Merging arguments copies the stored dictionary with SortedDictionary's linear copy constructor
/// when it already uses the case-insensitive comparer. Stored arguments from the public constructor
/// may use another comparer; those keep the overwrite loop and its last-wins semantics.
/// </summary>
public class ArgumentCopyTests
{
    [Fact]
    public void MergeFieldArguments_CaseInsensitiveStore_KeepsExistingAndAppliesOverrides()
    {
        var stored = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["b"] = 1, ["a"] = 2 };
        var field = new FieldDefinition("f", "String", null, stored);

        var merged = field.MergeFieldArguments(new Dictionary<string, object?> { ["A"] = 3, ["c"] = 4 });

        merged.Arguments.Select(kv => $"{kv.Key}:{kv.Value}").Should().Equal("a:3", "b:1", "c:4");
        stored.Should().HaveCount(2, "the merge copies instead of mutating the source dictionary");
    }

    [Fact]
    public void MergeFieldArguments_OrdinalStore_ResortsCaseInsensitivelyWithLastWins()
    {
        var stored = new SortedDictionary<string, object?>(StringComparer.Ordinal) { ["B"] = 1, ["a"] = 2, ["b"] = 3 };
        var field = new FieldDefinition("f", "String", null, stored);

        var merged = field.MergeFieldArguments(new Dictionary<string, object?> { ["c"] = 4 });

        merged.Arguments.Select(kv => $"{kv.Key}:{kv.Value}").Should().Equal("a:2", "B:3", "c:4");
    }
}
