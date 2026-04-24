using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class QueryBlockTests
{
    [Fact]
    public void AddField_WithSubQueryContainingVariables_MergesVariablesIntoParent()
    {
        // Arrange
        var parentVar = new Variable("$parentId", "ID");
        var parentQuery = new QueryBlock("parent", variables: [parentVar]);
        
        var child1Var = new Variable("$childId", "ID");
        var child2Var = new Variable("$limit", "Int");
        var subQuery = new QueryBlock("child", variables: [child1Var, child2Var]);

        // Act
        parentQuery.AddField(subQuery);

        // Assert
        parentQuery.Variables.Should().HaveCount(3);
        parentQuery.Variables.Should().Contain(v => v.Name == "$parentId");
        parentQuery.Variables.Should().Contain(v => v.Name == "$childId");
        parentQuery.Variables.Should().Contain(v => v.Name == "$limit");
        parentQuery.FieldsList.Should().Contain(subQuery);
    }

    [Fact]
    public void AddField_WithSubQueryHavingDuplicateVariables_PreservesBothReferences()
    {
        // Arrange
        var sharedVar = new Variable("$id", "ID");
        var parentQuery = new QueryBlock("parent", variables: [sharedVar]);
        var subQuery = new QueryBlock("child", variables: [sharedVar]);

        // Act
        parentQuery.AddField(subQuery);

        // Assert - Variables collection handles duplicates
        parentQuery.Variables.Should().Contain(v => v.Name == "$id");
        parentQuery.FieldsList.Should().Contain(subQuery);
    }

    [Fact]
    public void AddField_WithMultipleSubQueries_MergesAllVariables()
    {
        // Arrange
        var parent = new QueryBlock("parent", variables: [new Variable("$p1", "String")]);
        var sub1 = new QueryBlock("sub1", variables: [new Variable("$s1", "String")]);
        var sub2 = new QueryBlock("sub2", variables: [new Variable("$s2", "Int")]);

        // Act
        parent.AddField(sub1);
        parent.AddField(sub2);

        // Assert
        parent.Variables.Should().HaveCount(3);
        parent.FieldsList.Should().HaveCount(2);
        parent.FieldsList.Should().Contain(sub1);
        parent.FieldsList.Should().Contain(sub2);
    }

    [Fact]
    public void AddField_WithSubQueryContainingNoVariables_StillAddsField()
    {
        // Arrange
        var parent = new QueryBlock("parent", variables: [new Variable("$parentId", "ID")]);
        var subQuery = new QueryBlock("child"); // No variables

        // Act
        parent.AddField(subQuery);

        // Assert
        parent.Variables.Should().HaveCount(1); // Only parent variable
        parent.FieldsList.Should().Contain(subQuery);
    }

    [Fact]
    public void AddField_WithStringArray_SortsAndAddsAllFields()
    {
        // Arrange
        var query = new QueryBlock("query");

        // Act
        query.AddField(new[] { "zebra", "apple", "middle" });

        // Assert
        query.FieldsList.Should().HaveCount(3);
        var fields = query.FieldsList.Cast<string>().ToList();
        fields[0].Should().Be("apple");
        fields[1].Should().Be("middle");
        fields[2].Should().Be("zebra");
    }

    [Fact]
    public void AddField_WithMixedStringAndQueryBlock_SupportsHeterogenousList()
    {
        // Arrange
        var query = new QueryBlock("parent");
        var subQuery = new QueryBlock("child");

        // Act
        query.AddField("field1");
        query.AddField(subQuery);
        query.AddField("field2");

        // Assert
        query.FieldsList.Should().HaveCount(3);
        query.FieldsList.OfType<string>().Should().HaveCount(2);
        query.FieldsList.OfType<QueryBlock>().Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithVariables_DeduplicatesVariablesByName()
    {
        // Arrange
        var var1 = new Variable("$id", "ID");
        var var2 = new Variable("$id", "ID");
        var var3 = new Variable("$name", "String");

        // Act
        var query = new QueryBlock("query", variables: [var1, var2, var3]);

        // Assert - Duplicate $id is removed
        query.Variables.Should().HaveCount(2);
        query.Variables.Should().Contain(v => v.Name == "$id");
        query.Variables.Should().Contain(v => v.Name == "$name");
    }

    [Fact]
    public void AddField_WithEmptyStringFromArray_IgnoresIt()
    {
        // Arrange
        var query = new QueryBlock("query");

        // Act
        query.AddField(new[] { "field1", "", "  ", "field2" });

        // Assert
        query.FieldsList.Should().HaveCount(2);
        var fields = query.FieldsList.Cast<string>().ToList();
        fields.Should().Contain("field1");
        fields.Should().Contain("field2");
    }
}
