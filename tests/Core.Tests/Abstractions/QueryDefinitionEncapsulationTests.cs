using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

// The declared (compile-time) type of the property is what closes the leak — the runtime object
// is still a concrete Dictionary; a consumer holding the read-only interface simply cannot reach
// its mutators without an explicit downcast.

namespace NGql.Core.Tests.Abstractions;

/// <summary>
/// Regression tests proving the field and metadata dictionaries are no longer exposed as live
/// mutable state. A consumer that could Clear/Remove/insert on the returned dictionary would
/// desync the builder's query map and path index; the fix narrows the public getters to
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/>.
/// </summary>
public class QueryDefinitionEncapsulationTests
{
    [Fact]
    public void Fields_PublicGetter_DeclaredType_IsReadOnlyDictionary()
    {
        var declaredType = typeof(QueryDefinition).GetProperty(nameof(QueryDefinition.Fields))!.PropertyType;

        declaredType.Should().Be(typeof(IReadOnlyDictionary<string, FieldDefinition>));
        typeof(IDictionary<string, FieldDefinition>).IsAssignableFrom(declaredType).Should().BeFalse(
            "the declared type must not expose Add/Remove/Clear to consumers");
    }

    [Fact]
    public void Metadata_PublicGetter_DeclaredType_IsReadOnlyDictionary()
    {
        var declaredType = typeof(QueryDefinition).GetProperty(nameof(QueryDefinition.Metadata))!.PropertyType;

        declaredType.Should().Be(typeof(IReadOnlyDictionary<string, object?>));
        typeof(IDictionary<string, object?>).IsAssignableFrom(declaredType).Should().BeFalse(
            "the declared type must not expose Add/Remove/Clear to consumers");
    }

    [Fact]
    public void Metadata_PublicGetter_HasNoPublicSetter()
    {
        var setter = typeof(QueryDefinition).GetProperty(nameof(QueryDefinition.Metadata))!.SetMethod;

        // No public setter — a consumer must not be able to swap the whole bag.
        (setter is null || !setter.IsPublic).Should().BeTrue();
    }

    [Fact]
    public void Fields_ReadAccess_StillRendersCorrectlyAfterObtainingReadOnlyView()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user")
            .AddField("account");

        // Obtaining the read-only view and enumerating it must not disturb rendering.
        var view = builder.Definition.Fields;
        view.Should().ContainKeys("user", "account");
        view.Count.Should().Be(2);
        view.TryGetValue("user", out var userField).Should().BeTrue();
        userField.Should().NotBeNull();

        builder.ToString().Should().Contain("user").And.Contain("account");
    }
}
