using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Extensions;

/// <summary>
/// Targets the merge-compatibility, deep-fingerprint, and fragment-merge branches of
/// <see cref="FieldDefinitionExtensions"/> that the behavioural suite leaves unreached.
/// </summary>
public class FieldDefinitionExtensionsCoverageTests
{
    [Fact]
    public void CanMergeFields_WhenOnlyExistingHasIncludeDirective_ReturnsFalse()
    {
        var existing = new FieldDefinition("user", "Object");
        existing.AddDirective(new FieldDirective("include", new Dictionary<string, object?> { ["if"] = "$show" }));
        var incoming = new FieldDefinition("user", "Object");

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanMergeFields_WhenOnlyIncomingHasSkipDirective_ReturnsFalse()
    {
        var existing = new FieldDefinition("user", "Object");
        var incoming = new FieldDefinition("user", "Object");
        incoming.AddDirective(new FieldDirective("skip", new Dictionary<string, object?> { ["if"] = "$hide" }));

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanMergeFields_WhenExistingExtraChildHasArgumentsAndIncomingHasNoChildren_ReturnsFalse()
    {
        var existing = new FieldDefinition("user", "Object");
        var child = new FieldDefinition("posts", "Object", null, new SortedDictionary<string, object?> { ["first"] = 10 });
        existing._children = new FieldChildren();
        existing._children.Append(child);

        var incoming = new FieldDefinition("user", "Object");
        incoming._children = new FieldChildren();

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanMergeFields_WhenIncomingHasNoChildCollectionAndExistingChildHasArguments_ReturnsFalse()
    {
        var existing = new FieldDefinition("user", "Object");
        existing._children = new FieldChildren();
        existing._children.Append(new FieldDefinition("posts", "Object", null, new SortedDictionary<string, object?> { ["first"] = 10 }));
        existing._children.Append(new FieldDefinition("name", "String"));

        var incoming = new FieldDefinition("user", "Object", null, new SortedDictionary<string, object?>());

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanMergeFields_WhenIncomingChildOnlyDiffersByConditionalDirective_ReturnsFalse()
    {
        var existing = new FieldDefinition("user", "Object", null, new SortedDictionary<string, object?> { ["id"] = 1 });

        var incoming = new FieldDefinition("user", "Object", null, new SortedDictionary<string, object?> { ["id"] = 1 });
        var branch = new FieldDefinition("profile", "Object");
        var conditional = new FieldDefinition("email", "String");
        conditional.AddDirective(new FieldDirective("include", new Dictionary<string, object?> { ["if"] = "$show" }));
        branch._children = new FieldChildren();
        branch._children.Append(conditional);
        incoming._children = new FieldChildren();
        incoming._children.Append(branch);

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanMergeFields_WhenExistingExtraArgumentChildIsMissingFromIncoming_ReturnsFalse()
    {
        var existing = new FieldDefinition("user", "Object");
        existing._children = new FieldChildren();
        existing._children.Append(new FieldDefinition("posts", "Object", null, new SortedDictionary<string, object?> { ["first"] = 10 }));

        var incoming = new FieldDefinition("user", "Object");
        incoming._children = new FieldChildren();
        incoming._children.Append(new FieldDefinition("name", "String"));

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanMergeFields_WhenExistingArgumentChildIsAlsoPresentOnIncoming_ReturnsTrue()
    {
        var existing = new FieldDefinition("user", "Object");
        existing._children = new FieldChildren();
        existing._children.Append(new FieldDefinition("posts", "Object", null, new SortedDictionary<string, object?> { ["first"] = 10 }));

        var incoming = new FieldDefinition("user", "Object");
        incoming._children = new FieldChildren();
        incoming._children.Append(new FieldDefinition("posts", "Object", null, new SortedDictionary<string, object?> { ["first"] = 10 }));

        var result = FieldDefinitionExtensions.CanMergeFields(existing, incoming);

        result.Should().BeTrue();
    }

    [Fact]
    public void ComputeDeepFingerprint_WhenFieldHasOnlyCustomDirective_MatchesDirectiveFreeField()
    {
        var withCustom = new FieldDefinition("user", "Object", null, new SortedDictionary<string, object?> { ["id"] = 1 });
        withCustom.AddDirective(new FieldDirective("deprecated", null));
        var without = new FieldDefinition("user", "Object", null, new SortedDictionary<string, object?> { ["id"] = 1 });

        var result = FieldDefinitionExtensions.ComputeDeepFingerprint(withCustom);

        result.Should().Be(FieldDefinitionExtensions.ComputeDeepFingerprint(without));
    }

    [Fact]
    public void ComputeDeepFingerprint_WhenOnlyDescendantCarriesArguments_DiffersFromArgumentFreeTwin()
    {
        var significant = new FieldDefinition("user", "Object");
        significant._children = new FieldChildren();
        var branch = new FieldDefinition("profile", "Object");
        branch._children = new FieldChildren();
        branch._children.Append(new FieldDefinition("posts", "Object", null, new SortedDictionary<string, object?> { ["first"] = 5 }));
        significant._children.Append(branch);

        var plain = new FieldDefinition("user", "Object");
        plain._children = new FieldChildren();
        plain._children.Append(new FieldDefinition("profile", "Object"));

        var result = FieldDefinitionExtensions.ComputeDeepFingerprint(significant);

        result.Should().NotBe(FieldDefinitionExtensions.ComputeDeepFingerprint(plain));
    }

    [Fact]
    public void MergeInlineFragmentsInto_WhenTargetIsNull_CreatesMapAndClonesIncoming()
    {
        var incomingFragment = new InlineFragmentDefinition("User");
        incomingFragment.GetOrCreateFieldsStore().Append(new FieldDefinition("name", "String"));
        var incoming = new Dictionary<string, InlineFragmentDefinition> { ["User"] = incomingFragment };
        Dictionary<string, InlineFragmentDefinition>? target = null;

        FieldDefinitionExtensions.MergeInlineFragmentsInto(ref target, incoming);

        target.Should().ContainKey("User");
        target!["User"].Should().NotBeSameAs(incomingFragment);
        target["User"].Fields.Should().ContainKey("name");
    }

    [Fact]
    public void MergeInlineFragmentsInto_WhenExistingFragmentHasNoStores_AdoptsIncomingFieldsAndNestedFragments()
    {
        var existingFragment = new InlineFragmentDefinition("User");
        Dictionary<string, InlineFragmentDefinition>? target =
            new(StringComparer.Ordinal) { ["User"] = existingFragment };

        var incomingFragment = new InlineFragmentDefinition("User");
        incomingFragment.GetOrCreateFieldsStore().Append(new FieldDefinition("name", "String"));
        var nested = new InlineFragmentDefinition("Member");
        nested.GetOrCreateFieldsStore().Append(new FieldDefinition("since", "String"));
        incomingFragment._fragments = new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal) { ["Member"] = nested };
        var incoming = new Dictionary<string, InlineFragmentDefinition> { ["User"] = incomingFragment };

        FieldDefinitionExtensions.MergeInlineFragmentsInto(ref target, incoming);

        target!["User"].Should().BeSameAs(existingFragment);
        existingFragment.Fields.Should().ContainKey("name");
        existingFragment.InlineFragments.Should().ContainKey("Member");
        existingFragment.InlineFragments["Member"].Should().NotBeSameAs(nested);
    }

    [Fact]
    public void Include_WhenNamedFragmentsShareTypeInlineFragment_MergesFragmentBodies()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddFragment("Parts", "Node", f => f.OnType("User", u => u.AddField("name")));
        var incoming = QueryBuilder.CreateDefaultBuilder("Incoming")
            .AddFragment("Parts", "Node", f => f.OnType("User", u => u.AddField("email")));

        var result = target.Include(incoming).ToString();

        result.Should().Contain("name").And.Contain("email");
        result.Should().Contain("... on User");
    }

    [Fact]
    public void Include_WhenInlineFragmentMergeIntroducesNewArgument_KeepsSubtreeMergeSignificant()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("node", n => n.OnType("User", u => u.AddField("posts", p => p.AddField("id"))));
        var incoming = QueryBuilder.CreateDefaultBuilder("Incoming")
            .AddField("node", n => n.OnType("User", u => u.AddField("posts", p => p.AddField("comments",
                new Dictionary<string, object?> { ["first"] = 10 }, c => c.AddField("body")))));

        target.Include(incoming);
        var merged = target.Definition.Fields["node"].InlineFragments["User"].Fields["posts"];

        FieldDefinitionExtensions.ComputeDeepFingerprint(merged)
            .Should().NotBe(FieldDefinitionExtensions.ComputeDeepFingerprint(new FieldDefinition("posts", "Object")));
        target.ToString().Should().Contain("first:10");
    }

    [Fact]
    public void Include_WhenInlineFragmentMergeAddsArgumentFreeChildren_KeepsTheArgumentBearingParentSignificant()
    {
        var arguments = new Dictionary<string, object?> { ["first"] = 10 };
        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("node", n => n.OnType("User", u => u.AddField("posts", arguments, p => p.AddField("id"))));
        var incoming = QueryBuilder.CreateDefaultBuilder("Incoming")
            .AddField("node", n => n.OnType("User", u => u.AddField("posts",
                new Dictionary<string, object?> { ["first"] = 10 }, p => p.AddField("title"))));

        target.Include(incoming);
        var merged = target.Definition.Fields["node"].InlineFragments["User"].Fields["posts"];

        FieldDefinitionExtensions.ComputeDeepFingerprint(merged)
            .Should().NotBe(FieldDefinitionExtensions.ComputeDeepFingerprint(new FieldDefinition("posts", "Object")));
        target.ToString().Should().Contain("id").And.Contain("title");
    }

    [Fact]
    public void Include_WhenNamedFragmentsCarryDistinctInlineFragments_KeepsBoth()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddFragment("Parts", "Node", f => f.OnType("User", u => u.AddField("name")));
        var incoming = QueryBuilder.CreateDefaultBuilder("Incoming")
            .AddFragment("Parts", "Node", f => f.OnType("Admin", a => a.AddField("role")));

        var result = target.Include(incoming).ToString();

        result.Should().Contain("... on User").And.Contain("... on Admin");
    }

    [Fact]
    public void Include_WhenFieldInlineFragmentsNestAnotherInlineFragment_MergesNestedBodies()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("node", n => n.OnType("User", u => u.OnType("Member", m => m.AddField("since"))));
        var incoming = QueryBuilder.CreateDefaultBuilder("Incoming")
            .AddField("node", n => n.OnType("User", u => u.OnType("Member", m => m.AddField("tier"))));

        var result = target.Include(incoming).ToString();

        result.Should().Contain("since").And.Contain("tier");
    }

    [Fact]
    public void Include_WhenFieldInlineFragmentsNestDistinctInlineFragments_KeepsBoth()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("node", n => n.OnType("User", u => u.OnType("Member", m => m.AddField("since"))));
        var incoming = QueryBuilder.CreateDefaultBuilder("Incoming")
            .AddField("node", n => n.OnType("User", u => u.OnType("Guest", g => g.AddField("visits"))));

        var result = target.Include(incoming).ToString();

        result.Should().Contain("... on Member").And.Contain("... on Guest");
    }
}
