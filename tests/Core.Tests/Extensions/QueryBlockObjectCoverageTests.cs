using System.Linq;
using System.Reflection;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class QueryBlockObjectCoverageTests
{
    public class BaseEntity
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
    }

    public class ShadowingEntity : BaseEntity
    {
        public new int Label { get; set; }
    }

    private static PropertyInfo PropertyOf<T>(string name)
        => typeof(T).GetProperties().First(p => p.Name == name && p.DeclaringType == typeof(T));

    [Fact]
    public void Include_TypeWithShadowedProperty_EmitsResponseNameOnce()
    {
        // Arrange & Act
        var query = new Query("Shadowed").Include<ShadowingEntity>("entity").ToString();

        // Assert
        query.Split("Label").Length.Should().Be(2, "the shadowed response name must be emitted exactly once");
        query.Should().Contain("Id");
    }

    [Fact]
    public void IsMoreDerivedThan_DerivedCandidateOverBaseCurrent_ReturnsTrue()
    {
        // Arrange
        var derived = PropertyOf<ShadowingEntity>("Label");
        var basic = PropertyOf<BaseEntity>("Label");

        // Act & Assert
        QueryBlockObjectExtensions.IsMoreDerivedThan(derived, basic).Should().BeTrue();
    }

    [Fact]
    public void IsMoreDerivedThan_BaseCandidateOverDerivedCurrent_ReturnsFalse()
    {
        // Arrange
        var derived = PropertyOf<ShadowingEntity>("Label");
        var basic = PropertyOf<BaseEntity>("Label");

        // Act & Assert
        QueryBlockObjectExtensions.IsMoreDerivedThan(basic, derived).Should().BeFalse();
    }

    [Fact]
    public void IsMoreDerivedThan_SameDeclaringType_ReturnsFalse()
    {
        // Arrange
        var label = PropertyOf<BaseEntity>("Label");
        var id = PropertyOf<BaseEntity>("Id");

        // Act & Assert
        QueryBlockObjectExtensions.IsMoreDerivedThan(label, id).Should().BeFalse();
    }
}
