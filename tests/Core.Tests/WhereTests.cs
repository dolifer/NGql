using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests;

public class WhereTests
{
    [Fact]
    public void Where_Empty_ReturnsEmptyObject()
    {
        // arrange
        var query = new Query("name");

        // act
        query.Where(new Dictionary<string, object>()
        {
            {"filter1", new{}},
            {"filter2", Array.Empty<int>()},
            {"filter3", null},
            {"filter4", 42}
        });

        // assert
        query.FieldsList.Should().BeEmpty();
            
        string queryText = query.ToString();
        queryText.Should().Be(@"query name(filter1:{}, filter2:[], filter3:null, filter4:42){
}");
    }
        
    [Fact]
    public void Where_VariableArgument_AddsToWhere()
    {
        // arrange
        var variable = new Variable("$id", "Int");
        var query = new Query("name");

        // act
        query.Where("id", variable);

        // assert
        query.Arguments.Should().ContainKey("id").WhoseValue.Should().Be(variable);

        // act
        var queryText = query.ToString();

        // assert
        query.Variables.Should().BeEquivalentTo([variable]);
        queryText.Should().Be(@"query name($id:Int){
}");
    }
        
    [Fact]
    public void Where_NumberArgument_AddsToWhere()
    {
        // arrange
        var query = new Query("name");

        // act
        query.Where("id", 42);

        // assert
        query.Arguments.Should().ContainKey("id").WhoseValue.Should().Be(42);

        // act
        var queryText = query.ToString();

        // assert
        queryText.Should().Be(@"query name(id:42){
}");
    }

    [Fact]
    public void Where_StringArgument_AddsToWhere()
    {
        // arrange
        var query = new Query("name");

        // act
        query.Where("name", "John");

        // assert
        query.Arguments.Should().ContainKey("name").WhoseValue.Should().Be("John");
    }

    [Fact]
    public void Where_Dictionary_Variable_AddsToWhere()
    {
        // arrange
        var toVariable = new Variable("$to", "Int");
        var fromVariable = new Variable("$from", "Int");
        var query = new Query("name", variables: toVariable);
        Dictionary<string, object> ageFilter = new()
        {
            {"to", toVariable},
            {"from", fromVariable}
        };

        // act
        var queryText = query.Where(ageFilter).ToString();

        // assert
        query.Variables.Should().BeEquivalentTo([fromVariable, toVariable]);
            
        query.Arguments.Should().ContainKey("from").WhoseValue.Should().Be(fromVariable);
        query.Arguments.Should().ContainKey("to").WhoseValue.Should().Be(toVariable);
            
        queryText.Should().Be(@"query name($from:Int, $to:Int){
}");
    }
        
    [Fact]
    public void Where_NestedDictionary_Variable_AddsToWhere()
    {
        // arrange
        var toVariable = new Variable("$to", "Int");
        var fromVariable = new Variable("$from", "Int");
        var query = new Query("name");
        Dictionary<string, object> ageFilter = new()
        {
            {"to", toVariable},
            {"from", fromVariable}
        };

        // act
        var queryText = query.Where("filters", ageFilter).ToString();

        // assert
        query.Variables.Should().BeEquivalentTo([fromVariable, toVariable]);
            
        var value = query.Arguments.Should().ContainKey("filters").WhoseValue as IDictionary<string, object>;
            
        value.Should().ContainKey("from").WhoseValue.Should().Be(fromVariable);
        value.Should().ContainKey("to").WhoseValue.Should().Be(toVariable);
            
        queryText.Should().Be(@"query name($from:Int, $to:Int, filters:{to:$to, from:$from}){
}");
    }
        
    [Fact]
    public void Where_SubQuery_Variable_AddsToWhere()
    {
        // arrange
        var rootVariable = new Variable("$useCache", "Boolean");
        var toVariable = new Variable("$to", "Int");
        var fromVariable = new Variable("$from", "Int");
        var query = new Query("name", variables: [rootVariable,rootVariable]);
        var subQuery = new Query("nested", variables: [toVariable, toVariable]);
        Dictionary<string, object> ageFilter = new()
        {
            {"to", toVariable},
            {"from", fromVariable}
        };

        // act
        var queryText = query.Select(subQuery.Where(ageFilter)).ToString();

        // assert
        query.Variables.Should().BeEquivalentTo([fromVariable, toVariable, rootVariable]);
        subQuery.Variables.Should().BeEquivalentTo([fromVariable, toVariable]);
            
        subQuery.Arguments.Should().ContainKey("from").WhoseValue.Should().Be(fromVariable);
        subQuery.Arguments.Should().ContainKey("to").WhoseValue.Should().Be(toVariable);
            
        queryText.Should().Be(@"query name($from:Int, $to:Int, $useCache:Boolean){
    nested(from:$from, to:$to){
    }
}");
    }
        
    [Fact]
    public void Where_Multiple_Variables_Sorted_RootQuery()
    {
        // arrange
        var variableA = new Variable("$a", "Int");
        var variableB = new Variable("$b", "Int");
        var variableC = new Variable("$c", "Int");
        var query = new Query("name", variables: [variableC, variableB, variableC]);

        // act
        query.Where("propB", variableB)
            .Where("propB2", variableB)
            .Where("propA", variableA);

        // assert
        query.Arguments.Should().ContainKey("propA").WhoseValue.Should().Be(variableA);
        query.Arguments.Should().ContainKey("propB").WhoseValue.Should().Be(variableB);
        query.Arguments.Should().ContainKey("propB2").WhoseValue.Should().Be(variableB);

        // act
        var queryText = query.ToString();

        // assert
        query.Variables.Should().BeEquivalentTo([variableA, variableB, variableC]);
        queryText.Should().Be(@"query name($a:Int, $b:Int, $c:Int){
}");
    }
        
    [Fact]
    public void Where_Dictionary_AddsToWhere()
    {
        // arrange
        var query = new Query("name");
        Dictionary<string, object> ageFilter = new()
        {
            {"from", 1},
            {"to", 100}
        };

        // act
        query.Where(ageFilter);

        // assert
        query.Arguments.Should().ContainKey("from").WhoseValue.Should().Be(1);
        query.Arguments.Should().ContainKey("to").WhoseValue.Should().Be(100);
    }

    [Fact]
    public void Where_DictionaryArgument_AddsToWhere()
    {
        // arrange
        var query = new Query("name");
        Dictionary<string, int> ageFilter = new()
        {
            {"from", 1},
            {"to", 100}
        };

        // act
        query.Where("age", ageFilter);

        // assert
        var storedFilter = query.Arguments.Should().ContainKey("age").WhoseValue as Dictionary<string, int>;

        storedFilter.Should().ContainKey("from").WhoseValue.Should().Be(1);
        storedFilter.Should().ContainKey("to").WhoseValue.Should().Be(100);
    }
}