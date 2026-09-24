using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FieldChildrenIndexBranchTests
{
    private const int AboveIndexThreshold = 20;

    private static FieldChildren BuildIndexedChildren()
    {
        var children = new FieldChildren();
        for (int i = 0; i < AboveIndexThreshold; i++)
            children.Append(new FieldDefinition($"field{i}"));
        return children;
    }

    [Fact]
    public void AsSpan_OnCollectionWithoutBackingStorage_ReturnsEmptySpan()
    {
        var children = new FieldChildren();

        children.AsSpan().IsEmpty.Should().BeTrue();
        children.Count.Should().Be(0);
    }

    [Fact]
    public void AsSpan_AfterFirstAppend_ReturnsThatChild()
    {
        var children = new FieldChildren();
        var child = new FieldDefinition("only");
        children.Append(child);

        children.AsSpan().ToArray().Should().ContainSingle().Which.Should().BeSameAs(child);
    }

    [Fact]
    public void Find_OnCollectionWithoutBackingStorage_ReturnsNull()
    {
        var children = new FieldChildren();

        children.Find("absent").Should().BeNull();
        children.Find("absent".AsSpan()).Should().BeNull();
    }

    [Fact]
    public void Find_BySpanOnIndexedCollection_ResolvesThroughIndex()
    {
        var children = BuildIndexedChildren();

        var found = children.Find("FIELD7".AsSpan());

        found.Should().NotBeNull();
        found!.Name.Should().Be("field7");
    }

    [Fact]
    public void Find_BySpanOnIndexedCollectionForMissingName_ReturnsNull()
    {
        var children = BuildIndexedChildren();

        children.Find("absent".AsSpan()).Should().BeNull();
    }

    [Fact]
    public void Set_ByKeyDifferingFromChildNameOnIndexedCollection_RepointsIndex()
    {
        var children = BuildIndexedChildren();

        children.Set("field3", new FieldDefinition("renamed"));

        children.Find("renamed").Should().NotBeNull();
        children.Find("field3").Should().BeNull();
        children.Count.Should().Be(AboveIndexThreshold);
    }

    [Fact]
    public void Set_ByUnknownKeyOnIndexedCollection_Appends()
    {
        var children = BuildIndexedChildren();

        children.Set("absent", new FieldDefinition("absent"));

        children.Count.Should().Be(AboveIndexThreshold + 1);
        children.Find("absent").Should().NotBeNull();
    }

    [Fact]
    public void ReplaceReference_WhenIndexHintSlotHoldsTarget_ReplacesThatSlot()
    {
        var children = BuildIndexedChildren();
        var target = children.Find("field5")!;
        var replacement = new FieldDefinition("field5");

        children.ReplaceReference(target, replacement);

        children.Find("field5").Should().BeSameAs(replacement);
    }

    [Fact]
    public void ReplaceReference_WhenIndexHintSlotHoldsSameNamedSibling_FallsBackToReferenceScan()
    {
        var children = BuildIndexedChildren();
        var first = new FieldDefinition("dup") { Alias = "a" };
        var second = new FieldDefinition("dup") { Alias = "b" };
        children.Append(first);
        children.Append(second);
        var replacement = new FieldDefinition("dup") { Alias = "a2" };

        children.ReplaceReference(first, replacement);

        children.AsSpan().ToArray().Should().Contain(replacement);
        children.AsSpan().ToArray().Should().Contain(second);
        children.AsSpan().ToArray().Should().NotContain(first);
    }

    [Fact]
    public void ReplaceReference_OnUnindexedCollection_ReplacesByReference()
    {
        var children = new FieldChildren();
        var first = new FieldDefinition("dup") { Alias = "a" };
        var second = new FieldDefinition("dup") { Alias = "b" };
        children.Append(first);
        children.Append(second);
        var replacement = new FieldDefinition("dup") { Alias = "a2" };

        children.ReplaceReference(second, replacement);

        children.AsSpan().ToArray().Should().Contain(replacement);
        children.AsSpan().ToArray().Should().NotContain(second);
    }

    [Fact]
    public void ReplaceReference_WithChildNotPresent_LeavesCollectionUnchanged()
    {
        var children = BuildIndexedChildren();

        children.ReplaceReference(new FieldDefinition("absent"), new FieldDefinition("absent"));

        children.Count.Should().Be(AboveIndexThreshold);
    }
}
