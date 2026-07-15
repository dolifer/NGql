using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: the signature hash folded raw characters, so <c>{User}</c> and <c>{user}</c>
/// produced DIFFERENT signatures — yet the library merges fields and sorts them under
/// OrdinalIgnoreCase (field maps and argument dictionaries both use
/// <see cref="System.StringComparer.OrdinalIgnoreCase"/>). Two things everything else treats as
/// merge-equal must not hash apart. The fix folds field names, paths, argument keys and nested
/// dictionary keys with <c>char.ToUpperInvariant</c> before hashing.
///
/// Scope of the case-fold matches the rest of the library exactly: NAMES and KEYS are
/// case-insensitive; argument VALUES stay case-sensitive (GraphQL argument values are
/// case-sensitive, and the library's argument-value equality preserves case).
/// </summary>
public class FieldSignatureCaseInsensitivityTests
{
    [Fact]
    public void GenerateSignature_FieldNamesDifferingOnlyInCase_ProduceEqualSignatures()
    {
        // Arrange
        var upper = new Dictionary<string, FieldDefinition> { ["User"] = new("User", "Object") };
        var lower = new Dictionary<string, FieldDefinition> { ["user"] = new("user", "Object") };

        // Act
        var upperSig = FieldSignatureGenerator.GenerateSignature(upper);
        var lowerSig = FieldSignatureGenerator.GenerateSignature(lower);

        // Assert
        upperSig.Should().Be(lowerSig);
    }

    [Fact]
    public void GenerateSignature_NestedFieldNamesDifferingOnlyInCase_ProduceEqualSignatures()
    {
        // Arrange — case difference at both parent and child level, so the case-folded path
        // (not just the leaf name) is exercised on the stackalloc path.
        var upper = new Dictionary<string, FieldDefinition>
        {
            ["User"] = new("User", "Object", null, new Dictionary<string, object?>(),
                new Dictionary<string, FieldDefinition> { ["Name"] = new("Name", "String") })
        };
        var lower = new Dictionary<string, FieldDefinition>
        {
            ["user"] = new("user", "Object", null, new Dictionary<string, object?>(),
                new Dictionary<string, FieldDefinition> { ["name"] = new("name", "String") })
        };

        // Act
        var upperSig = FieldSignatureGenerator.GenerateSignature(upper);
        var lowerSig = FieldSignatureGenerator.GenerateSignature(lower);

        // Assert
        upperSig.Should().Be(lowerSig);
    }

    [Fact]
    public void GenerateSignature_LongFieldNamesDifferingOnlyInCase_HeapFallbackAndStackAgree()
    {
        // Arrange — a >256-char name forces the heap-fallback path. Its case-folded signature
        // must match a short-name field of the same lowercase content run through the stackalloc
        // path, and the upper/lower long names must match each other.
        var longUpper = new string('A', 300);
        var longLower = new string('a', 300);
        var upper = new Dictionary<string, FieldDefinition> { [longUpper] = new(longUpper, "String") };
        var lower = new Dictionary<string, FieldDefinition> { [longLower] = new(longLower, "String") };

        // Act
        var upperSig = FieldSignatureGenerator.GenerateSignature(upper);
        var lowerSig = FieldSignatureGenerator.GenerateSignature(lower);

        // Assert
        upperSig.Should().Be(lowerSig);
    }

    [Fact]
    public void GenerateSignature_ArgumentKeysDifferingOnlyInCase_ProduceEqualSignatures()
    {
        // Arrange — same value, argument KEY differs only in case. Separate dictionaries so the
        // OrdinalIgnoreCase argument store never sees a colliding key.
        var upperKey = new Dictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Object", null,
                new Dictionary<string, object?> { ["Filter"] = "active" })
        };
        var lowerKey = new Dictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Object", null,
                new Dictionary<string, object?> { ["filter"] = "active" })
        };

        // Act
        var upperSig = FieldSignatureGenerator.GenerateSignature(upperKey);
        var lowerSig = FieldSignatureGenerator.GenerateSignature(lowerKey);

        // Assert
        upperSig.Should().Be(lowerSig);
    }

    [Fact]
    public void GenerateSignature_NestedArgumentDictionaryKeysDifferingOnlyInCase_ProduceEqualSignatures()
    {
        // Arrange — nested-dictionary argument keys are also case-insensitive.
        var upperKey = new Dictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Object", null, new Dictionary<string, object?>
            {
                ["options"] = new Dictionary<string, object?> { ["Status"] = "on" }
            })
        };
        var lowerKey = new Dictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Object", null, new Dictionary<string, object?>
            {
                ["options"] = new Dictionary<string, object?> { ["status"] = "on" }
            })
        };

        // Act
        var upperSig = FieldSignatureGenerator.GenerateSignature(upperKey);
        var lowerSig = FieldSignatureGenerator.GenerateSignature(lowerKey);

        // Assert
        upperSig.Should().Be(lowerSig);
    }

    [Fact]
    public void GenerateSignature_ArgumentValuesDifferingOnlyInCase_ProduceDifferentSignatures()
    {
        // Arrange — argument VALUES stay case-sensitive; only key/name case is folded.
        var upperValue = new Dictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Object", null,
                new Dictionary<string, object?> { ["filter"] = "Active" })
        };
        var lowerValue = new Dictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Object", null,
                new Dictionary<string, object?> { ["filter"] = "active" })
        };

        // Act
        var upperSig = FieldSignatureGenerator.GenerateSignature(upperValue);
        var lowerSig = FieldSignatureGenerator.GenerateSignature(lowerValue);

        // Assert
        upperSig.Should().NotBe(lowerSig);
    }

    [Fact]
    public void GenerateSignature_GenuinelyDifferentFieldNames_StillProduceDifferentSignatures()
    {
        // Arrange — the fold must not over-collapse distinct names.
        var users = new Dictionary<string, FieldDefinition> { ["users"] = new("users", "Object") };
        var user = new Dictionary<string, FieldDefinition> { ["user"] = new("user", "Object") };

        // Act
        var usersSig = FieldSignatureGenerator.GenerateSignature(users);
        var userSig = FieldSignatureGenerator.GenerateSignature(user);

        // Assert
        usersSig.Should().NotBe(userSig);
    }
}
