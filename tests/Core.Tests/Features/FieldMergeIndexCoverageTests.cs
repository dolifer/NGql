using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Features;

/// <summary>
/// Drives <see cref="FieldMergeIndex"/> directly to reach its self-healing and re-bucketing arms —
/// the paths that exist so a fingerprint drifting after indexing can never cause a false split.
/// </summary>
public class FieldMergeIndexCoverageTests
{
    private static FieldDefinition Field(string name, int? first = null)
    {
        var arguments = first is null
            ? null
            : new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase) { ["first"] = first };
        return new FieldDefinition(name, "Object", null, arguments);
    }

    [Fact]
    public void GetMergeCandidatesByFingerprint_WhenFingerprintNeverIndexed_ReturnsNull()
    {
        var index = new FieldMergeIndex();
        var user = Field("user");
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = user };
        index.IndexAdd("user", user);

        var result = index.GetMergeCandidatesByFingerprint(fields, "user", 0xDEADBEEFUL);

        result.Should().BeNull();
    }

    [Fact]
    public void GetMergeCandidatesByFingerprint_WhenBucketedFieldIsReplacedByAnUnrelatedOne_HealsBucketAndReturnsNull()
    {
        var index = new FieldMergeIndex();
        var original = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = original };
        index.IndexAdd("user", original);

        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(original);
        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().ContainSingle();

        // Same key, same Name, same count — but a different argument shape, so the indexed entry now
        // sits under a fingerprint it no longer matches.
        fields["user"] = Field("user", 25);

        var result = index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        result.Should().BeNull();
    }

    [Fact]
    public void GetMergeCandidatesByFingerprint_AfterHealing_FindsFieldUnderItsNewFingerprint()
    {
        var index = new FieldMergeIndex();
        var original = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = original };
        index.IndexAdd("user", original);

        var oldFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(original);
        index.GetMergeCandidatesByFingerprint(fields, "user", oldFingerprint);

        var replacement = Field("user", 25);
        fields["user"] = replacement;
        index.GetMergeCandidatesByFingerprint(fields, "user", oldFingerprint);

        var result = index.GetMergeCandidatesByFingerprint(
            fields, "user", FieldDefinitionExtensions.ComputeDeepFingerprint(replacement));

        result.Should().ContainSingle().Which.Should().Be("user");
    }

    [Fact]
    public void GetMergeCandidatesByFingerprint_WhenIndexedKeyIsGoneFromTheFieldMap_DropsItFromTheBucket()
    {
        var index = new FieldMergeIndex();
        var first = Field("user", 10);
        var second = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = first, ["user_1"] = second };
        index.IndexAdd("user", first);
        index.IndexAdd("user_1", second);

        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(first);
        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().HaveCount(2);

        // Removing one key AND adding another keeps Count stable, so EnsureSynced's count check
        // cannot see the swap — the bucket walk has to drop the vanished key itself.
        fields.Remove("user_1");
        fields["other"] = Field("other");

        var result = index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        result.Should().ContainSingle().Which.Should().Be("user");
    }

    [Fact]
    public void ReindexFingerprint_WhenNameWasNeverBucketed_LeavesIndexUsable()
    {
        var index = new FieldMergeIndex();
        var user = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = user };
        index.IndexAdd("user", user);

        index.ReindexFingerprint(fields, "user", "user");

        var result = index.GetMergeCandidatesByFingerprint(
            fields, "user", FieldDefinitionExtensions.ComputeDeepFingerprint(user));

        result.Should().ContainSingle().Which.Should().Be("user");
    }

    [Fact]
    public void ReindexFingerprint_WhenKeyIsNotInTheFieldMap_IsANoOp()
    {
        var index = new FieldMergeIndex();
        var user = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = user };
        index.IndexAdd("user", user);
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(user);
        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        index.ReindexFingerprint(fields, "user", "missing");

        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint)
            .Should().ContainSingle().Which.Should().Be("user");
    }

    [Fact]
    public void ReindexFingerprint_WhenFingerprintIsUnchanged_KeepsTheKeyInItsBucketExactlyOnce()
    {
        var index = new FieldMergeIndex();
        var user = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = user };
        index.IndexAdd("user", user);
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(user);
        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        index.ReindexFingerprint(fields, "user", "user");

        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint)
            .Should().ContainSingle().Which.Should().Be("user");
    }

    [Fact]
    public void ReindexFingerprint_WhenTheFieldMutatedInPlace_MovesItToItsNewBucket()
    {
        var index = new FieldMergeIndex();
        var user = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = user };
        index.IndexAdd("user", user);
        var oldFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(user);
        index.GetMergeCandidatesByFingerprint(fields, "user", oldFingerprint);

        // Mutate exactly the way MergeFieldsInPlace does — arguments change and the field's own
        // memos are dropped — then tell the index, which is the contract QueryMerger follows.
        user._arguments!["first"] = 25;
        user._deepArgumentFingerprint = null;
        user._subtreeHasAnyArguments = null;
        index.ReindexFingerprint(fields, "user", "user");

        index.GetMergeCandidatesByFingerprint(fields, "user", oldFingerprint).Should().BeNull();
        index.GetMergeCandidatesByFingerprint(fields, "user", FieldDefinitionExtensions.ComputeDeepFingerprint(user))
            .Should().ContainSingle().Which.Should().Be("user");
    }

    [Fact]
    public void IndexAdd_AfterBucketsWereBuilt_PlacesTheNewKeyInItsFingerprintBucket()
    {
        var index = new FieldMergeIndex();
        var first = Field("user", 10);
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = first };
        index.IndexAdd("user", first);
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(first);
        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        var second = Field("user", 10);
        fields["user_1"] = second;
        index.IndexAdd("user_1", second);

        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint)
            .Should().BeEquivalentTo("user", "user_1");
    }

    [Fact]
    public void NextUniqueKey_WhenBaseKeyIsFree_ReturnsItUnchanged()
    {
        var index = new FieldMergeIndex();
        var user = Field("user");
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = user };
        index.IndexAdd("user", user);

        index.NextUniqueKey(fields, "post").Should().Be("post");
    }

    [Fact]
    public void NextUniqueKey_WhenSuffixedKeysAlreadyExist_SkipsPastThem()
    {
        var index = new FieldMergeIndex();
        var fields = new Dictionary<string, FieldDefinition>();
        foreach (var key in new[] { "user", "user_1", "user_2" })
        {
            var field = Field("user");
            fields[key] = field;
            index.IndexAdd(key, field);
        }

        index.NextUniqueKey(fields, "user").Should().Be("user_3");
    }
}
