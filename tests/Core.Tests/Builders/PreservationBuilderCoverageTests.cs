using System;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class PreservationBuilderCoverageTests
{
    private static QueryBuilder SampleQuery()
        => QueryBuilder
            .CreateDefaultBuilder("Sample")
            .AddField("Sample:data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

    [Fact]
    public void Create_NullQuery_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => PreservationBuilder.Create(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("sourceQuery");
    }

    [Theory]
    [InlineData("", "edges.node", "Sample")]
    [InlineData("   ", "edges.node", "Sample")]
    [InlineData("email", "", "Sample")]
    [InlineData("email", "   ", "Sample")]
    [InlineData("email", "edges.node", "")]
    [InlineData("email", "edges.node", "   ")]
    public void PreserveAtPathInRoot_BlankArgument_IsNoOp(string fieldPath, string nodePath, string root)
    {
        // Arrange
        var query = SampleQuery();

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot(fieldPath, nodePath, root)
            .Build();

        // Assert
        result.Should().BeSameAs(query, "a blank argument must not preserve anything");
    }

    [Fact]
    public void PreserveAtPathInRoot_AllArgumentsSupplied_PreservesScopedField()
    {
        // Arrange
        var query = SampleQuery();

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("email", "edges.node", "Sample")
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("email");
        result.Should().NotContain("name");
    }

    [Fact]
    public void PreserveAtPath_NodePathMissingUnderRoot_IsNoOp()
    {
        // Arrange
        var query = SampleQuery();

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("email", "nowhere")
            .Build();

        // Assert
        result.Should().BeSameAs(query);
    }

    [Fact]
    public void PreserveAtPathInRoot_NodePathMissingUnderNamedRoot_IsNoOp()
    {
        // Arrange
        var query = SampleQuery();

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("email", "nowhere", "Sample")
            .Build();

        // Assert
        result.Should().BeSameAs(query);
    }

    [Fact]
    public void PreserveAtPathInRoot_UnknownRoot_IsNoOp()
    {
        // Arrange
        var query = SampleQuery();

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("email", "edges.node", "NoSuchRoot")
            .Build();

        // Assert
        result.Should().BeSameAs(query);
    }
}
