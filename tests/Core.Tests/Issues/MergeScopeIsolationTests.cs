using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class MergeScopeIsolationTests
{
    [Fact]
    public void ConcurrentObservers_AllReceiveInvalidation()
    {
        var field = new FieldDefinition("user");
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = field };
        var indexes = new FieldMergeIndex[20];
        var buckets = new List<string>?[20];
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(field);
        Parallel.For(0, indexes.Length, i =>
        {
            indexes[i] = new FieldMergeIndex();
            buckets[i] = indexes[i].GetMergeCandidatesByFingerprint(fields, "user", fingerprint);
        });

        field.ClearMergeMemo();

        for (var i = 0; i < indexes.Length; i++)
            indexes[i].GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().NotBeSameAs(buckets[i]);
    }

    [Fact]
    public void UnrelatedMutation_PreservesExistingFingerprintBucket()
    {
        var field = new FieldDefinition("user");
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = field };
        var index = new FieldMergeIndex();
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(field);
        var bucket = index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        new FieldDefinition("other").ClearMergeMemo();

        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().BeSameAs(bucket);
    }

    [Fact]
    public void SharedRoot_NotifiesEveryIndexAndRecordCopyKeepsTracking()
    {
        var field = new FieldDefinition("user");
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = field };
        var first = new FieldMergeIndex();
        var second = new FieldMergeIndex();
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(field);
        var firstBucket = first.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);
        var secondBucket = second.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        fields["user"] = field with { };
        fields["user"].ClearMergeMemo();

        first.GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().NotBeSameAs(firstBucket);
        second.GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().NotBeSameAs(secondBucket);
    }

    [Fact]
    public void DeepClone_DoesNotInvalidateSourceIndex()
    {
        var field = new FieldDefinition("user");
        var fields = new Dictionary<string, FieldDefinition> { ["user"] = field };
        var index = new FieldMergeIndex();
        var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(field);
        var bucket = index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint);

        field.DeepClone().ClearMergeMemo();

        index.GetMergeCandidatesByFingerprint(fields, "user", fingerprint).Should().BeSameAs(bucket);
    }
}
