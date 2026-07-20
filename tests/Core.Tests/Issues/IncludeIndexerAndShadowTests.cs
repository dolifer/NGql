using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class IncludeIndexerAndShadowTests
{
    [Fact]
    public void Include_Object_WithIndexer_DoesNotThrow_And_OmitsItemField()
    {
        // Arrange
        var query = new Query("TestQuery");
        var obj = new IndexedModel();

        // Act
        var act = () => query.Include(obj);

        // Assert
        act.Should().NotThrow();
        var rendered = query.ToString();
        rendered.Should().Contain("Name");
        rendered.Should().NotContain("Item");
    }

    [Fact]
    public void Include_Type_WithIndexer_OmitsItemField()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query.Include<IndexedModel>("node");

        // Assert
        var rendered = query.ToString();
        rendered.Should().Contain("Name");
        rendered.Should().NotContain("Item");
    }

    [Fact]
    public void Include_Type_WithShadowedProperty_EmitsResponseNameOnce()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query.Include<DerivedModel>("node");

        // Assert
        var rendered = query.ToString();
        System.Text.RegularExpressions.Regex
            .Matches(rendered, @"\bValue\b")
            .Should().ContainSingle();
    }

    [Fact]
    public void Include_Object_WithShadowedProperty_EmitsResponseNameOnce()
    {
        // Arrange
        var query = new Query("TestQuery");
        var obj = new DerivedModel();

        // Act
        query.Include(obj);

        // Assert
        var rendered = query.ToString();
        System.Text.RegularExpressions.Regex
            .Matches(rendered, @"\bValue\b")
            .Should().ContainSingle();
    }

    [Fact]
    public void Include_Type_NormalType_RendersAllFieldsUnchanged()
    {
        // Arrange
        var query = new Query("TestQuery");

        // Act
        query.Include<PlainModel>("node");

        // Assert
        query.ToString().Should().Be(@"query TestQuery{
    node{
        Age
        Id
        Name
    }
}");
    }

    public class IndexedModel
    {
        public string Name { get; set; } = "n";

        public string this[int index] => "x";
    }

    public class BaseModel
    {
        public string Value { get; set; } = "base";
    }

    public class DerivedModel : BaseModel
    {
        public new string Value { get; set; } = "derived";
    }

    public class PlainModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = "n";

        public int Age { get; set; }
    }
}
