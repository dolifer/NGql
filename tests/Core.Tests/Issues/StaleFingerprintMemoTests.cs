using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class StaleFingerprintMemoTests
{
    private static QueryBuilder Fragment(string name, string path, string filter) =>
        CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
            .AddField(path, new Dictionary<string, object?> { ["filter"] = filter });

    [Fact]
    public async Task Include_AfterOutOfBandArgumentMutation_MergesGenuineCandidate()
    {
        // Arrange — memoize a deep fingerprint on the root "user" field via a divergent Include,
        // then mutate "user.id"'s arguments out-of-band through the non-in-place
        // FieldDefinitionExtensions.MergeFieldArguments path (plain AddField), which must clear
        // that memo. A subsequent Include with the POST-mutation argument shape is a genuine
        // merge candidate and must land in the same root, not get falsely split into "user_2".
        var root = CreateDefaultBuilder("R", MergingStrategy.MergeByFieldPath);
        root.Include(Fragment("F0", "user.id", "A"));
        root.Include(Fragment("Fx", "user.id", "ZZZ")); // forces the fingerprint memo onto root "user"
        root.AddField("user.id", new Dictionary<string, object?> { ["filter"] = "B" }); // memo now stale if uncleared

        // Act
        root.Include(Fragment("F2", "user.id", "B")); // genuine match against the post-mutation shape

        // Assert — F0(A)/Fx(ZZZ) start as 2 separate roots ("user", "user_1"); the out-of-band
        // AddField overwrites "user"'s filter from A to B, so F2(B) merges into "user" and the
        // total stays 2. A stale memo would falsely split F2 into a third "user_2" root.
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().NotContain("user_2");
        await root.Verify();
    }

    private static QueryBuilder DeepFragment(string name, string filter) =>
        CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
            .AddField("bo.entity.node.leaf", new Dictionary<string, object?> { ["filter"] = filter });

    [Fact]
    public void Include_AfterDeepOutOfBandMutation_ThreeLevelsDown_MergesPostMutationShape()
    {
        // Arrange — a SECOND divergent Include is required to actually force FindMergeTarget to
        // memoize a fingerprint on the ancestor chain ("bo" and "entity"): with only one prior
        // field of a given name there are no merge candidates to compare against, so no
        // fingerprint is ever computed and the bug this test targets could not manifest. After
        // the memo exists, mutate an argument THREE levels below "bo" ("bo.entity.node.leaf")
        // out-of-band. The ancestor chain must lose its memoized fingerprints too, not just
        // "leaf" itself, or FindMergeTarget still compares against the pre-mutation subtree shape.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        root.Include(DeepFragment("F0", "A"));
        root.Include(DeepFragment("Fx", "ZZZ")); // forces the fingerprint memo onto the "bo" ancestor chain

        // Out-of-band mutation of the same deep leaf, three levels below "bo".
        root.AddField("bo.entity.node.leaf", new Dictionary<string, object?> { ["filter"] = "B" });

        // Act — a fragment matching the POST-mutation shape must merge.
        root.Include(DeepFragment("PostMatch", "B"));

        // Assert — F0(A)/Fx(ZZZ) start as 2 separate roots ("bo", "bo_1"); the out-of-band
        // AddField overwrites "bo"'s leaf filter from A to B, so PostMatch(B) merges into "bo"
        // and the total stays 2. A stale ancestor memo would falsely split PostMatch into a
        // third "bo_2" root.
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().NotContain("bo_2");
    }

    [Fact]
    public void Include_AfterDeepOutOfBandMutation_PreMutationShapeStaysSeparate()
    {
        // Arrange — inverse of the previous test: after the same deep out-of-band mutation, a
        // fragment matching the PRE-mutation shape must NOT merge (the live field genuinely no
        // longer carries that argument value).
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        root.Include(DeepFragment("F0", "A"));
        root.Include(DeepFragment("Fx", "ZZZ"));

        root.AddField("bo.entity.node.leaf", new Dictionary<string, object?> { ["filter"] = "B" });

        // Act — a fragment matching the PRE-mutation ("A") shape must stay separate: "bo" itself
        // no longer holds that value.
        root.Include(DeepFragment("PreMatch", "A"));

        // Assert
        root.DefinitionsCount.Should().Be(3);
        var rendered = root.ToString();
        rendered.Should().Contain("bo_2");
    }

    [Fact]
    public void Include_AfterNestedFieldBuilderActionMutation_MergesGenuineCandidate()
    {
        // Arrange — same ancestor-chain hazard, but the out-of-band mutation happens via the
        // Action<FieldBuilder> nested-builder path (FieldBuilder.Where inside a nested AddField
        // action) rather than a dotted-path string. The parent FieldBuilder's ancestor field must
        // still lose its memoized fingerprint once the nested action completes.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        QueryBuilder NestedFragment(string name, string filter) =>
            CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
                .AddField("bo", b => b.AddField("leaf", new Dictionary<string, object?> { ["filter"] = filter }));

        root.Include(NestedFragment("F0", "A"));
        root.Include(NestedFragment("Fx", "ZZZ")); // forces the fingerprint memo onto the "bo" ancestor

        root.AddField("bo", b => b.AddField("leaf", la => la.Where("filter", "B")));

        // Act
        root.Include(NestedFragment("PostMatch", "B"));

        // Assert — same reasoning as the dotted-path variant above: 2 prior roots, the mutation
        // moves the first onto the PostMatch shape, so the total stays 2 rather than a false 3.
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().NotContain("bo_2");
    }
}
