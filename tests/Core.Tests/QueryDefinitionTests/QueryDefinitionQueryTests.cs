using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.QueryDefinitionTests;

public class QueryDefinitionQueryTests
{
    [Fact]
    public void Simple_Nested_Fields_Syntax()
    {
        // Arrange
        var query = new QueryDefinition("SimpleQuery")
            {
                Fields =
                {
                    {
                        "parent", new FieldDefinition("parent")
                        {
                            Fields =
                            {
                                { "child", new FieldDefinition("child") }
                            }
                        }
                    }
                }
            };
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SimpleQuery{
    parent{
        child
    }
}");
    }
    
    [Fact]
    public void Multiple_Nested_Fields_Syntax()
    {
        // Arrange
        var query = new QueryDefinition("ComplexQuery")
            {
                Fields =
                {
                    {
                        "parent", new FieldDefinition("parent")
                        {
                            Fields =
                            {
                                {
                                    "child1", new FieldDefinition("child1")
                                    {
                                        Fields =
                                        {
                                            { "grandchild1", new FieldDefinition("grandchild1") }
                                        }
                                    }
                                },
                                {
                                    "child2", new FieldDefinition("child2")
                                    {
                                        Fields =
                                        {
                                            { "grandchild2", new FieldDefinition("grandchild2") }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        
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
        var query = new QueryDefinition("MultipleTopLevelQuery")
            {
                Fields =
                {
                    { "field1", new FieldDefinition("field1") },
                    { "field2", new FieldDefinition("field2") }
                }
            };
        
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
        var query = new QueryDefinition("MixedQuery")
            {
                Fields =
                {
                    { "field1", new FieldDefinition("field1") },
                    {
                        "parent", new FieldDefinition("parent")
                        {
                            Fields =
                            {
                                { "child", new FieldDefinition("child") }
                            }
                        }
                    }
                }
            };
        
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
        var query = new QueryDefinition("DeepQuery")
            {
                Fields =
                {
                    {
                        "level1", new FieldDefinition("level1")
                        {
                            Fields =
                            {
                                {
                                    "level2", new FieldDefinition("level2")
                                    {
                                        Fields =
                                        {
                                            {
                                                "level3", new FieldDefinition("level3")
                                                {
                                                    Fields =
                                                    {
                                                        { "level4", new FieldDefinition("level4") }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
                                                            
        
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
        var query = new QueryDefinition("SiblingsQuery")
            {
                Fields =
                {
                    {
                        "parent", new FieldDefinition("parent")
                        {
                            Fields =
                            {
                                { "child1", new FieldDefinition("child1") },
                                { "child2", new FieldDefinition("child2") },
                                { "child3", new FieldDefinition("child3") }
                            }
                        }
                    }
                }
            };
        
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
        var query = new QueryDefinition("ComplexCombinationQuery")
            {
                Fields =
                {
                    { "field1", new FieldDefinition("field1") },
                    {
                        "parent1", new FieldDefinition("parent1")
                        {
                            Fields =
                            {
                                { "child1", new FieldDefinition("child1") },
                                { "child2", new FieldDefinition("child2") }
                            }
                        }
                    },
                    {
                        "parent2", new FieldDefinition("parent2")
                        {
                            Fields =
                            {
                                {
                                    "child", new FieldDefinition("child")
                                    {
                                        Fields =
                                        {
                                            { "grandchild", new FieldDefinition("grandchild") }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query ComplexCombinationQuery{
    field1
    parent1{
        child1
        child2
    }
    parent2{
        child{
            grandchild
        }
    }
}");
    }
}
