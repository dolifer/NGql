using System;
using FluentAssertions;
using NGql.Core.Caching;
using Xunit;

namespace NGql.Core.Tests.Caching;

public class TypeCacheTests
{
    [Theory]
    [InlineData("CustomUserType")]
    [InlineData("ProductInputType")]
    [InlineData("DateTimeScalar")]
    [InlineData("MyComplexType123")]
    public void GetInternedType_WithCustomType_ShouldCacheAndReturn(string customType)
    {
        // Arrange
        var typeSpan = customType.AsSpan();

        // Act
        var result1 = TypeCache.GetInternedType(typeSpan);
        var result2 = TypeCache.GetInternedType(typeSpan);

        // Assert
        result1.Should().Be(customType);
        result2.Should().Be(customType);
        ReferenceEquals(result1, result2).Should().BeTrue(); // Same cached reference
    }

    [Fact]
    public void GetInternedType_WithEmptySpan_ShouldReturnDefaultFieldType()
    {
        // Arrange
        var emptySpan = "".AsSpan();

        // Act
        var result = TypeCache.GetInternedType(emptySpan);

        // Assert
        result.Should().Be(Constants.DefaultFieldType);
        result.Should().Be("String");
    }

    [Theory]
    [InlineData("String")]
    [InlineData("Int")]
    [InlineData("Boolean")]
    [InlineData("ID")]
    [InlineData("object")]
    public void GetInternedType_WithCommonType_ShouldReturnCommonType(string commonType)
    {
        // Arrange
        var typeSpan = commonType.AsSpan();

        // Act
        var result = TypeCache.GetInternedType(typeSpan);

        // Assert
        result.Should().Be(commonType);
    }

    [Theory]
    [InlineData("String?")]
    [InlineData("Int?")]
    [InlineData("Boolean?")]
    public void GetInternedType_WithCommonNullableType_ShouldReturnCached(string nullableType)
    {
        // Arrange
        var typeSpan = nullableType.AsSpan();

        // Act
        var result = TypeCache.GetInternedType(typeSpan);

        // Assert
        result.Should().Be(nullableType);
    }

    [Fact]
    public void InternType_Alias_CallsGetInternedType()
    {
        // Arrange
        var uniqueType = "UniqueType" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        var typeSpan = uniqueType.AsSpan();

        // Act
        var result1 = TypeCache.InternType(typeSpan);
        var result2 = TypeCache.GetInternedType(typeSpan);

        // Assert - Both should return the same cached value
        result1.Should().Be(uniqueType);
        result1.Should().Be(result2);
        ReferenceEquals(result1, result2).Should().BeTrue();
    }

    [Fact]
    public void GetInternedType_WithMultipleDifferentCustomTypes_ShouldCacheEachSeparately()
    {
        // Arrange
        var type1 = "Type1_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
        var type2 = "Type2_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);

        // Act
        var result1a = TypeCache.GetInternedType(type1.AsSpan());
        var result2a = TypeCache.GetInternedType(type2.AsSpan());
        var result1b = TypeCache.GetInternedType(type1.AsSpan());
        var result2b = TypeCache.GetInternedType(type2.AsSpan());

        // Assert
        result1a.Should().Be(type1);
        result2a.Should().Be(type2);
        ReferenceEquals(result1a, result1b).Should().BeTrue();
        ReferenceEquals(result2a, result2b).Should().BeTrue();
        ReferenceEquals(result1a, result2a).Should().BeFalse();
    }
}
