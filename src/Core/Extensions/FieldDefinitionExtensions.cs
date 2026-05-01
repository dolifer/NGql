using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Exceptions;
using NGql.Core.Features;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods and utilities for FieldDefinition operations.
/// </summary>
internal static class FieldDefinitionExtensions
{
    /// <summary>
    /// Determines if two fields can be merged based on their structure and arguments.
    /// The recursion is bounded by the field tree's depth — the public API does not allow
    /// constructing cyclic trees (FieldDefinition.Fields is get-only, _children is internal),
    /// so the recursion always terminates. A library bug introducing a cycle would surface as
    /// a StackOverflowException, which is louder and more actionable than a swallowed throw.
    /// </summary>
    internal static bool CanMergeFields(FieldDefinition existingField, FieldDefinition incomingField)
    {
        if (!Helpers.AreArgumentsEqual(existingField._arguments, incomingField._arguments))
            return false;
        return AreNestedFieldsCompatible(existingField, incomingField);
    }

    private static bool AreNestedFieldsCompatible(FieldDefinition existingField, FieldDefinition incomingField)
        => IncomingChildrenCompatible(existingField._children, incomingField._children)
        && ExistingExtrasCompatible(existingField, incomingField._children);

    private static bool IncomingChildrenCompatible(FieldChildren? existingChildren, FieldChildren? incomingChildren)
    {
        if (incomingChildren is not { Count: > 0 }) return true;

        var span = incomingChildren.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            if (!IsIncomingChildCompatible(existingChildren, span[i]))
                return false;
        }
        return true;
    }

    private static bool IsIncomingChildCompatible(FieldChildren? existingChildren, FieldDefinition incomingChild)
    {
        if (existingChildren is null) return !HasAnyArguments(incomingChild);
        if (!existingChildren.TryGetValue(incomingChild.Name, out var existingChild)) return !HasAnyArguments(incomingChild);
        return Helpers.AreArgumentsEqual(existingChild!._arguments, incomingChild._arguments)
            && AreNestedFieldsCompatible(existingChild, incomingChild);
    }

    // Skip the existing-not-in-incoming check entirely when nothing in the existing subtree
    // could ever fail it — early-out when no field in the existing subtree carries arguments.
    private static bool ExistingExtrasCompatible(FieldDefinition existingField, FieldChildren? incomingChildren)
    {
        var existingChildren = existingField._children;
        if (existingChildren is not { Count: > 0 } || !SubtreeHasAnyArguments(existingField))
            return true;

        var span = existingChildren.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            var existingChild = span[i];
            if (!IsExistingExtraCompatible(existingChild, incomingChildren))
                return false;
        }
        return true;
    }

    private static bool IsExistingExtraCompatible(FieldDefinition existingChild, FieldChildren? incomingChildren)
    {
        if (incomingChildren is null) return !HasAnyArguments(existingChild);
        return incomingChildren.Find(existingChild.Name) is not null || !HasAnyArguments(existingChild);
    }

    private static bool SubtreeHasAnyArguments(FieldDefinition field)
    {
        if (field._subtreeHasAnyArguments is { } cached) return cached;

        var result = field._arguments is { Count: > 0 } || AnyChildHasArguments(field._children);
        field._subtreeHasAnyArguments = result;
        return result;
    }

    private static bool AnyChildHasArguments(FieldChildren? children)
    {
        if (children is null || children.Count == 0) return false;
        var span = children.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            if (SubtreeHasAnyArguments(span[i])) return true;
        }
        return false;
    }

    private static bool HasAnyArguments(FieldDefinition field)
    {
        if (field._arguments is { Count: > 0 }) return true;
        if (field._children is null) return false;
        foreach (var child in field._children.AsSpan())
        {
            if (HasAnyArguments(child)) return true;
        }
        return false;
    }

    /// <summary>
    /// Deep-clones <paramref name="source"/> producing a new <see cref="FieldDefinition"/> that
    /// shares no mutable state with the original. Used by <see cref="QueryMerger"/> when adding an
    /// incoming field reference into the target dictionary, so subsequent in-place merges into the
    /// target do not leak back into the source builder.
    /// </summary>
    internal static FieldDefinition DeepClone(this FieldDefinition source)
    {
        var clone = new FieldDefinition(
            source.Name,
            source._type!,
            source._alias,
            source._arguments is null ? null : new SortedDictionary<string, object?>(source._arguments, StringComparer.OrdinalIgnoreCase))
        {
            Path = source.Path,
            IsNeverMerge = source.IsNeverMerge,
        };

        if (source._metadata is { Count: > 0 })
        {
            foreach (var kvp in source._metadata) clone.Metadata[kvp.Key] = kvp.Value;
        }

        if (source._children is { Count: > 0 })
        {
            clone._children = new FieldChildren();
            foreach (var child in source._children.AsSpan())
                clone._children.Append(child.DeepClone());
        }

        return clone;
    }

    /// <summary>
    /// Merges <paramref name="incoming"/> INTO <paramref name="existing"/>, mutating its child collection
    /// and argument dictionary in place. The caller must own <paramref name="existing"/> exclusively
    /// (no external aliases) — used by <see cref="QueryMerger"/> when the result will overwrite the
    /// dictionary entry that holds <paramref name="existing"/>.
    /// </summary>
    /// <returns><paramref name="existing"/> after mutation.</returns>
    internal static FieldDefinition MergeFieldsInPlace(FieldDefinition existing, FieldDefinition incoming)
    {
        ThrowIfTypesConflict(existing, incoming);
        MergeIncomingChildrenInPlace(existing, incoming._children);
        MergeIncomingArgumentsInPlace(existing, incoming._arguments);
        return existing;
    }

    private static void ThrowIfTypesConflict(FieldDefinition existing, FieldDefinition incoming)
    {
        // _type defaults to Constants.DefaultFieldType in every public constructor and is
        // never null on the merge path through Include — the field is always touched by
        // QueryBuilder.AddField which goes through FieldFactory.CreateFieldDefinition.
        if (existing._type!.AsSpan().Equals(incoming._type!.AsSpan(), StringComparison.OrdinalIgnoreCase)) return;
        throw new QueryMergeException($"Type conflict: existing field has type '{existing._type}', incoming field has type '{incoming._type}'");
    }

    private static void MergeIncomingChildrenInPlace(FieldDefinition existing, FieldChildren? incomingChildren)
    {
        if (incomingChildren is null) return;

        // Existing._children is non-null on every path that reaches here through the public
        // Include API: type-compatibility means both sides are object-typed, and object-typed
        // FieldDefinitions get a children collection from QueryBuilder.AddField at construction
        // time. Trust the invariant.
        var existingChildren = existing._children!;
        var span = incomingChildren.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            MergeChildInPlace(existingChildren, span[i]);
        }
        existing._subtreeHasAnyArguments = null;
    }

    private static void MergeIncomingArgumentsInPlace(FieldDefinition existing, SortedDictionary<string, object?>? incomingArguments)
    {
        if (incomingArguments is not { Count: > 0 }) return;
        existing.MergeFieldArgumentsInPlace(incomingArguments);
        existing._subtreeHasAnyArguments = null;
    }

    private static void MergeChildInPlace(FieldChildren existingChildren, FieldDefinition incomingChild)
    {
        if (existingChildren.TryGetValue(incomingChild.Name, out var existingNested) && existingNested != null)
        {
            MergeFieldsInPlace(existingNested, incomingChild);
            return;
        }

        // Deep-clone so target's tree never references nodes owned by the source builder. Any
        // later in-place merges into existingChildren must not propagate back to the source.
        var clone = incomingChild.DeepClone();

        // Effective-name conflict can only occur when the incoming field carries an alias
        // distinct from its name; otherwise the Name lookup above would have caught it.
        if (!ReferenceEquals(incomingChild._effectiveName, incomingChild.Name) &&
            HasEffectiveNameConflict(existingChildren, incomingChild._effectiveName))
        {
            var uniqueAlias = KeyGenerator.GenerateUniqueKey(incomingChild._effectiveName, existingChildren.AsSpan());
            existingChildren.Append(clone with { Alias = uniqueAlias });
        }
        else
        {
            existingChildren.Append(clone);
        }
    }

    private static bool HasEffectiveNameConflict(FieldChildren children, string effectiveName)
    {
        foreach (var f in children.AsSpan())
        {
            if (string.Equals(f._effectiveName, effectiveName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Merges <paramref name="newArguments"/> into <paramref name="existingField"/>'s argument
    /// dictionary in place. Caller (MergeFieldsInPlace via CanMergeFields) guarantees that
    /// <c>existingField._arguments</c> already has the same keys as <paramref name="newArguments"/>;
    /// the merge here only refines values for keys that hold nested dictionaries.
    /// </summary>
    // Callers (MergeFieldsInPlace via CanMergeFields) guarantee newArguments has Count > 0
    // and existingField._arguments is non-null with the same keys.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MergeFieldArgumentsInPlace(this FieldDefinition existingField, IDictionary<string, object?> newArguments)
    {
        var target = existingField._arguments!;
        foreach (var (key, newValue) in newArguments)
        {
            if (target.TryGetValue(key, out var existingValue)
                && existingValue is IDictionary<string, object?> existingDict
                && newValue is IDictionary<string, object?> newDict)
            {
                target[key] = Helpers.MergeNullableDictionaries(existingDict, newDict);
                continue;
            }
            target[key] = newValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition MergeFieldArguments(this FieldDefinition existingField, IDictionary<string, object?>? newArguments)
    {
        if (newArguments is not { Count: > 0 }) return existingField;

        if (existingField._arguments is null || existingField._arguments.Count == 0)
        {
            return existingField with { _arguments = AsSortedCaseInsensitive(newArguments) };
        }

        var merged = CopyToSortedCaseInsensitive(existingField._arguments);
        ApplyArgumentOverrides(merged, newArguments);
        return existingField with { _arguments = merged };
    }

    private static SortedDictionary<string, object?> AsSortedCaseInsensitive(IDictionary<string, object?> source)
        => source is SortedDictionary<string, object?> sd
            ? sd
            : new SortedDictionary<string, object?>(source, StringComparer.OrdinalIgnoreCase);

    private static SortedDictionary<string, object?> CopyToSortedCaseInsensitive(IDictionary<string, object?> source)
    {
        var copy = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source) copy[key] = value;
        return copy;
    }

    private static void ApplyArgumentOverrides(SortedDictionary<string, object?> target, IDictionary<string, object?> overrides)
    {
        foreach (var (key, newValue) in overrides)
        {
            target[key] = MergedArgumentValue(target, key, newValue);
        }
    }

    private static object? MergedArgumentValue(SortedDictionary<string, object?> target, string key, object? newValue)
    {
        if (target.TryGetValue(key, out var existingValue)
            && existingValue is IDictionary<string, object?> existingDict
            && newValue is IDictionary<string, object?> newDict)
        {
            return Helpers.MergeNullableDictionaries(existingDict, newDict);
        }
        return newValue;
    }
}
