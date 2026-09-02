using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression tests for <c>FieldDefinitionExtensions.MergeFieldArguments</c> not bumping
/// <c>FieldDefinition</c>'s global merge-memo epoch. That method replaces a root
/// dictionary/<c>FieldChildren</c> VALUE at an EXISTING key with a new <c>FieldDefinition</c>
/// instance carrying a cleared fingerprint memo — exactly the out-of-band mutation shape
/// <c>FieldMergeIndex</c>'s class remarks name explicitly ("plain AddField replacing a root
/// dictionary VALUE at an existing key") — but without the epoch bump, <c>FieldMergeIndex</c>
/// never learns the old instance's cached fingerprint bucket is stale, and a later
/// <c>MergeByFieldPath</c> Include whose fingerprint matches the LIVE post-mutation shape misses
/// the bucket and falsely splits into a spurious aliased root instead of merging.
/// </summary>
public class MergeFieldArgumentsEpochTests
{
    [Fact]
    public async Task Include_AfterMergeByDefaultLeafWidenedThenMergeByFieldPathIncludes_MergesIntoSingleRoot()
    {
        // Arrange — SHAPE A: a MergeByDefault root gets a leaf-with-args widened via a
        // MergeByDefault Include (RecursiveCreateField's ClearMergeMemo cascade is gated on
        // fieldDefinition._children != null, so a leaf incoming skips it entirely), memoizing a
        // fingerprint on "user" via an interleaved MergeByFieldPath Include, then a further
        // MergeByFieldPath Include matches the post-widen shape and must merge rather than split.
        var root = CreateDefaultBuilder("R", MergingStrategy.MergeByDefault)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1 });

        root.Include(CreateDefaultBuilder("F", MergingStrategy.MergeByFieldPath)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1 }));
        root.Include(CreateDefaultBuilder("G", MergingStrategy.MergeByDefault)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }));

        // Act
        root.Include(CreateDefaultBuilder("H", MergingStrategy.MergeByFieldPath)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }));

        // Assert — exactly one "user" root carrying both arguments; a false split would instead
        // produce two byte-identical "user(a:1, b:2)" roots ("user" and "user_1").
        root.DefinitionsCount.Should().Be(1);
        var rendered = root.ToString();
        rendered.Should().NotContain("user_1");
        await root.Verify();
    }

    [Fact]
    public async Task Include_AfterRootLevelAddFieldWidensExistingArguments_MergesIntoSingleRoot()
    {
        // Arrange — SHAPE B: an all-MergeByFieldPath tree where a plain root-level AddField call
        // widens an existing root field's arguments via FieldFactory.UpdateExistingField's
        // MergeFieldArguments path (GetOrAddSimpleField's non-dotted fast path).
        var root = CreateDefaultBuilder("R", MergingStrategy.MergeByFieldPath)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1 });

        root.Include(CreateDefaultBuilder("F", MergingStrategy.MergeByFieldPath)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1 }));
        root.AddField("user", new Dictionary<string, object?> { ["b"] = 2 });

        // Act
        root.Include(CreateDefaultBuilder("H", MergingStrategy.MergeByFieldPath)
            .AddField("user", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 }));

        // Assert — exactly one "user" root carrying both arguments; a false split would instead
        // produce two byte-identical "user(a:1, b:2)" roots ("user" and "user_1").
        root.DefinitionsCount.Should().Be(1);
        var rendered = root.ToString();
        rendered.Should().NotContain("user_1");
        await root.Verify();
    }
}
