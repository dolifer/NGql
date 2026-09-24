using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Exceptions;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Variables are deduplicated by name and type, so the same name with two types used to be kept
/// twice and rendered invalid GraphQL (<c>query Q($id:ID!, $id:Int)</c>). A conflicting declaration
/// now throws before anything is added; the same name with the same type still deduplicates.
/// </summary>
public class VariableTypeConflictTests
{
    private static Dictionary<string, object?> Arguments(Variable variable) => new() { ["id"] = variable };

    [Fact]
    public void AddField_SameVariableNameAndType_DeclaresOnce()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("a", Arguments(new Variable("$id", "ID!")))
            .AddField("b", Arguments(new Variable("$id", "ID!")));

        query.ToString().Should().Be("query Q($id:ID!){\n    a(id:$id)\n    b(id:$id)\n}");
    }

    [Fact]
    public void AddField_SameVariableNameDifferentType_ThrowsAndLeavesQueryUnchanged()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("a", Arguments(new Variable("$id", "ID!")));

        var act = () => query.AddField("b", Arguments(new Variable("$id", "Int")));

        act.Should().Throw<ArgumentException>().WithMessage("*$id*ID!*Int*");
        query.ToString().Should().Be("query Q($id:ID!){\n    a(id:$id)\n}");
    }

    [Fact]
    public void AddField_ConflictInsideOneArgumentDictionary_Throws()
    {
        var arguments = new Dictionary<string, object?>
        {
            ["id"] = new Variable("$id", "ID!"),
            ["filter"] = new Dictionary<string, object?> { ["owner"] = new Variable("$id", "String") },
        };

        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("a", arguments);

        act.Should().Throw<ArgumentException>().WithMessage("*$id*");
    }

    [Fact]
    public void FieldBuilder_IncludeIfThenConflictingArgument_Throws()
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("user", u => u
            .IncludeIf(new Variable("$show", "Boolean!"))
            .AddField("posts", new Dictionary<string, object?> { ["show"] = new Variable("$show", "String") }));

        act.Should().Throw<ArgumentException>().WithMessage("*$show*");
    }

    [Fact]
    public void FieldBuilder_WhereWithConflictingVariable_Throws()
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("a", Arguments(new Variable("$id", "ID!")))
            .AddField("b", b => b.Where("id", new Variable("$id", "Int")));

        act.Should().Throw<ArgumentException>().WithMessage("*$id*");
    }

    [Fact]
    public void FieldBuilder_IncludeIfConflictingVariable_ThrowsWithoutAddingDirective()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("a", Arguments(new Variable("$show", "String")));
        FieldBuilder? captured = null;
        query.AddField("b", b => captured = b);

        var act = () => captured!.IncludeIf(new Variable("$show", "Boolean!"));

        act.Should().Throw<ArgumentException>();
        query.Definition.Fields["b"].HasDirectives.Should().BeFalse();
    }

    [Fact]
    public void Include_ConflictingVariableTypes_ThrowsAndLeavesTargetUnchanged()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Q").AddField("a", Arguments(new Variable("$id", "ID!")));
        var fragment = QueryBuilder.CreateDefaultBuilder("F").AddField("b", Arguments(new Variable("$id", "Int")));

        var act = () => target.Include(fragment);

        act.Should().Throw<QueryMergeException>().WithMessage("*$id*ID!*Int*");
        target.ToString().Should().Be("query Q($id:ID!){\n    a(id:$id)\n}");
    }

    [Fact]
    public void AddField_SubFieldDefinitionWithVariableArgument_DeclaresVariable()
    {
        var posts = new NGql.Core.Abstractions.FieldDefinition("posts", "String", null,
            new Dictionary<string, object?> { ["first"] = new Variable("$n", "Int") });

        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", new[] { posts });

        query.ToString().Should().Be("query Q($n:Int){\n    user{\n        posts(first:$n)\n    }\n}");
    }

    [Fact]
    public void FieldBuilder_NestedArgumentVariable_IsDeclared()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("posts", new Dictionary<string, object?> { ["first"] = new Variable("$n", "Int") }, new[] { "id" }));

        query.ToString().Should().Be("query Q($n:Int){\n    user{\n        posts(first:$n){\n            id\n        }\n    }\n}");
    }

    [Fact]
    public void FieldBuilder_WhereOnFieldWithoutArguments_StaysAttached()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.Where("id", new Variable("$id", "ID!")).AddField("name"));

        query.ToString().Should().Be("query Q($id:ID!){\n    user(id:$id){\n        name\n    }\n}");
    }
}
