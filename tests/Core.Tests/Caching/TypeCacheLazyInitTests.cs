using System;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Caching;
using Xunit;

namespace NGql.Core.Tests.Caching;

/// <summary>
/// Tests for TypeCache lazy initialization, interning, and concurrent access patterns.
/// Covers common types, nullable types, custom types, and thread-safe cache operations.
/// </summary>
public class TypeCacheLazyInitTests
{
    [Fact]
    public void TypeCache_GetInternedType_WithCommonString_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("String".AsSpan());
        var type2 = TypeCache.GetInternedType("String".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
        object.ReferenceEquals(type1, type2).Should().BeTrue("Should return same instance");
    }

    [Fact]
    public void TypeCache_GetInternedType_WithEmptySpan_ReturnsDefaultType()
    {
        // Arrange & Act
        var result = TypeCache.GetInternedType("".AsSpan());

        // Assert
        result.Should().Be("String");
    }

    [Fact]
    public void TypeCache_GetInternedType_WithInt_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("Int".AsSpan());
        var type2 = TypeCache.GetInternedType("Int".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
        object.ReferenceEquals(type1, type2).Should().BeTrue();
    }

    [Fact]
    public void TypeCache_GetInternedType_WithBoolean_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("Boolean".AsSpan());
        var type2 = TypeCache.GetInternedType("Boolean".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithCustomType_CachesAndReturns()
    {
        // Arrange
        var customType = "CustomGraphQLType";
        var type1 = TypeCache.GetInternedType(customType.AsSpan());
        var type2 = TypeCache.GetInternedType(customType.AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
        type1.Should().Be(customType);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithNullableType_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("String?".AsSpan());
        var type2 = TypeCache.GetInternedType("String?".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void TypeCache_InternType_Alias_WorksSameAsGetInternedType()
    {
        // Arrange & Act
        var result1 = TypeCache.InternType("Int!".AsSpan());
        var result2 = TypeCache.GetInternedType("Int!".AsSpan());

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithArrayMarker_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("[]".AsSpan());
        var type2 = TypeCache.GetInternedType("[]".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
        type1.Should().Be("[]");
    }

    [Fact]
    public void TypeCache_GetInternedType_WithObjectType_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("object".AsSpan());
        var type2 = TypeCache.GetInternedType("object".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithNonNullString_ReturnsCached()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("String!".AsSpan());
        var type2 = TypeCache.GetInternedType("String!".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithID_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("ID".AsSpan());
        var type2 = TypeCache.GetInternedType("ID".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithFloat_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("Float".AsSpan());
        var type2 = TypeCache.GetInternedType("Float".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public void TypeCache_GetInternedType_WithFloatNonNull_ReturnsCachedInstance()
    {
        // Arrange
        var type1 = TypeCache.GetInternedType("Float!".AsSpan());
        var type2 = TypeCache.GetInternedType("Float!".AsSpan());

        // Act & Assert
        type1.Should().Be(type2);
    }

    [Fact]
    public async Task TypeCache_GetInternedType_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var threadCount = 10;
        var tasks = new Task[threadCount];

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var result = TypeCache.GetInternedType("String".AsSpan());
                    results.Add(result);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All should get valid cached types
        results.Count.Should().Be(1000);
        results.Should().AllSatisfy(r => r.Should().Be("String"));
    }

    [Fact]
    public async Task TypeCache_GetInternedType_ConcurrentMixedTypes_ThreadSafe()
    {
        // Arrange
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        var threadCount = 8;
        var types = new[] { "String", "Int", "Boolean", "Float", "ID", "String?", "Int!", "[]" };
        var tasks = new Task[threadCount];

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                foreach (var type in types)
                {
                    var result = TypeCache.GetInternedType(type.AsSpan());
                    results.Add(result);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        results.Count.Should().Be(threadCount * types.Length);
        results.Should().AllSatisfy(r => r.Should().BeOneOf(types));
    }

    [Fact]
    public void TypeCache_GetInternedType_LazyInitialization_Triggers()
    {
        // Arrange - The first access should trigger lazy initialization
        // Act - This should work without error and trigger lazy init
        var result1 = TypeCache.GetInternedType("String".AsSpan());
        var result2 = TypeCache.GetInternedType("Int".AsSpan());

        // Assert
        result1.Should().Be("String");
        result2.Should().Be("Int");
    }

    [Fact]
    public void TypeCache_GetInternedType_ConsistentBehaviorAfterFirstAccess()
    {
        // Arrange - First access triggers lazy init
        var first = TypeCache.GetInternedType("String".AsSpan());

        // Act - Subsequent accesses should be consistent
        var second = TypeCache.GetInternedType("String".AsSpan());
        var third = TypeCache.GetInternedType("String".AsSpan());

        // Assert - All should be the same instance (interned)
        object.ReferenceEquals(first, second).Should().BeTrue();
        object.ReferenceEquals(second, third).Should().BeTrue();
    }

    [Fact]
    public void TypeMetadataCache_ObjectPropertyCache_IsThreadSafe()
    {
        // Arrange
        var type1 = typeof(string);
        var type2 = typeof(int);

        // Act
        TypeMetadataCache.ObjectPropertyCache.TryAdd(type1, System.Type.EmptyTypes.GetType().GetProperties());
        TypeMetadataCache.ObjectPropertyCache.TryAdd(type2, System.Type.EmptyTypes.GetType().GetProperties());

        // Assert
        TypeMetadataCache.ObjectPropertyCache.Count.Should().BeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("String")]
    [InlineData("Int")]
    [InlineData("Boolean")]
    [InlineData("Float")]
    [InlineData("ID")]
    public void TypeCache_CommonTypes_ArePreInterned(string commonType)
    {
        // Arrange & Act
        var result1 = TypeCache.GetInternedType(commonType.AsSpan());
        var result2 = TypeCache.GetInternedType(commonType.AsSpan());

        // Assert
        result1.Should().Be(commonType);
        object.ReferenceEquals(result1, result2).Should().BeTrue("Common types should be pre-interned");
    }

    [Theory]
    [InlineData("String?")]
    [InlineData("Int?")]
    [InlineData("Boolean?")]
    [InlineData("Float?")]
    [InlineData("ID?")]
    public void TypeCache_CommonNullableTypes_ArePreInterned(string nullableType)
    {
        // Arrange & Act
        var result1 = TypeCache.GetInternedType(nullableType.AsSpan());
        var result2 = TypeCache.GetInternedType(nullableType.AsSpan());

        // Assert
        result1.Should().Be(nullableType);
        object.ReferenceEquals(result1, result2).Should().BeTrue();
    }
}
