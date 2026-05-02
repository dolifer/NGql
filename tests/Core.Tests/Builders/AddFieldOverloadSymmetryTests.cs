using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Tests the 2.1 footgun-killing overloads on QueryBuilder and FieldBuilder.
/// These overloads exist to remove the need for callers to remember magic
/// parameter orderings or pass <c>metadata: null</c> to access common shapes.
/// </summary>
public class AddFieldOverloadSymmetryTests
{
    // ── QueryBuilder.AddField(field, args, lambda) ─────────────────────────────

    [Fact]
    public void QueryBuilder_AddField_ArgsAndLambda_Renders_Correctly()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("WithArgsLambda")
            .AddField("user",
                new Dictionary<string, object?> { ["id"] = "abc" },
                b => b.AddField("name").AddField("email"))
            .ToString();

        // Assert
        query.Should().Contain("user(id:\"abc\")");
        query.Should().Contain("email");
        query.Should().Contain("name");
    }

    [Fact]
    public void QueryBuilder_AddField_ArgsAndLambda_Equivalent_To_Explicit_MetadataNull()
    {
        // The new shorthand must produce identical output to the explicit four-arg form.
        var args = new Dictionary<string, object?> { ["id"] = "abc" };

        var shorthand = QueryBuilder.CreateDefaultBuilder("X")
            .AddField("user", args, b => b.AddField("name"))
            .ToString();

        var explicitForm = QueryBuilder.CreateDefaultBuilder("X")
            .AddField("user", args, metadata: null, b => b.AddField("name"))
            .ToString();

        shorthand.Should().Be(explicitForm);
    }

    // ── QueryBuilder.AddField(field, subFields, lambda) ────────────────────────

    [Fact]
    public void QueryBuilder_AddField_SubFieldsAndLambda_Adds_Both_Static_And_Dynamic_Children()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Hybrid")
            .AddField("user",
                new[] { "id", "name" },
                b => b.AddField("posts", new[] { "title" }))
            .ToString();

        // Assert — static sub-fields and the dynamically-added "posts" should all render
        query.Should().Contain("id");
        query.Should().Contain("name");
        query.Should().Contain("posts");
        query.Should().Contain("title");
    }

    // ── FieldBuilder.AddField args-first symmetry ──────────────────────────────

    [Fact]
    public void FieldBuilder_AddField_ArgsFirstSubFields_Matches_SubFieldsFirst()
    {
        // Both orderings must produce identical output now that the args-first
        // overload exists for symmetry with QueryBuilder.AddField.
        var args = new Dictionary<string, object?> { ["first"] = 10 };
        var subFields = new[] { "name", "stargazerCount" };

        var argsFirst = QueryBuilder.CreateDefaultBuilder("A")
            .AddField("user", b => b.AddField("repositories", args, subFields))  // args, subFields
            .ToString();

        var subFieldsFirst = QueryBuilder.CreateDefaultBuilder("A")
            .AddField("user", b => b.AddField("repositories", subFields, args))  // subFields, args
            .ToString();

        argsFirst.Should().Be(subFieldsFirst);
    }

    [Fact]
    public void FieldBuilder_AddField_ArgsFirstSubFields_With_Lambda_Renders_Correctly()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("B")
            .AddField("user", b => b.AddField(
                "repositories",
                new Dictionary<string, object?> { ["first"] = 10 },
                new[] { "name" },
                inner => inner.AddField("owner", new[] { "login" })))
            .ToString();

        // Assert
        query.Should().Contain("repositories(first:10)");
        query.Should().Contain("name");
        query.Should().Contain("owner");
        query.Should().Contain("login");
    }

    [Fact]
    public void FieldBuilder_AddField_ArgsFirstSubFields_With_Metadata_And_Lambda_Renders_Correctly()
    {
        // Arrange & Act — exercises the 5-arg args-first overload
        var query = QueryBuilder.CreateDefaultBuilder("C")
            .AddField("user", b => b.AddField(
                "repositories",
                new Dictionary<string, object?> { ["first"] = 5 },
                new[] { "id" },
                metadata: new Dictionary<string, object?> { ["cached"] = true },
                inner => inner.AddField("name")))
            .ToString();

        // Assert
        query.Should().Contain("repositories(first:5)");
        query.Should().Contain("id");
        query.Should().Contain("name");
    }
}
