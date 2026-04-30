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
        var parentQuery = new QueryBlock("parent", "", null, parentVar);

        var child1Var = new Variable("$childId", "ID");
        var child2Var = new Variable("$limit", "Int");
        var subQuery = new QueryBlock("child", "", null, child1Var, child2Var);

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
        var parentQuery = new QueryBlock("parent", "", null, sharedVar);
        var subQuery = new QueryBlock("child", "", null, sharedVar);

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
        var parent = new QueryBlock("parent", "", null, new Variable("$p1", "String"));
        var sub1 = new QueryBlock("sub1", "", null, new Variable("$s1", "String"));
        var sub2 = new QueryBlock("sub2", "", null, new Variable("$s2", "Int"));

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
        var parent = new QueryBlock("parent", "", null, new Variable("$parentId", "ID"));
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
        query.AddField("zebra", "apple", "middle");

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
        var query = new QueryBlock("query", "", null, var1, var2, var3);

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
        query.AddField("field1", "", "  ", "field2");

        // Assert
        query.FieldsList.Should().HaveCount(2);
        var fields = query.FieldsList.Cast<string>().ToList();
        fields.Should().Contain("field1");
        fields.Should().Contain("field2");
    }

    [Fact]
    public void AddField_NestedQueryBlocks_AreSortedByName()
    {
        var query = new QueryBlock("parent");
        var blockB = new QueryBlock("bbb");
        var blockA = new QueryBlock("aaa");

        query.AddField(new object[] { blockB, blockA });

        query.FieldsList.Should().HaveCount(2);
        // HandleAddField sorts list items using QueryBlock.Name — verify the alphabetical order.
        ((QueryBlock)query.FieldsList[0]).Name.Should().Be("aaa");
        ((QueryBlock)query.FieldsList[1]).Name.Should().Be("bbb");
    }

    [Fact]
    public void Constructor_DuplicateVariableNames_KeepsFirstOccurrenceOnly()
    {
        // DistinctBy(x => x.Name) drops the second entry; covers the "skip duplicate" branch
        // in the constructor's variable-deduplication step.
        var first = new Variable("$id", "ID");
        var duplicate = new Variable("$id", "String");
        var unique = new Variable("$limit", "Int");

        var block = new QueryBlock("query", "", null, first, duplicate, unique);

        block.Variables.Should().HaveCount(2);
        block.Variables.Should().Contain(first);
        block.Variables.Should().Contain(unique);
        block.Variables.Should().NotContain(duplicate);
    }

    [Fact]
    public void Constructor_EmptyAndNullVariableArray_BothProduceEmptyVariables()
    {
        // Cover the constructor's variable-init paths: explicit null (the `is null → true`
        // branch) AND an empty array (the `is null → false` branch with zero items to
        // collect into the spread).
        var blockNull = new QueryBlock("a", "", null, (Variable[]?)null);
        var blockEmpty = new QueryBlock("b", "", null, System.Array.Empty<Variable>());
        var blockNoVarArgs = new QueryBlock("c");

        blockNull.Variables.Should().BeEmpty();
        blockEmpty.Variables.Should().BeEmpty();
        blockNoVarArgs.Variables.Should().BeEmpty();
    }
}
