using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Field lookup during preservation enumerates the root Dictionary and FieldChildren through their
/// struct enumerators; any other read-only dictionary still goes through the interface. The
/// projected query's variable set is created only when a preserved field can hold a variable.
/// </summary>
public class PreservationLookupTests
{
    [Fact]
    public void FindFieldByNameOrAlias_OtherReadOnlyDictionary_MatchesByAlias()
    {
        var field = new FieldDefinition("user", "String", "account");
        var fields = new ReadOnlyDictionary<string, FieldDefinition>(new Dictionary<string, FieldDefinition> { ["account"] = field });

        var match = PreserveExtensions.FindFieldByNameOrAlias(fields, "ACCOUNT");

        match.Should().NotBeNull();
        match!.Value.Value.Should().BeSameAs(field);
        PreserveExtensions.FindFieldByNameOrAlias(fields, "missing").Should().BeNull();
    }

    [Fact]
    public void Preserve_FieldsWithoutArguments_DoesNotCreateVariableSet()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Q").AddField("user.name").AddField("user.email");

        var preserved = PreservationBuilder.Create(source).Preserve("user.name").Build();

        preserved.Definition._variables.Should().BeNull();
        preserved.ToString().Should().Be("query Q{\n    user{\n        name\n    }\n}");
    }

    [Fact]
    public void Preserve_FieldWithVariableArgument_DeclaresVariable()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID!") }, new[] { "name" });

        var preserved = PreservationBuilder.Create(source).Preserve("user.name").Build();

        preserved.Variables.Should().ContainSingle(v => v.Name == "$id");
    }
}
