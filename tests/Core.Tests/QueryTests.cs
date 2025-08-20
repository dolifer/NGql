using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FluentAssertions;
using Shared;
using Xunit;

namespace NGql.Core.Tests;

public class QueryTests
{
    [Fact]
    public Task Ctor_Sets_Name()
    {
        // arrange
        var query = new Query("name");

        // assert
        query.Name.Should().Be("name");
        query.Alias.Should().BeNull();

        query.FieldsList.Should().BeEmpty();
        query.Arguments.Should().BeEmpty();

        return query.Verify("emptyQuery");
    }

    [Fact]
    public Task Ctor_Sets_Alias()
    {
        // arrange
        var query = new Query("name", "alias");

        // assert
        query.Name.Should().Be("name");
        query.Alias.Should().Be("alias");

        query.FieldsList.Should().BeEmpty();
        query.Arguments.Should().BeEmpty();
            
        return query.Verify("emptyQuery");
    }

    [Fact]
    public Task AliasAs_Sets_Alias()
    {
        // arrange
        var query = new Query("name", "alias");

        // assert
        query.Alias.Should().Be("alias");
            
        return query.Verify("emptyQuery");
    }
        
    [Fact]
    public void Select_String_AddsToSelectList()
    {
        // arrange
        var query = new Query("name");

        // act
        query.Select("id", "name");

        // assert
        query.FieldsList.Should().BeEquivalentTo(new[] { "id", "name" });
            
        string queryText = query;
        queryText.Should().Be(@"query name{
    id
    name
}");
    }
        
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public Task Select_String_ReturnsEmptyQuery(string item)
    {
        // arrange
        var query = new Query("name");

        // act
        query.Select(item);

        // assert
        query.FieldsList.Should().BeEmpty();
            
        return query.Verify("emptyQuery");
    }
        
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public Task Select_ObjectArray_ReturnsEmptyQuery(string item)
    {
        // arrange
        var query = new Query("name");

        // act
        query.Select(new object[] { item });

        // assert
        query.FieldsList.Should().BeEmpty();
            
        return query.Verify("emptyQuery");
    }
        
    [Fact]
    public Task Select_Empty_Dictionary_ReturnsEmptyQuery()
    {
        // arrange
        var query = new Query("name");

        // act
        query.Where(new Dictionary<string, object>());

        // assert
        query.FieldsList.Should().BeEmpty();
            
        return query.Verify("emptyQuery");
    }

    [Fact]
    public Task Select_List_AddsToSelectList()
    {
        // arrange
        var query = new Query("name");

        // act
        query.Select(new List<string> {"id", "name"});

        // assert
        query.FieldsList.Should().BeEquivalentTo(new[] { "id", "name" });
            
        return query.Verify("idNameQuery");
    }

    [Fact]
    public Task Select_ChainOfCalls_AddsToSelectList()
    {
        // arrange
        var query = new Query("name");

        // act
        query
            .Select("id")
            .Select(new List<string> { "name"});

        // assert
        query.FieldsList.Should().BeEquivalentTo(new[] { "id", "name" });
            
        return query.Verify("idNameQuery");
    }

    [Fact]
    public void Variable_AddsToVariableList()
    {
        // arrange
        var query = new Query("name");

        // act
        query
            .Variable("$name", "String")
            .Variable("$name", "String")
            .Select("id");

        // assert
        query.Variables.Should().ContainSingle(x => x.Name == "$name" && x.Type == "String");
    }

    [Fact]
    public void Variable_Instance_AddsToVariableList()
    {
        // arrange
        var query = new Query("name");
        var variable = new Variable("$name", "String");

        // act
        query
            .Variable(variable)
            .Variable(variable)
            .Select("id");

        // assert
        query.Variables.Should().ContainSingle(x => x.Name == "$name" && x.Type == "String");
    }

    [Fact]
    public void ToString_UsesVariables()
    {
        // arrange
        var usersQuery = new Query("users")
            .Variable("$id", "String")
            .Variable("$id", "String")
            .Select("id", "name");

        // act
        string queryText = usersQuery;

        // assert
        queryText.Should().Be(@"query users($id:String){
    id
    name
}");
    }

    [Fact]
    public void ToString_Returns_SubQuery()
    {
        // arrange
        var query = new Query("myQuery");
        var usersQuery = new Query("users")
            .Select("id", "name");

        // act
        string queryText = query
            .Select(usersQuery);

        // assert
        queryText.Should().Be(@"query myQuery{
    users{
        id
        name
    }
}");
    }

