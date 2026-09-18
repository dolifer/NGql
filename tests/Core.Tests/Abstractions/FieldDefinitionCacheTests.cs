using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FieldDefinitionCacheTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(1UL)]
    [InlineData(ulong.MaxValue)]
    public void FingerprintCache_AllValuesSurviveRecordCopyAndCanBeCleared(ulong fingerprint)
    {
        var source = new FieldDefinition("user", "[User]?")
        {
            _deepArgumentFingerprint = fingerprint,
            _subtreeHasAnyArguments = false,
            IsNeverMerge = true
        };
        source.IsArray.Should().BeTrue();
        source.IsNullable.Should().BeTrue();

        var copy = source with { Alias = "other" };
        FieldDefinitionExtensions.ComputeDeepFingerprint(copy).Should().Be(fingerprint);
        copy.ClearMergeMemo();

        copy._deepArgumentFingerprint.Should().BeNull();
        copy._subtreeHasAnyArguments.Should().BeNull();
        copy.IsArray.Should().BeTrue();
        copy.IsNullable.Should().BeTrue();
        copy.IsNeverMerge.Should().BeTrue();
        source._deepArgumentFingerprint.Should().Be(fingerprint);
        source._subtreeHasAnyArguments.Should().BeFalse();
    }

    [Fact]
    public void MergeFieldArguments_CopiedFieldRecomputesCachesWithoutChangingSource()
    {
        var source = WithArgument(1);
        var sourceFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(source);

        var changed = source.MergeFieldArguments(new Dictionary<string, object?> { ["id"] = 2 });

        changed._deepArgumentFingerprint.Should().BeNull();
        changed._subtreeHasAnyArguments.Should().BeNull();
        FieldDefinitionExtensions.ComputeDeepFingerprint(changed).Should()
            .Be(FieldDefinitionExtensions.ComputeDeepFingerprint(WithArgument(2)));
        source._deepArgumentFingerprint.Should().Be(sourceFingerprint);
        source.Arguments["id"].Should().Be(1);
    }

    [Fact]
    public void DeepClone_WarmedFieldRetainsIdentityAndRecomputesIndependentCaches()
    {
        var source = WithArgument(1);
        source.IsNeverMerge = true;
        var sourceFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(source);
        source.IsArray.Should().BeTrue();
        source.IsNullable.Should().BeTrue();

        var clone = source.DeepClone();

        clone._deepArgumentFingerprint.Should().BeNull();
        clone._subtreeHasAnyArguments.Should().BeNull();
        clone.Should().Be(source);
        clone.GetHashCode().Should().Be(source.GetHashCode());
        clone.IsNeverMerge.Should().BeTrue();
        clone.IsArray.Should().BeTrue();
        clone.IsNullable.Should().BeTrue();
        FieldDefinitionExtensions.ComputeDeepFingerprint(clone).Should().Be(sourceFingerprint);
        clone.ClearMergeMemo();
        source._deepArgumentFingerprint.Should().Be(sourceFingerprint);
    }

    [Theory]
    [InlineData("User", false, false)]
    [InlineData("[User]", true, false)]
    [InlineData("User?", false, true)]
    [InlineData("[User]?", true, true)]
    public void ConcurrentReadOnlyMemoization_PreservesTypeAndFingerprintResults(
        string type, bool isArray, bool isNullable)
    {
        var reference = WithArgument(1) with { Type = type };
        var expectedFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(reference);

        for (var iteration = 0; iteration < 32; iteration++)
        {
            var field = WithArgument(1) with { Type = type, IsNeverMerge = true };
            Parallel.For(0, 8, _ =>
            {
                field.IsArray.Should().Be(isArray);
                field.IsNullable.Should().Be(isNullable);
                FieldDefinitionExtensions.ComputeDeepFingerprint(field).Should().Be(expectedFingerprint);
            });

            field._subtreeHasAnyArguments.Should().BeTrue();
            field._deepArgumentFingerprint.Should().Be(expectedFingerprint);
            field.IsNeverMerge.Should().BeTrue();
        }
    }

    private static FieldDefinition WithArgument(int id)
        => new("user", "[User]?", null, new Dictionary<string, object?> { ["id"] = id });
}
