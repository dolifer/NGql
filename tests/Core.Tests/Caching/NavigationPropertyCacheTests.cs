using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Caching;
using Xunit;

namespace NGql.Core.Tests.Caching;

/// <summary>
/// Tests for the navigation-property reflection cache added to <see cref="TypeMetadataCache"/>.
/// Verifies the cached lookup returns correct metadata, preserves case-sensitive
/// GetProperty semantics (including the ambiguous-match throw), and that navigation-property
/// expansion output is unchanged when routed through the cache.
/// </summary>
public class NavigationPropertyCacheTests
{
    [Fact]
    public void GetNavigationProperties_ReturnsSameCachedInstance_AcrossCalls()
    {
        var first = TypeMetadataCache.GetNavigationProperties(typeof(Sample));
        var second = TypeMetadataCache.GetNavigationProperties(typeof(Sample));

        second.Should().BeSameAs(first, "the per-type metadata should be memoized");
        second.Properties.Should().BeSameAs(first.Properties, "the PropertyInfo[] should not be rebuilt");
    }

    [Fact]
    public void GetProperty_ExactCase_ReturnsMatchingProperty()
    {
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(Sample));

        var property = metadata.GetProperty(nameof(Sample.Name));

        property.Should().NotBeNull();
        property!.Name.Should().Be(nameof(Sample.Name));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("NAME")]
    [InlineData("nAmE")]
    public void GetProperty_WrongCase_ReturnsNull_MatchingGetPropertySemantics(string lookup)
    {
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(Sample));

        var cached = metadata.GetProperty(lookup);
        var direct = typeof(Sample).GetProperty(lookup, BindingFlags.Public | BindingFlags.Instance);

        cached.Should().BeNull("cache must stay case-sensitive like Type.GetProperty");
        cached.Should().BeSameAs(direct);
    }

    [Fact]
    public void GetProperty_MissingName_ReturnsNull()
    {
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(Sample));

        metadata.GetProperty("DoesNotExist").Should().BeNull();
    }

    [Fact]
    public void GetProperty_ShadowedName_ResolvesToDerived_LikeReflection()
    {
        // Derived hides Value with a `new` property. The runtime de-dups shadowed names in
        // GetProperties (returning the most-derived), and GetProperty resolves to Derived without
        // throwing — the cache must reproduce exactly that.
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(Derived));

        var cached = metadata.GetProperty(nameof(Derived.Value));
        var direct = typeof(Derived).GetProperty(
            nameof(Derived.Value),
            BindingFlags.Public | BindingFlags.Instance);

        cached.Should().NotBeNull();
        direct.Should().NotBeNull();
        cached!.DeclaringType.Should().Be(typeof(Derived));
        cached.DeclaringType.Should().Be(direct!.DeclaringType);
    }

    [Fact]
    public void Properties_MatchGetPropertiesPublicInstance()
    {
        var metadata = TypeMetadataCache.GetNavigationProperties(typeof(Sample));

        var expected = typeof(Sample)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name);

        metadata.Properties.Select(p => p.Name).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void ExpandNavigationProperty_ThroughCache_ExpandsGetterOnlyProperty()
    {
        // Name is getter-only (navigation) → expands to the settable properties, never itself.
        var result = NavigationPropertyExpander.ExpandNavigationProperty("Name", typeof(NavModel));

        result.Should().Contain("FirstName");
        result.Should().Contain("LastName");
        result.Should().Contain("Id");
        result.Should().NotContain("Name");
    }

    [Fact]
    public void ExpandNavigationProperty_ThroughCache_KeepsRegularPropertyName()
    {
        var result = NavigationPropertyExpander.ExpandNavigationProperty("FirstName", typeof(NavModel));

        result.Should().ContainSingle().Which.Should().Be("FirstName");
    }

    public sealed class Sample
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    public class Base
    {
        public string Value { get; set; } = "";
    }

    public sealed class Derived : Base
    {
        public new string Value { get; set; } = "";
    }

    public sealed class NavModel
    {
        public string? Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Name => $"{FirstName} {LastName}";
    }
}
