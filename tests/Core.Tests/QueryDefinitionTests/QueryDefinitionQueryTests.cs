using System;
using System.Collections.Generic;
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
                            _children = new FieldChildren
                            {
                                new FieldDefinition("child")
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
                            _children = new FieldChildren
                            {
                                new FieldDefinition("child1")
                                {
                                    _children = new FieldChildren
                                    {
                                        new FieldDefinition("grandchild1")
                                    }
                                },
                                new FieldDefinition("child2")
                                {
                                    _children = new FieldChildren
                                    {
                                        new FieldDefinition("grandchild2")
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
                            _children = new FieldChildren
                            {
                                new FieldDefinition("child")
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
                            _children = new FieldChildren
                            {
                                new FieldDefinition("level2")
                                {
                                    _children = new FieldChildren
                                    {
                                        new FieldDefinition("level3")
                                        {
                                            _children = new FieldChildren
                                            {
                                                new FieldDefinition("level4")
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
                            _children = new FieldChildren
                            {
                                new FieldDefinition("child1"),
                                new FieldDefinition("child2"),
                                new FieldDefinition("child3")
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
                            _children = new FieldChildren
                            {
                                new FieldDefinition("child1"),
                                new FieldDefinition("child2")
                            }
                        }
                    },
                    {
                        "parent2", new FieldDefinition("parent2")
                        {
                            _children = new FieldChildren
                            {
                                new FieldDefinition("child")
                                {
                                    _children = new FieldChildren
                                    {
                                        new FieldDefinition("grandchild")
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

    [Fact]
    public void Fields_Property_Internal_Setter_Via_Reflection()
    {
        // Arrange
        var query = new QueryDefinition("TestQuery");
        var newFields = new Dictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            { "field1", new FieldDefinition("field1") }
        };
        
        // Act - Use reflection to invoke the internal setter
        var fieldsProperty = query.GetType().GetProperty("Fields");
        var setMethod = fieldsProperty?.GetSetMethod(true);
        
        // Assert
        if (setMethod != null)
        {
            setMethod.Invoke(query, [newFields]);
            query.Fields.Should().HaveCount(1);
            query.Fields.Should().ContainKey("field1");
        }
    }
}
