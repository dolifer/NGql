using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Field creation used to materialize the same text several times: a new root segment copied its
/// name for the dictionary key, the field name and the path; a dotted leaf copied the caller's
/// whole string as its path. Equal strings now share one instance.
/// </summary>
public class FieldStringReuseTests
{
    [Fact]
    public void AddField_DottedPath_RootSegmentSharesKeyNameAndPath()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user.profile.name");

        var (key, root) = SingleRoot(query);

        root.Name.Should().BeSameAs(key);
        root.Path.Should().BeSameAs(key);
    }

    [Fact]
    public void AddField_DottedPath_LeafPathIsCallerString()
    {
        var path = string.Concat("user.profile.", "name");
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField(path);

        var leaf = query.Definition.Fields["user"].Fields["profile"].Fields["name"];

        leaf.Path.Should().BeSameAs(path);
    }

    [Fact]
    public void AddField_SimpleFieldWithArguments_UsesCallerStringForKeyNameAndPath()
    {
        var name = string.Concat("us", "er");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField(name, new Dictionary<string, object?> { ["id"] = 1 });

        var (key, root) = SingleRoot(query);

        key.Should().BeSameAs(name);
        root.Name.Should().BeSameAs(name);
        root.Path.Should().BeSameAs(name);
    }

    [Fact]
    public void AddField_SubFieldArray_DoesNotAttachEmptyMetadata()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", new[] { "id", "name" });

        var user = query.Definition.Fields["user"];

        user.Fields["id"].HasMetadata.Should().BeFalse();
        user.Fields["id"]._metadata.Should().BeNull();
    }

    [Fact]
    public void AddField_WithLambdaAndNoVariables_DoesNotCreateVariableSet()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", b => b.AddField("id"));

        query.Definition._variables.Should().BeNull();
    }

    private static (string Key, FieldDefinition Field) SingleRoot(QueryBuilder query)
    {
        var (key, field) = query.Definition.FieldsInternal.Single();
        return (key, field);
    }
}
