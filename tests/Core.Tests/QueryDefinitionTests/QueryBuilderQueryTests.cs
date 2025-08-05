using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.QueryDefinitionTests;

public class QueryBuilderQueryTests
{
    [Theory]
    [InlineData("parent")]
    [InlineData("parent.")]
    [InlineData("parent.. ")]
    public void Simple_Fields_Syntax(string fieldName)
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("SimpleQuery")
            .AddField(fieldName);
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SimpleQuery{
    parent
}");
    }
    
    [Theory]
    [InlineData("alias:parent")]
    [InlineData("alias::parent")]
    [InlineData("::alias:parent.")]
    [InlineData("alias:parent::.")]
    [InlineData("alias:parent:.. ")]
    public void Simple_Fields_Syntax_With_Alias(string fieldName)
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("SimpleQuery")
            .AddField(fieldName);
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SimpleQuery{
    alias:parent
}");
    }
    
    [Fact]
    public void Simple_Nested_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("SimpleQuery")
            .AddField("parent.child");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SimpleQuery{
    parent{
        child
    }
}");
    }
    
    [Fact]
    public void Simple_Nested_Fields_Alias_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("SimpleQuery")
            .AddField("alias:parent.alias:child");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SimpleQuery{
    alias:parent{
        alias:child
    }
}");
    }
    
    [Fact]
    public void Multiple_Nested_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.grandchild1")
            .AddField("parent.child2.grandchild2");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query ComplexQuery{
    parent{
        child1{
            grandchild1
        }
        child2{
            grandchild2
        }
    }
}");
    }
    
    [Fact]
    public void Multiple_Top_Level_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("MultipleTopLevelQuery")
            .AddField("field1")
            .AddField("field2");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query MultipleTopLevelQuery{
    field1
    field2
}");
    }
    
    [Fact]
    public void Nested_And_Top_Level_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("MixedQuery")
            .AddField("field1")
            .AddField("parent.child");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query MixedQuery{
    field1
    parent{
        child
    }
}");
    }
    
    [Fact]
    public void Deeply_Nested_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("DeepQuery")
            .AddField("level1.level2.level3.level4");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query DeepQuery{
    level1{
        level2{
            level3{
                level4
            }
        }
    }
}");
    }
    
    [Fact]
    public void Fields_With_Multiple_Siblings_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("SiblingsQuery")
            .AddField("parent.child1")
            .AddField("parent.child2")
            .AddField("parent.child3");
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SiblingsQuery{
    parent{
        child1
        child2
        child3
    }
}");
    }
    
    [Fact]
    public void Complex_Combination_Of_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexCombinationQuery")
            .AddField("field1")
            .AddField("parent1", subFields:["child1", "child2"])
            .AddField("parent1.child2")
            .AddField("parent2.child", subFields:["gc:grandchild", "grandchild2"]);
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query ComplexCombinationQuery{
    field1
    parent1{
        child1
        child2
    }
    parent2{
        child{
            gc:grandchild
            grandchild2
        }
    }
}");
    }
    
    [Fact]
    public void Complex_Combination_Of_Fields_Syntax_Include()
    {
        // Arrange
        var parent1 = QueryBuilder
            .CreateDefaultBuilder("Parent1")
            .AddField("f1alias:field1.child1", arguments: new Dictionary<string, object?>() { { "first", "true" } })
            .AddField("field1.child1.edges.node", subFields: ["child11", "child12"])
            .AddField("field1.child1.edges.node.metrics.values.foo", subFields: ["bar", "baz"]);
            
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexCombinationQuery")
            .Include(parent1);
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query ComplexCombinationQuery{
    f1alias:field1{
        child1(first:""true""){
            edges{
                node{
                    child11
                    child12
                    metrics{
                        values{
                            foo{
                                bar
                                baz
                            }
                        }
                    }
                }
            }
        }
    }
}");
    }

    [Fact]
    public void Ensure_Include_Correctly_WithAlias()
    {
        QueryBuilder myQuery = QueryBuilder.CreateDefaultBuilder(nameof(myQuery));

        QueryBuilder child = QueryBuilder.CreateDefaultBuilder(nameof(myQuery))
            .AddField("Alias:path.to.object", new Dictionary<string, object?>
            {
                { "first", new Variable("$take", "Int") },
                {"enums", new Dictionary<string, object?>
                    {
                        { "raw", HttpStatusCode.OK },
                        { "enumValue", new EnumValue(HttpStatusCode.OK) }
                    }
                },
                { "nested", new 
                    {
                        foo = "fooooo",
                        bar = "baaaaaar",
                        bazvar = new Variable("$bazvar", "Int")
                    }
                }
            })
            .AddField("path.to.object.edges.node.left", subFields: ["foo:bar","baz:qux"])
            .AddField("path.to.object.edges.node.right", subFields: ["foo:bar", "baz:qux"])
            .AddField("Alias:path.to.object", new Dictionary<string, object?>
            {
                { "second", new Variable("$take", "Int") },
                { "nested", new Dictionary<string, object>()
                    {
                        { "baz", "qux" }
                    }
                }
            });

        child.Variables.Should().ContainSingle(v => v.Name == "$take" && v.Type == "Int");
        child.Variables.Should().ContainSingle(v => v.Name == "$bazvar" && v.Type == "Int");
        
        var text = myQuery.Include(child).ToString();

        myQuery.Variables.Should().ContainSingle(v => v.Name == "$take" && v.Type == "Int");
        myQuery.Variables.Should().ContainSingle(v => v.Name == "$bazvar" && v.Type == "Int");
        
        text.Should().Be(@"query myQuery($bazvar:Int, $take:Int){
    Alias:path{
        to{
            object(enums:{enumValue:OK, raw:OK}, first:$take, nested:{bar:""baaaaaar"", baz:""qux"", bazvar:$bazvar, foo:""fooooo""}, second:$take){
                edges{
                    node{
                        left{
                            baz:qux
                            foo:bar
                        }
                        right{
                            baz:qux
                            foo:bar
                        }
                    }
                }
            }
        }
    }
}");
    }
}