    [Fact]
    public Task ToString_Returns_SubQueryAlias()
    {
        // arrange
        var query = new Query("myQuery");
        var usersQuery = new Query("users", "alias")
            .Select("id", "name");

        // act && assert
        return query
            .Select(usersQuery).Verify("idNameAliasMyQuery");
    }

    [Fact]
    public Task ToString_Includes_SubQueryAlias()
    {
        // act
        var query = new Query("myQuery")
            .Include("users", x => x
                    .Select("id", "name")
                , "alias");

        // assert
        return query.Verify("idNameAliasMyQuery");
    }

    [Fact]
    public void Include_AddsFieldsAndArgumentsFromObject()
    {
        // Arrange
        var query = new Query("TestQuery");
        var obj = new { child = new { prop1 = 42, prop2 = "name" }, prop3 = true };

        // Act
        query.Include(obj);

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    child{
        prop1
        prop2
    }
    prop3
}");
    }
        
    [Fact]
    public void Include_AddsFieldsAndArgumentsFromDictionary()
    {
        // Arrange
        var query = new Query("TestQuery");
        var obj = new
        {
            child = new Dictionary<string, object>
            {
                { "prop1", 42 },
                { "prop2", "name" },
                { "prop5", new Dictionary<string, object> { { "prop6", "value" } }}
            },
            prop3 = true
        };

        // Act
        query.Include(obj);

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    child{
        prop1
        prop2
        prop5{
            prop6
        }
    }
    prop3
}");
    }
        
    class ChildClass
    {
        [DataMember(Name = "age")]
        public int Prop1 { get; set; }
            
        [JsonPropertyName("name")]
        public string Prop2 { get; set; }
            
        [JsonPropertyName("Prop3")]
        public string Prop3 { get; set; }
    }
        
    [Fact]
    public void Include_AddsFieldsAndArgumentsFromObjectUsesAttributes()
    {
        // Arrange
        var query = new Query("TestQuery");
        var obj = new { child = new ChildClass { Prop1 = 42, Prop2 = "name" }, prop3 = true };

        // Act
        query.Include(obj);

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    child{
        Prop1:age
        Prop2:name
        Prop3
    }
    prop3
}");
    }
        
    [Fact]
    public void Include_AddsFieldsAndArgumentsFromTypeUsesAttributes()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query
            .Include<ChildClass>("child")
            .Select("prop3");

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    child{
        Prop1:age
        Prop2:name
        Prop3
    }
    prop3
}");
    }
        
    [Fact]
    public void Include_AddsFieldsAndArgumentsFromTypeUsesAttributesWithAlias()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query
            .Include<ChildClass>("child", "alias")
            .Select("prop3");

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    alias:child{
        Prop1:age
        Prop2:name
        Prop3
    }
    prop3
}");
    }
        
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(".")]
    public void IncludeAtPath_AddsFieldsAndArgumentsFromTypeUsesAttributesWithAlias(string path)
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query
            .IncludeAtPath<ChildClass>(path, "child", "alias")
            .Select("prop3");

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    alias:child{
        Prop1:age
        Prop2:name
        Prop3
    }
    prop3
}");
    }
        
    [Fact]
    public void IncludeAtPath_AddsToQueryAtGivenPath()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query
            .IncludeAtPath<ChildClass>("nested.here.iam", "child")
            .Select("prop3");

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    nested{
        here{
            iam{
                child{
                    Prop1:age
                    Prop2:name
                    Prop3
                }
            }
        }
    }
    prop3
}");
    }
        
    [Fact]
    public void IncludeAtPath_AddsToQueryAtGivenPathAndAlias()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query
            .IncludeAtPath<ChildClass>("nested.here.iam", "child", "alias")
            .Select("prop3");

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    nested{
        here{
            iam{
                alias:child{
                    Prop1:age
                    Prop2:name
                    Prop3
                }
            }
        }
    }
    prop3
}");
    }
        
    [Fact]
    public void IncludeAtPath_AddsToQueryAtGivenPathAndAliases()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query
            .IncludeAtPath<ChildClass>("l1:nested.l2:here.l3:iam", "child", "alias")
            .Select("prop3");

        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query TestQuery{
    l1:nested{
        l2:here{
            l3:iam{
                alias:child{
                    Prop1:age
                    Prop2:name
                    Prop3
                }
            }
        }
    }
    prop3
}");
    }
}
