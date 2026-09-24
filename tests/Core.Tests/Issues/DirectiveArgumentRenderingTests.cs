using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Directive arguments render through the struct enumerator of the normalized SortedDictionary.
/// A record initializer can still supply another dictionary type, or an empty one.
/// </summary>
public class DirectiveArgumentRenderingTests
{
    [Fact]
    public void Render_DirectiveWithInitializerDictionary_RendersItsEntries()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user");
        query.Definition.Fields["user"].AddDirective(
            new FieldDirective("format") { Arguments = new Dictionary<string, object?> { ["as"] = "ISO8601" } });

        query.ToString().Should().Be("query Q{\n    user @format(as:\"ISO8601\")\n}");
    }

    [Fact]
    public void Render_DirectiveWithEmptyInitializerDictionary_RendersNameOnly()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user");
        query.Definition.Fields["user"].AddDirective(
            new FieldDirective("cached") { Arguments = new Dictionary<string, object?>() });

        query.ToString().Should().Be("query Q{\n    user @cached\n}");
    }

    [Fact]
    public void IncludeIf_String_RendersSameAsVariableOverload()
    {
        var fromString = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", u => u.IncludeIf("$show").AddField("id"));
        var fromVariable = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", u => u.IncludeIf(new Variable("$show", "Boolean!")).AddField("id"));

        fromString.Definition.Fields["user"].Directives.Should().ContainSingle()
            .Which.IsStructurallyEqualTo(fromVariable.Definition.Fields["user"].Directives[0]).Should().BeTrue();
    }
}
