using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression tests for the multi-parameter same-type disambiguation bug.
/// When a predicate has two parameters of the SAME type, each parameter's fields must stay
/// scoped to its own node. The extractor previously gated parameter-name prefixing on the
/// number of distinct parameter TYPES (a HashSet&lt;Type&gt;), so two same-typed parameters
/// collapsed to one and no prefix was emitted, causing fields to leak across sibling nodes.
/// The gate is now driven by the outermost lambda's parameter COUNT.
/// </summary>
public class MultiParamSameTypePrefixTests
{
    public sealed class Node
    {
        public string? A { get; set; }
        public string? B { get; set; }
    }

    [Fact]
    public void MultiParameter_SameType_DistinctNodes_DoesNotLeakFieldsAcrossSiblings()
    {
        // Arrange - two sibling roots that stay distinct (NeverMerge), each exposing both A and B.
        var queryX = QueryBuilder
            .CreateDefaultBuilder("QueryX")
            .AddField("QueryX:data.edges.node.A:a")
            .AddField("data.edges.node.B:b");

        var queryY = QueryBuilder
            .CreateDefaultBuilder("QueryY")
            .AddField("QueryY:data.edges.node.A:a")
            .AddField("data.edges.node.B:b");

        var merged = QueryBuilder
            .CreateDefaultBuilder("Merged", MergingStrategy.NeverMerge)
            .Include(queryX)
            .Include(queryY);

        var localMap = new Dictionary<string, string[]>
        {
            { "x", ["QueryX"] },
            { "y", ["QueryY"] }
        };

        // Act - both parameters are the SAME type; x references only A, y references only B.
        Expression<Func<Node, Node, bool>> expr =
            (x, y) => x.A != null && y.B != null;

        var result = PreservationBuilder.Create(merged)
            .PreserveFromExpression((Expression)expr, "edges.node", localMap)
            .Build();

        // Assert - x's node keeps only A, y's node keeps only B. No cross-leak.
        var rendered = result.ToString();
        rendered.Should().Contain("QueryX");
        rendered.Should().Contain("QueryY");

        var xBlock = ExtractNodeBlock(rendered, "QueryX");
        var yBlock = ExtractNodeBlock(rendered, "QueryY");

        xBlock.Should().Contain("A:a");
        xBlock.Should().NotContain("B:b");

        yBlock.Should().Contain("B:b");
        yBlock.Should().NotContain("A:a");
    }

    [Fact]
    public void SingleParameter_Lambda_ProducesUnprefixedPaths()
    {
        // Arrange & Act - a single-parameter lambda must remain unprefixed.
        var paths = ExpressionFieldExtractor.ExtractFieldPaths<Node>(x => x.A != null && x.B != null);

        // Assert - bare field names, no parameter prefix.
        paths.Should().BeEquivalentTo("A", "B");
    }

    [Fact]
    public void MultiParameter_SameType_Extraction_PrefixesEachParameter()
    {
        // Arrange - two parameters of the same type, each touching a different field.
        Expression<Func<Node, Node, bool>> expr =
            (x, y) => x.A != null && y.B != null;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert - each path carries its owning parameter's name so downstream preservation
        // can route fields to the correct node.
        paths.Should().BeEquivalentTo("x.A", "y.B");
    }

    // Isolates the rendered sub-tree belonging to a given root alias so we can assert which
    // fields ended up under it without matching the sibling root.
    private static string ExtractNodeBlock(string rendered, string rootAlias)
    {
        var marker = rootAlias + ":data";
        var start = rendered.IndexOf(marker, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"rendered output should contain root '{rootAlias}'");

        var next = rendered.IndexOf(":data", start + marker.Length, StringComparison.Ordinal);
        return next >= 0 ? rendered.Substring(start, next - start) : rendered.Substring(start);
    }
}
