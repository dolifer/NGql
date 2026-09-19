using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class ExpressionPreservationCoverageTests
{
    public class Basket
    {
        public string? BasketId { get; set; }
        public List<Item> Items { get; set; } = new();
    }

    public class Item
    {
        public string? Sku { get; set; }
        public string? Label { get; set; }
    }

    public class Crate
    {
        public string? CrateId { get; set; }
        public List<Item> Items { get; set; } = new();
    }

    private static QueryBuilder MergedTwoRootQuery()
    {
        var left = QueryBuilder
            .CreateDefaultBuilder("Left")
            .AddField("Left:data.edges.node.BasketId:basketId")
            .AddField("data.edges.node.Sku:sku")
            .AddField("data.edges.node.Label:label");

        var right = QueryBuilder
            .CreateDefaultBuilder("Right")
            .AddField("Right:data.edges.node.CrateId:crateId")
            .AddField("data.edges.node.Sku:sku")
            .AddField("data.edges.node.Label:label");

        return QueryBuilder
            .CreateDefaultBuilder("Merged", MergingStrategy.MergeByFieldPath)
            .Include(left)
            .Include(right);
    }

    [Fact]
    public void PreserveFromExpression_MultipleParametersWithoutPrefixedPaths_ExpandsPerParameterType()
    {
        // Arrange: two parameters share one base path, but the only member chain in the body is
        // rooted at a CAPTURED collection rather than either parameter, so no extracted path
        // carries a root-parameter prefix and the per-parameter-type expansion branch runs.
        var query = MergedTwoRootQuery();
        var captured = new List<Item>();
        var localMap = new Dictionary<string, string[]>
        {
            { "basket", ["Left"] },
            { "crate", ["Left"] }
        };

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (Basket basket, Crate crate) => captured.Any(entry => entry.Sku != null),
                "edges.node",
                localMap)
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("sku");
    }

    [Fact]
    public void PreserveFromExpression_MultipleParametersWithMixedPrefixedPaths_FiltersPerParameter()
    {
        // Arrange: same two-parameter shape, but one extracted path IS parameter-prefixed, which
        // must flip the decision back to per-parameter filtering.
        var query = MergedTwoRootQuery();
        var captured = new List<Item>();
        var localMap = new Dictionary<string, string[]>
        {
            { "basket", ["Left"] },
            { "crate", ["Left"] }
        };

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (Basket basket, Crate crate) => captured.Any(entry => entry.Sku != null) && basket.BasketId != null,
                "edges.node",
                localMap)
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("basketId");
    }

    [Fact]
    public void PreserveFromExpression_SingleParameterWithoutPrefixedPaths_DoesNotExpandPerType()
    {
        // Arrange: only one parameter for the base path, so the per-type expansion branch is
        // skipped even though no extracted path carries a parameter prefix.
        var query = MergedTwoRootQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "basket", ["Left"] }
        };

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((Basket basket) => basket.Items.Any(entry => entry.Sku != null), "edges.node", localMap)
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("sku");
    }

    [Fact]
    public void PreserveFromExpression_MultipleParametersWithPrefixedPaths_FiltersPerParameter()
    {
        // Arrange: both parameters map to the same base path AND the body references them
        // directly, so the extracted paths ARE prefixed and per-parameter filtering wins.
        var query = MergedTwoRootQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "basket", ["Left"] },
            { "crate", ["Left"] }
        };

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (Basket basket, Crate crate) => basket.BasketId != null && crate.CrateId != null,
                "edges.node",
                localMap)
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("basketId");
    }

    [Fact]
    public void PreserveFromExpression_BareParameterReferenceWithoutName_KeepsPathUnchanged()
    {
        // Arrange: the sole extracted path equals the parameter name itself, exercising the
        // bare-parameter-reference detection for both the named and unnamed cases.
        var query = QueryBuilder
            .CreateDefaultBuilder("Bare")
            .AddField("Bare:data.edges.node.Sku:sku")
            .AddField("data.edges.node.Label:label");

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((Item item) => item == null, "edges.node")
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("sku");
        result.Should().Contain("label");
    }

    [Fact]
    public void PreserveFromExpression_MultipleParametersWithBareRootReference_PreservesWholeParameter()
    {
        // Arrange: the body references the first parameter bare, so the extracted path is the
        // parameter NAME itself rather than a prefixed chain — the exact-name match inside
        // per-parameter path selection.
        var query = MergedTwoRootQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "basket", ["Left"] },
            { "crate", ["Left"] }
        };

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (Basket basket, Crate crate) => basket == null && crate.CrateId != null,
                "edges.node",
                localMap)
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("crateId");
    }

    [Fact]
    public void PreserveFromExpression_NodePathMissingUnderOneRoot_SkipsThatRoot()
    {
        // Arrange: two independent roots; nodePath resolves under one of them only.
        var withNode = QueryBuilder
            .CreateDefaultBuilder("WithNode")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("WithNode:data.edges.node.Sku:sku")
            .AddField("data.edges.node.Label:label");

        var withoutNode = QueryBuilder
            .CreateDefaultBuilder("WithoutNode")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("WithoutNode:other.leaf");

        var query = QueryBuilder
            .CreateDefaultBuilder("Combined", MergingStrategy.NeverMerge)
            .Include(withNode)
            .Include(withoutNode);

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((Item item) => item.Sku != null, "edges.node")
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("sku");
    }

    [Fact]
    public void PreserveFromExpression_NodePathResolvesToLeafField_PreservesNothingForThatRoot()
    {
        // Arrange: "node" exists but is a leaf (no sub-fields), so the HasFields guard trips.
        var query = QueryBuilder
            .CreateDefaultBuilder("LeafNode")
            .AddField("LeafNode:data.edges.node")
            .AddField("data.other.keep");

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((Item item) => item.Sku != null, "edges.node")
            .Build();

        // Assert
        result.Should().BeSameAs(query);
    }

    [Fact]
    public void PreserveFromExpression_MissingParameterTypeInLocalMap_StillPreservesMappedParameters()
    {
        // Arrange: localMap names a parameter the lambda does not declare, so that key contributes
        // a base path with no parameters, and alwaysPreserveFields forces it to be processed.
        var query = MergedTwoRootQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "basket", ["Left"] },
            { "absent", ["Right"] }
        };

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (Basket basket) => basket.BasketId != null,
                "edges.node",
                localMap,
                "Sku")
            .Build()
            .ToString();

        // Assert
        result.Should().Contain("sku");
    }
}
