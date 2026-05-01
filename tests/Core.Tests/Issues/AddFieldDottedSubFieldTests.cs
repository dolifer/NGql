using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: <c>QueryBuilder.AddField(string, FieldDefinition[])</c> rejected sub-fields
/// whose <c>Name</c> contained dots, even though <c>AddField(string)</c> accepts dotted paths
/// and expands them into nested fields.
///
/// The two overloads must agree: a dotted name on either side should be expanded into
/// a chain of nested fields by <c>FieldFactory</c>, not rejected as an invalid identifier.
/// Adapters (e.g. trait converters that build <see cref="FieldDefinition"/> instances from
/// configuration paths) rely on this symmetry.
/// </summary>
public class AddFieldDottedSubFieldTests
{
    [Fact]
    public void AddField_WithDottedFieldDefinitionSubField_DoesNotThrow()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        var nested = new FieldDefinition("session.lastLogin", "DateTimeOffset?");

        var act = () => qb.AddField("metrics", subFields: new[] { nested });

        act.Should().NotThrow();
    }

    [Fact]
    public void AddField_WithDottedAliasedFieldDefinitionSubField_DoesNotThrow()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        var nested = new FieldDefinition("session.LastLogin:lastLogin", "DateTimeOffset?");

        var act = () => qb.AddField("Metrics:metrics", subFields: new[] { nested });

        act.Should().NotThrow();
    }

    [Fact]
    public void AddField_WithDottedSubField_ExpandsIntoNestedTree()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        var nested = new FieldDefinition("session.lastLogin", "DateTimeOffset?");

        qb.AddField("metrics", subFields: new[] { nested });

        // Both segments should be present in the rendered output
        var rendered = qb.ToString();
        rendered.Should().Contain("metrics");
        rendered.Should().Contain("session");
        rendered.Should().Contain("lastLogin");
    }

    [Fact]
    public void AddField_WithDottedStringSubField_DoesNotThrow()
    {
        // The string[] sub-fields overload internally wraps each string into a FieldDefinition,
        // routing through the same FieldBuilder.AddField(FieldDefinition) code path.
        var qb = QueryBuilder.CreateDefaultBuilder("Q");

        var act = () => qb.AddField("metrics", subFields: new[] { "session.lastLogin" });

        act.Should().NotThrow();
        qb.ToString().Should().Contain("session").And.Contain("lastLogin");
    }

    [Fact]
    public void AddField_WithArgumentsAndDottedStringSubField_DoesNotThrow()
    {
        // The (string, args, string[]) overload wraps strings into FieldDefinitions too.
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        var args = new Dictionary<string, object?> { ["limit"] = 10 };

        var act = () => qb.AddField("metrics", args, subFields: new[] { "session.lastLogin" });

        act.Should().NotThrow();
        qb.ToString().Should().Contain("session").And.Contain("lastLogin");
    }

    [Fact]
    public void AddField_WithDottedSubFieldContainingInvalidSegment_StillRejects()
    {
        // The relaxation only allows dot-as-separator. Each segment must still be a valid
        // GraphQL identifier — segments containing illegal characters are still rejected.
        var qb = QueryBuilder.CreateDefaultBuilder("Q");
        var nested = new FieldDefinition("session.bad-name", "String");

        var act = () => qb.AddField("metrics", subFields: new[] { nested });

        act.Should().Throw<System.ArgumentException>()
            .WithMessage("*invalid character*");
    }
}
