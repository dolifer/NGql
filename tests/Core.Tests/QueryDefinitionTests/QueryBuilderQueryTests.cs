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
            .New("SimpleQuery")
            .AddField(fieldName)
            .ToQuery();
    
        // Assert the final GraphQL query
        query.ToString().Should().Be(@"query SimpleQuery{
    parent
}");
    }
    
    [Fact]
    public void Simple_Nested_Fields_Syntax()
    {
        // Arrange
        var query = QueryBuilder
            .New("SimpleQuery")
            .AddField("parent.child")
            .ToQuery();
    
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
        var query = QueryBuilder
            .New("ComplexQuery")
            .AddField("parent.child1.grandchild1")
            .AddField("parent.child2.grandchild2")
            .ToQuery();
    
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
            .New("MultipleTopLevelQuery")
            .AddField("field1")
            .AddField("field2")
            .ToQuery();
    
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
            .New("MixedQuery")
            .AddField("field1")
            .AddField("parent.child")
            .ToQuery();
    
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
            .New("DeepQuery")
            .AddField("level1.level2.level3.level4")
            .ToQuery();
    
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
            .New("SiblingsQuery")
            .AddField("parent.child1")
            .AddField("parent.child2")
            .AddField("parent.child3")
            .ToQuery();
    
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
            .New("ComplexCombinationQuery")
            .AddField("field1")
            .AddField("parent1.child1")
            .AddField("parent1.child2")
            .AddField("parent2.child")
            .AddField("parent2.child.grandchild")
            .ToQuery();
    
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
