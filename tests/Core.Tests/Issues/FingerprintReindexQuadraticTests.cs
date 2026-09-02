using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class FingerprintReindexQuadraticTests
{
    private static QueryBuilder MixedFilterFragment(string name, int i) =>
        CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId",
                new Dictionary<string, object?> { ["filter"] = $"segment{i / 2}" });

    [Fact]
    public void ReindexFingerprint_ManySingletonBucketsUnderOneName_MergesEachRepeatedFilterCorrectly()
    {
        // Arrange/Act — each distinct filter arrives TWICE in sequence (segment0, segment0,
        // segment1, segment1, ...), so every second Include is a genuine merge that forces
        // FieldMergeIndex.ReindexFingerprint to re-key an entry while MANY other singleton
        // fingerprint buckets already exist under the shared "businessObjects" name — the shape
        // that made the old all-buckets linear scan in ReindexFingerprint O(N) per call.
        const int pairCount = 100;
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        for (var i = 0; i < pairCount * 2; i++)
        {
            root.Include(MixedFilterFragment($"F{i}", i));
        }

        // Assert — one merged root per distinct filter value, none lost or falsely split further.
        root.DefinitionsCount.Should().Be(pairCount);
        var rendered = root.ToString();
        for (var i = 0; i < pairCount; i++)
        {
            rendered.Should().Contain($"segment{i}");
        }
    }

    [Fact]
    public void ReindexFingerprint_AfterReKey_SubsequentLookupsStillFindCorrectBucket()
    {
        // Arrange — force several distinct singleton buckets, then two Includes that both
        // resolve to the SAME post-merge fingerprint (repeated re-keying of one entry), to
        // confirm the reverse key->fingerprint map tracks a moving target correctly across
        // multiple successive re-keys, not just a single one.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        for (var i = 0; i < 20; i++)
        {
            root.Include(MixedFilterFragment($"Solo{i}", i * 2)); // all distinct singleton buckets
        }

        root.Include(MixedFilterFragment("A1", 0));
        root.Include(MixedFilterFragment("A2", 0));
        root.Include(MixedFilterFragment("A3", 0));

        // Assert — "segment0" merged across all three additional Includes into one root, on top
        // of the 20 untouched singleton roots.
        root.DefinitionsCount.Should().Be(20);
        root.ToString().Should().Contain("segment0");
    }
}
