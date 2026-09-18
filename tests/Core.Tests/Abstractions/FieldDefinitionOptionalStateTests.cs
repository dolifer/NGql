using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FieldDefinitionOptionalStateTests
{
    [Theory]
    [InlineData("Directive")]
    [InlineData("Spread")]
    [InlineData("InlineFragment")]
    [InlineData("Metadata")]
    public void RecordCopy_GainingOptionalState_LeavesSourceWithoutIt(string scenario)
    {
        var source = new FieldDefinition("user", "User");
        var copy = source with { Alias = "other" };

        Mutate(copy, scenario);

        HasAnyOptionalState(copy).Should().BeTrue();
        HasAnyOptionalState(source).Should().BeFalse();
    }

    [Theory]
    [InlineData("InlineFragment")]
    [InlineData("Metadata")]
    public void RecordCopy_GainingSecondOptionalState_KeepsSourceStateUnchanged(string scenario)
    {
        var source = new FieldDefinition("user", "User");
        source.AddDirective(new FieldDirective("first", null));
        source.AddSpreadFragment("First");
        var copy = source with { Alias = "other" };

        Mutate(copy, scenario);
        copy.Metadata = new Dictionary<string, object?> { ["replaced"] = true };

        source.HasMetadata.Should().BeFalse();
        source.HasInlineFragments.Should().BeFalse();
        source.Directives.Should().ContainSingle();
        source.SpreadFragments.Should().ContainSingle();
        copy.Directives.Should().ContainSingle();
        copy.Metadata.Should().ContainKey("replaced");
    }

    [Fact]
    public void Metadata_ReadOnFieldWithoutMetadata_AttachesMutableDictionaryOnce()
    {
        var field = new FieldDefinition("user", "User");

        var first = field.Metadata;
        first["key"] = 1;

        field.Metadata.Should().BeSameAs(first);
        field.HasMetadata.Should().BeTrue();
        field.HasDirectives.Should().BeFalse();
        field.HasInlineFragments.Should().BeFalse();
    }

    [Fact]
    public void Metadata_ReadRacingOtherOptionalState_KeepsEveryMember()
    {
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var field = new FieldDefinition("user", "User");

            Parallel.Invoke(
                () => field.Metadata["key"] = 1,
                () => field.AddDirective(new FieldDirective("live", null)),
                () => field.AddSpreadFragment("Identity"),
                () => field.GetOrAddInlineFragment("Admin"));

            field.Metadata.Should().ContainKey("key");
            field.Directives.Should().ContainSingle();
            field.SpreadFragments.Should().ContainSingle();
            field.InlineFragments.Should().ContainKey("Admin");
        }
    }

    [Fact]
    public void DeepClone_FieldWithEveryOptionalMember_CopiesIndependentCollections()
    {
        var source = new FieldDefinition("user", "User");
        source.Metadata["key"] = 1;
        source.AddDirective(new FieldDirective("live", null));
        source.AddSpreadFragment("Identity");
        source.GetOrAddInlineFragment("Admin");

        var clone = source.DeepClone();
        clone.Metadata["added"] = 2;
        clone.AddSpreadFragment("Other");
        clone.GetOrAddInlineFragment("Guest");

        source.Metadata.Should().ContainSingle();
        source.SpreadFragments.Should().ContainSingle();
        source.InlineFragments.Should().ContainSingle();
        clone.Directives.Should().ContainSingle();
        clone.Metadata.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(null, "user")]
    [InlineData("", "user")]
    [InlineData("u", "u")]
    public void EffectiveName_FollowsAliasAcrossRecordCopies(string? alias, string expected)
    {
        var source = new FieldDefinition("user", "User", "original");

        var copy = source with { Alias = alias };

        copy._effectiveName.Should().Be(expected);
        source._effectiveName.Should().Be("original");
    }

    private static void Mutate(FieldDefinition field, string scenario)
    {
        switch (scenario)
        {
            case "Directive":
                field.AddDirective(new FieldDirective("live", null));
                break;
            case "Spread":
                field.AddSpreadFragment("Identity");
                break;
            case "InlineFragment":
                field.GetOrAddInlineFragment("Admin");
                break;
            default:
                field.Metadata["source"] = "test";
                break;
        }
    }

    private static bool HasAnyOptionalState(FieldDefinition field)
        => field.HasDirectives || field.SpreadFragments.Count > 0 || field.HasInlineFragments || field.HasMetadata;
}
