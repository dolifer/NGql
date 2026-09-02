using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Exceptions;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class ChildlessObjectFieldMergeTests
{
    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void ExistingChildlessObjectField_MergesIncomingChildren_DoesNotThrow(MergingStrategy strategy)
    {
        // Arrange — "user" declared with an explicit "Object" type and no sub-fields (a true leaf
        // that never allocated a children store), merged with an incoming "user.name" whose "user"
        // is object-typed AND already carries a child. Both sides render the same field type
        // ("Object"), so CanMergeFields already treats this as a legitimate merge — reaching
        // MergeChildInPlace with existing._children still null must not throw.
        var existingLeaf = CreateDefaultBuilder("A", strategy).AddField("user", "Object");
        var incomingWithChild = CreateDefaultBuilder("B", strategy).AddField("user.name");

        // Act
        var root = CreateDefaultBuilder("R", strategy)
            .Include(existingLeaf)
            .Include(incomingWithChild);

        // Assert
        root.ToString().Should().Be("query R{\n    user{\n        name\n    }\n}");
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void MergeIsOrderIndependent_RegardlessOfWhichSideHasChildren(MergingStrategy strategy)
    {
        // Arrange/Act — merging in the opposite order (children-bearing field included first) must
        // produce the identical rendered result as the childless-first order above.
        var incomingWithChild = CreateDefaultBuilder("B", strategy).AddField("user.name");
        var existingLeaf = CreateDefaultBuilder("A", strategy).AddField("user", "Object");

        var root = CreateDefaultBuilder("R", strategy)
            .Include(incomingWithChild)
            .Include(existingLeaf);

        // Assert
        root.ToString().Should().Be("query R{\n    user{\n        name\n    }\n}");
    }

    [Fact]
    public void GenuineTypeConflict_StillThrows_NotMaskedByChildAllocationFix()
    {
        // Arrange — "user" as "Object" vs "user" as "String": a real, unrelated type conflict.
        // Must still raise QueryMergeException, proving the null-children fix does not swallow it.
        var objectField = CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath).AddField("user", "Object");
        var stringField = CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath).AddField("user", "String");

        // Act
        var act = () => CreateDefaultBuilder("R", MergingStrategy.MergeByFieldPath)
            .Include(objectField)
            .Include(stringField);

        // Assert
        act.Should().Throw<QueryMergeException>()
            .WithMessage("*type conflicts in field 'user'*");
    }

    [Fact]
    public async Task ExistingChildlessObjectField_MergesIncomingChildren_Snapshot()
    {
        // Arrange — the exact reported repro, captured as a Verify() snapshot.
        var existingLeaf = CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath).AddField("user", "Object");
        var incomingWithChild = CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath).AddField("user.name");

        // Act
        var root = CreateDefaultBuilder("R", MergingStrategy.MergeByFieldPath)
            .Include(existingLeaf)
            .Include(incomingWithChild);

        // Assert
        await root.Verify();
    }
}
