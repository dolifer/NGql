using System.Reflection;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Caching;
using Xunit;

namespace NGql.Core.Tests.Caching;

public class NavigationPropertyMetadataCoverageTests
{
    public class BaseWithValue
    {
        public string? Value { get; set; }
        public string? Unshadowed { get; set; }
    }

    public class DerivedShadowingValue : BaseWithValue
    {
        public new int Value { get; set; }
    }

    [Fact]
    public void GetProperty_ShadowedPropertyName_ThrowsAmbiguousMatch()
    {
        // Arrange
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(DerivedShadowingValue));

        // Act
        var act = () => metadata.GetProperty("Value");

        // Assert
        act.Should().Throw<AmbiguousMatchException>();
    }

    [Fact]
    public void GetProperty_UnshadowedPropertyName_ReturnsDeclaration()
    {
        // Arrange
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(DerivedShadowingValue));

        // Act
        var property = metadata.GetProperty("Unshadowed");

        // Assert
        property.Should().NotBeNull();
        property!.Name.Should().Be("Unshadowed");
    }

    [Fact]
    public void GetProperty_UnknownPropertyName_ReturnsNull()
    {
        // Arrange
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(DerivedShadowingValue));

        // Act
        var property = metadata.GetProperty("NoSuchProperty");

        // Assert
        property.Should().BeNull();
    }

    [Fact]
    public void GetNavigationProperties_ShadowedType_KeepsBothDeclarationsInProperties()
    {
        // Arrange & Act
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(DerivedShadowingValue));

        // Assert
        metadata.Properties.Should().HaveCount(3, "GetProperties returns both Value declarations plus Unshadowed");
    }

    [Fact]
    public void ExpandNavigationProperty_ShadowedPropertyName_ReturnsOriginalFieldName()
    {
        // Arrange & Act
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Value", typeof(DerivedShadowingValue));

        // Assert
        result.Should().ContainSingle().Which.Should().Be("Value");
    }

    [Fact]
    public void ExpandNavigationProperty_ShadowedPropertyInNestedPath_ReturnsOriginalFieldName()
    {
        // Arrange & Act
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Value.Nested", typeof(DerivedShadowingValue));

        // Assert
        result.Should().ContainSingle().Which.Should().Be("Value.Nested");
    }
}
