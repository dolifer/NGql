using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FieldDefinitionOptionalInitTests
{
    [Fact]
    public void MetadataInit_OnFieldWithoutOptionalState_AttachesOnlyMetadata()
    {
        var field = new FieldDefinition("user", "User") { _metadata = new Dictionary<string, object?> { ["k"] = 1 } };

        field.HasMetadata.Should().BeTrue();
        field.HasDirectives.Should().BeFalse();
        field.SpreadFragments.Should().BeEmpty();
        field.InlineFragments.Should().BeEmpty();
    }

    [Fact]
    public void MetadataInit_OnCopyOfFieldWithEveryOptionalMember_PreservesOtherMembers()
    {
        var source = new FieldDefinition("user", "User");
        source.AddDirective(new FieldDirective("live", null));
        source.AddSpreadFragment("Identity");
        source.GetOrAddInlineFragment("Admin");

        var copy = source with { _metadata = new Dictionary<string, object?> { ["k"] = 1 } };

        copy.Metadata.Should().ContainKey("k");
        copy.Directives.Should().ContainSingle();
        copy.SpreadFragments.Should().ContainSingle();
        copy.InlineFragments.Should().ContainKey("Admin");
    }

    [Fact]
    public void MetadataInit_OnCopyOfFieldWithExistingMetadata_ReplacesMetadata()
    {
        var source = new FieldDefinition("user", "User");
        source.Metadata["old"] = 1;
        source.AddDirective(new FieldDirective("live", null));

        var copy = source with { _metadata = new Dictionary<string, object?> { ["new"] = 2 } };

        copy.Metadata.Should().ContainKey("new").And.NotContainKey("old");
        copy.Directives.Should().ContainSingle();
    }

    [Fact]
    public void Metadata_ConcurrentFirstReads_AttachOneSharedDictionary()
    {
        var workers = Math.Max(4, Environment.ProcessorCount);

        for (var iteration = 0; iteration < 3000; iteration++)
        {
            var field = new FieldDefinition("user", "User");
            var attached = new Dictionary<string, object?>[workers];
            using var gate = new Barrier(workers);

            Parallel.For(0, workers, i =>
            {
                gate.SignalAndWait();
                attached[i] = field.Metadata;
            });

            attached.Distinct().Should().ContainSingle();
            attached[0].Should().BeSameAs(field.Metadata);
        }
    }

    [Fact]
    public void Metadata_ReadRacingDirectiveAndFragmentWrites_KeepsEveryMember()
    {
        for (var iteration = 0; iteration < 3000; iteration++)
        {
            var field = new FieldDefinition("user", "User");
            using var gate = new Barrier(4);

            Parallel.Invoke(
                () => { gate.SignalAndWait(); field.Metadata["key"] = 1; },
                () => { gate.SignalAndWait(); field.AddDirective(new FieldDirective("live", null)); },
                () => { gate.SignalAndWait(); field.AddSpreadFragment("Identity"); },
                () => { gate.SignalAndWait(); field.GetOrAddInlineFragment("Admin"); });

            field.Metadata.Should().ContainKey("key");
            field.Directives.Should().ContainSingle();
            field.SpreadFragments.Should().ContainSingle();
            field.InlineFragments.Should().ContainKey("Admin");
        }
    }
}
