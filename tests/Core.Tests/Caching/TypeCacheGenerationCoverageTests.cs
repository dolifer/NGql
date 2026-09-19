using System;
using FluentAssertions;
using NGql.Core.Caching;
using Xunit;

namespace NGql.Core.Tests.Caching;

[Collection("Cache retention")]
public class TypeCacheGenerationCoverageTests
{
    // One generation holds 2048 names. Re-touching the tracked name every 256 fillers keeps it
    // reachable across rotations: once the generation holding it becomes the previous one, the
    // next lookup finds it there and promotes it back into the current generation.
    private const int FillerCount = 6000;
    private const int TouchInterval = 256;

    [Fact]
    public void GetInternedType_NameRotatedIntoPreviousGeneration_IsPromotedAndReused()
    {
        // Arrange
        var name = "Rotated" + Guid.NewGuid().ToString("N");
        var first = TypeCache.GetInternedType(name.AsSpan());
        var prefix = "Filler" + Guid.NewGuid().ToString("N");

        // Act: churn past several rotations, re-reading the tracked name often enough that the
        // promotion path is taken rather than the name being dropped entirely.
        for (var i = 0; i < FillerCount; i++)
        {
            TypeCache.GetInternedType((prefix + i).AsSpan());
            if (i % TouchInterval == 0)
            {
                TypeCache.GetInternedType(name.AsSpan()).Should().BeSameAs(first);
            }
        }

        var promoted = TypeCache.GetInternedType(name.AsSpan());

        // Assert
        promoted.Should().BeSameAs(first);
    }

    [Fact]
    public void GetInternedType_NameMissingFromBothGenerations_ReturnsSuppliedName()
    {
        // Arrange
        var name = "NeverSeen" + Guid.NewGuid().ToString("N");

        // Act
        var interned = TypeCache.GetInternedType(name.AsSpan());

        // Assert
        interned.Should().Be(name);
        TypeCache.GetInternedType(name.AsSpan()).Should().BeSameAs(interned);
    }

    [Fact]
    public void InternType_CommonType_ReturnsPreInternedInstance()
    {
        // Arrange & Act
        var interned = TypeCache.InternType("String".AsSpan());

        // Assert
        interned.Should().BeSameAs(TypeCache.GetInternedType("String".AsSpan()));
    }
}
