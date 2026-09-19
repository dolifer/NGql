using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class HelpersBranchCoverageTests
{
    private static IEnumerable<object> LazySequence()
    {
        yield return 1;
        yield return "two";
    }

    [Fact]
    public void SortArgumentValue_NonCountableSequence_SortsEveryItem()
    {
        // Arrange: an iterator has no non-enumerated count, so the capacity hint falls back to 0.
        var sequence = LazySequence();

        // Act
        var sorted = Helpers.SortArgumentValue(sequence);

        // Assert
        sorted.Should().BeOfType<List<object?>>()
            .Which.Should().BeEquivalentTo(new object?[] { 1, "two" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void SortArgumentValue_CountableSequence_SortsEveryItem()
    {
        // Arrange: a List<object> reports its count up front, taking the other capacity branch.
        var sequence = new List<object> { 1, "two" };

        // Act
        var sorted = Helpers.SortArgumentValue(sequence);

        // Assert
        sorted.Should().BeOfType<List<object?>>()
            .Which.Should().BeEquivalentTo(new object?[] { 1, "two" }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void AreArgumentsEqual_SameCountDifferentKeys_ReturnsFalse()
    {
        // Arrange: equal counts, so the shape check passes and entry comparison must reject.
        var left = new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", 1 }
        };
        var right = new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "beta", 1 }
        };

        // Act & Assert
        Helpers.AreArgumentsEqual(left, right).Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_SameKeysSameValues_ReturnsTrue()
    {
        // Arrange
        var left = new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", 1 }
        };
        var right = new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", 1 }
        };

        // Act & Assert
        Helpers.AreArgumentsEqual(left, right).Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AreArgumentsEqual_OneSideHasNullValue_ReturnsFalse(bool nullOnLeft)
    {
        // Arrange: covers both orderings of the null check inside the value comparison.
        var left = new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", nullOnLeft ? null : "value" }
        };
        var right = new SortedDictionary<string, object?>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", nullOnLeft ? "value" : null }
        };

        // Act & Assert
        Helpers.AreArgumentsEqual(left, right).Should().BeFalse();
    }
}
