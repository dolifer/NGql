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
    /// Determines if two fields can be merged based on their structure, arguments, and
    /// conditional-include state (<c>@include</c>/<c>@skip</c> — see <see cref="AreConditionalDirectivesEqual"/>).
    /// The recursion is bounded by the field tree's depth — the public API does not allow
    /// constructing cyclic trees (FieldDefinition.Fields is get-only, _children is internal),
    /// so the recursion always terminates. A library bug introducing a cycle would surface as
    /// a StackOverflowException, which is louder and more actionable than a swallowed throw.
    /// </summary>
    internal static bool CanMergeFields(FieldDefinition existingField, FieldDefinition incomingField)
    {
        if (!Helpers.AreArgumentsEqual(existingField._arguments, incomingField._arguments))
            return false;
        if (!AreConditionalDirectivesEqual(existingField, incomingField))
            return false;
        return AreNestedFieldsCompatible(existingField, incomingField);
    }

    /// <summary>
    /// Compares the <c>@include</c>/<c>@skip</c> conditional state of two fields — the part of a
    /// field's directive list that is MERGE-IDENTITY-RELEVANT. Two fragments requesting the same
    /// field path under different runtime conditions (different <c>if</c> variables, or one
    /// conditional and one not) are not the same field for merge purposes: collapsing them into one
    /// node would apply one side's condition to content the other side asked for unconditionally.
    /// Custom directives (added via the generic <see cref="Builders.FieldBuilder.Directive(string, System.Collections.Generic.Dictionary{string, object?})"/>
    /// overload, excluding the reserved <c>include</c>/<c>skip</c> names) are intentionally NOT part
    /// of this comparison — they stay repeatable/mergeable exactly as before, matching the issue's
    /// scope (only <c>@include</c>/<c>@skip</c> are constrained here).
    /// </summary>
    private static bool AreConditionalDirectivesEqual(FieldDefinition existingField, FieldDefinition incomingField)
    {
        var existingInclude = FindConditional(existingField._directives, "include");
        var incomingInclude = FindConditional(incomingField._directives, "include");
        if (!AreDirectivesStructurallyEqual(existingInclude, incomingInclude)) return false;

        var existingSkip = FindConditional(existingField._directives, "skip");
        var incomingSkip = FindConditional(incomingField._directives, "skip");
        return AreDirectivesStructurallyEqual(existingSkip, incomingSkip);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "The plain loop short-circuits on first match without allocating an enumerator on the merge-identity hot path.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDirective? FindConditional(List<FieldDirective>? directives, string name)
    {
        if (directives is not { Count: > 0 }) return null;
        foreach (var directive in directives)
        {
            if (string.Equals(directive.Name, name, StringComparison.Ordinal)) return directive;
        }
        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreDirectivesStructurallyEqual(FieldDirective? a, FieldDirective? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return a.IsStructurallyEqualTo(b);
    }

    /// <summary>
    /// Whether <paramref name="field"/> carries an <c>@include</c> or <c>@skip</c> directive on its
    /// own node. Folded into <see cref="HasAnyArguments"/>/<see cref="SubtreeHasAnyArguments"/> so a
    /// child that differs ONLY by conditional-include state (no arguments anywhere) is still treated
    /// as merge-identity-significant — otherwise <see cref="IsIncomingChildCompatible"/>/
    /// <see cref="IsExistingExtraCompatible"/>'s argument-free early-out would silently accept a
    /// mismatched condition as "absent, therefore compatible".
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasConditionalDirectives(FieldDefinition field)
        => field._directives is { Count: > 0 } directives
        && (FindConditional(directives, "include") is not null || FindConditional(directives, "skip") is not null);

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
        var existingChild = FindChildByNameAndAlias(existingChildren, incomingChild);
        if (existingChild is null) return !HasAnyArguments(incomingChild);
        return Helpers.AreArgumentsEqual(existingChild._arguments, incomingChild._arguments)
            && AreConditionalDirectivesEqual(existingChild, incomingChild)
            && AreNestedFieldsCompatible(existingChild, incomingChild);
    }

    /// <summary>
    /// Finds a child matching <paramref name="target"/>'s exact GraphQL identity — (Name, alias),
    /// not Name alone. Two children may legitimately share a Name while differing by alias (distinct
    /// response keys); a Name-only lookup would wrongly treat one as a match for the other. Thin
    /// wrapper over the shared ordinal probe <see cref="Helpers.FindByOrdinalNameAndAlias"/> — unlike
    /// <see cref="Helpers.FindExistingField(FieldChildren, FieldDefinition)"/>, an outright miss here
    /// stays a plain null (no Path-based fallback): every caller of this method treats null as
    /// "no compatible existing child", not "search further by another identity".
    /// </summary>
    private static FieldDefinition? FindChildByNameAndAlias(FieldChildren children, FieldDefinition target)
        => Helpers.FindByOrdinalNameAndAlias(children, target.Name, target._alias);

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
        return FindChildByNameAndAlias(incomingChildren, existingChild) is not null || !HasAnyArguments(existingChild);
    }

    // Named "HasAnyArguments" historically (pre-directives), but the memoized flag actually answers
    // a broader question: "is this subtree merge-identity-significant beyond bare structure?" —
    // which now includes conditional-include state, not just arguments. Folding
    // HasConditionalDirectives in here (rather than a parallel memoized flag) means every existing
    // caller — the fingerprint short-circuit, ExistingExtrasCompatible's early-out, ancestor
    // invalidation — picks up directive-significance for free, with the same invalidation contract
    // (ClearMergeMemo) that already covers argument mutations.
    private static bool SubtreeHasAnyArguments(FieldDefinition field)
    {
        if (field._subtreeHasAnyArguments is { } cached) return cached;

        var result = field._arguments is { Count: > 0 }
            || HasConditionalDirectives(field)
            || AnyChildHasArguments(field._children);
        field._subtreeHasAnyArguments = result;
        return result;
    }

    // FNV-style mixing constant shared with Helpers.ComputeArgumentFingerprint's CombineHash —
    // duplicated here (rather than exposed) to keep the deep-fingerprint recursion self-contained
    // within the file that owns its invalidation contract.
    private const ulong DeepFingerprintFnvPrime = 1099511628211UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong CombineDeepHash(ulong hash, ulong value)
        => (hash ^ value) * DeepFingerprintFnvPrime;

    /// <summary>
    /// Computes (and memoizes on <see cref="FieldDefinition._deepArgumentFingerprint"/>) a
    /// conservative fingerprint over the MERGE-RELEVANT subtree of <paramref name="field"/>, for
    /// use as a cheap pre-filter bucket key in <c>QueryMerger.FindMergeTarget</c> — NEVER as a
    /// merge decision by itself. Strictly refines (never coarsens) the shallow, own-arguments-only
    /// fingerprint <see cref="Helpers.ComputeArgumentFingerprint"/> already provides, since this
    /// fingerprint folds that same value in at every level plus a commutative combination of every
    /// argument-carrying descendant.
    ///
    /// <para>
    /// <b>Conservatism proof sketch</b> (the contract this method must uphold: <c>CanMergeFields(a, b)
    /// == true</c> MUST imply <c>ComputeDeepFingerprint(a) == ComputeDeepFingerprint(b)</c>).
    /// <c>CanMergeFields</c> requires equal own arguments (already covered — matches
    /// <see cref="Helpers.ComputeArgumentFingerprint"/>'s own proven contract) and, recursively for
    /// every child on EITHER side whose subtree carries an argument anywhere
    /// (<see cref="SubtreeHasAnyArguments"/>), that the OTHER side has a same-named child which is
    /// itself pairwise <c>CanMergeFields</c>-compatible. (<see cref="IsIncomingChildCompatible"/>'s
    /// and <see cref="IsExistingExtraCompatible"/>'s absent/argument-free branches only accept a
    /// missing counterpart when the present side's subtree is ENTIRELY argument-free — so an
    /// argument-carrying child always demands an argument-carrying, pairwise-compatible match on
    /// the other side.) By induction on subtree depth, a pairwise-compatible child pair has equal
    /// deep fingerprints — so folding in EVERY argument-carrying child's (name, deep fingerprint)
    /// pair can never observe two <c>CanMergeFields</c>-compatible fields disagree.
    /// </para>
    ///
    /// <para>
    /// <b>Argument-free-child invariance.</b> A child whose entire subtree carries no arguments
    /// (checked via the existing memoized <see cref="SubtreeHasAnyArguments"/> — NOT the child's
    /// own <c>_arguments</c> in isolation) contributes NOTHING to the hash: <c>CanMergeFields</c>
    /// tolerates such a child being entirely absent from, or structurally different on, the other
    /// side (<see cref="IsIncomingChildCompatible"/> returns true for an incoming argument-free
    /// child missing from existing; <see cref="ExistingExtrasCompatible"/> early-outs completely
    /// when the existing subtree has no arguments at all). Two otherwise-identical fields differing
    /// only in argument-free children — at any depth — MUST hash equal, or a genuine merge would be
    /// falsely split. Recursing through <see cref="SubtreeHasAnyArguments"/> (not a shallow check)
    /// is what lets a SINGLE argument three levels down still make the whole ancestor chain down to
    /// it significant, while an entirely argument-free branch of arbitrary depth vanishes completely.
    /// </para>
    ///
    /// <para>
    /// <b>Order independence.</b> Children are matched by name, not position (see
    /// <see cref="FieldChildren.Find(string)"/>), so per-child contributions are combined with XOR —
    /// commutative and associative — rather than an order-sensitive fold.
    /// </para>
    /// </summary>
    internal static ulong ComputeDeepFingerprint(FieldDefinition field)
    {
        if (field._deepArgumentFingerprint is { } cached) return cached;

        // O(1) short-circuit for the common argument-free subtree: when SubtreeHasAnyArguments(field)
        // is false, field._arguments is empty/null (so ownArgsFp is the fixed FnvOffsetBasis constant
        // — see Helpers.ComputeArgumentFingerprint) AND every descendant's own SubtreeHasAnyArguments
        // is also false (that is exactly what AnyChildHasArguments, which SubtreeHasAnyArguments folds
        // in, recursively requires). DeepChildrenFingerprint's loop would therefore `continue` past
        // every single child without ever computing a contribution, always returning 0UL — so skipping
        // straight to CombineDeepHash(ownArgsFp, 0UL) here is provably identical to running the loop,
        // without visiting a single child. This is what collapses the O(children)-per-call cost that
        // MergeIncomingChildrenInPlace's memo invalidation otherwise forces on every Include for the
        // (extremely common) case of a merge-irrelevant, argument-free subtree of arbitrary size.
        if (!SubtreeHasAnyArguments(field))
        {
            var constantResult = CombineDeepHash(Helpers.ComputeArgumentFingerprint(null), 0UL);
            field._deepArgumentFingerprint = constantResult;
            return constantResult;
        }

        var ownArgsFp = CombineDeepHash(Helpers.ComputeArgumentFingerprint(field._arguments), ConditionalDirectiveFingerprint(field));
        var childrenFp = DeepChildrenFingerprint(field._children);
        var result = CombineDeepHash(ownArgsFp, childrenFp);

        field._deepArgumentFingerprint = result;
        return result;
    }

    // Distinct sentinel folded in when a field carries NEITHER @include nor @skip, so an
    // include/skip-bearing field's fingerprint can never coincide with one that reuses the same
    // combined value by coincidence of the two directives' own fingerprints XORing to the "absent"
    // marker's bit pattern (defense in depth — CombineDeepHash's FNV mixing already makes this
    // astronomically unlikely, but a named sentinel makes the "absent" case an explicit branch
    // rather than an emergent zero).
    private const ulong NoConditionalDirectiveSentinel = 0xA24BAED4963EE889UL;

    /// <summary>
    /// Conservative fingerprint contribution for <paramref name="field"/>'s OWN <c>@include</c>/
    /// <c>@skip</c> state, folded into <see cref="ComputeDeepFingerprint"/>'s own-node hash exactly
    /// like <see cref="Helpers.ComputeArgumentFingerprint"/> folds in arguments. Must uphold the same
    /// conservatism contract: <see cref="AreConditionalDirectivesEqual"/> considering two fields
    /// equal MUST imply this returns the same value for both — reuses
    /// <see cref="Helpers.ComputeArgumentFingerprint"/> directly on each directive's (already
    /// case-insensitively sorted — see <see cref="FieldDirective"/>'s constructor)
    /// <see cref="FieldDirective.Arguments"/>, which is already proven conservative for arbitrary
    /// argument shapes (nested dictionaries, lists, and unrecognized value types all fold into the
    /// shared conservative sentinel rather than risk a false inequality).
    /// </summary>
    private static ulong ConditionalDirectiveFingerprint(FieldDefinition field)
    {
        if (field._directives is not { Count: > 0 } directives) return NoConditionalDirectiveSentinel;

        var include = FindConditional(directives, "include");
        var skip = FindConditional(directives, "skip");
        if (include is null && skip is null) return NoConditionalDirectiveSentinel;

        // "include" and "skip" are hashed under distinct salts so `@include(if:$x)` alone and
        // `@skip(if:$x)` alone (same underlying argument shape, different directive name) never
        // collide into the same contribution — mirroring how ComputeArgumentFingerprint folds the
        // KEY hash in alongside the value hash rather than hashing values alone.
        var includeFp = include is { } inc
            ? CombineDeepHash((ulong)"include".GetHashCode(StringComparison.Ordinal), ArgumentsFingerprint(inc.Arguments))
            : 0UL;
        var skipFp = skip is { } sk
            ? CombineDeepHash((ulong)"skip".GetHashCode(StringComparison.Ordinal), ArgumentsFingerprint(sk.Arguments))
            : 0UL;

        return CombineDeepHash(includeFp, skipFp);
    }

    // FieldDirective's constructor always routes arguments through Helpers.SortArgumentValue into a
    // case-insensitive SortedDictionary (see FieldDirective.NormalizeArguments) or leaves Arguments
    // null for an empty/absent dictionary — there is no public construction path that produces any
    // other concrete type, so the cast below is unconditionally safe.
    private static ulong ArgumentsFingerprint(IReadOnlyDictionary<string, object?>? arguments)
        => Helpers.ComputeArgumentFingerprint((SortedDictionary<string, object?>?)arguments);

    private static ulong DeepChildrenFingerprint(FieldChildren? children)
    {
        if (children is not { Count: > 0 }) return 0UL;

        var span = children.AsSpan();
        var accumulator = 0UL;
        for (int i = 0; i < span.Length; i++)
        {
            var child = span[i];

            // Argument-free-at-every-depth children are invisible to CanMergeFields (see the
            // conservatism proof above) — they must be invisible to the fingerprint too, or a
            // genuine merge candidate that only differs by such a child would be falsely split.
            if (!SubtreeHasAnyArguments(child)) continue;

            // XOR (commutative) so sibling iteration order never affects the result — children
            // are matched by NAME during the actual merge, never by position.
            var nameHash = (ulong)child.Name.GetHashCode(StringComparison.OrdinalIgnoreCase);
            var childContribution = CombineDeepHash(nameHash, ComputeDeepFingerprint(child));
            accumulator ^= childContribution;
        }
        return accumulator;
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

    // Un-memoized counterpart of SubtreeHasAnyArguments, used at the "child missing from the other
    // side entirely" leaf checks in IsIncomingChildCompatible/IsExistingExtraCompatible. Must apply
    // the exact same significance rule (arguments OR conditional directives OR a significant
    // descendant) — a child differing only by @include/@skip, with no arguments anywhere in its
    // subtree, must not be treated as "absent, therefore compatible" any more than an
    // argument-bearing child would be.
    private static bool HasAnyArguments(FieldDefinition field)
    {
        if (field._arguments is { Count: > 0 }) return true;
        if (HasConditionalDirectives(field)) return true;
        if (field._children is null) return false;
        foreach (var child in field._children.AsSpan())
        {
            if (HasAnyArguments(child)) return true;
        }
        return false;
    }

    /// <summary>
    /// Deep-clones <paramref name="source"/> producing a new <see cref="FieldDefinition"/> that
    /// shares no mutable state with the original. This is THE clone boundary — it must copy
    /// every per-field store (children, arguments, metadata, inline fragments, spreads); any
    /// store it misses aliases the clone to the source and silently drops state on isolation
    /// paths (Preserve, Include). Used by <see cref="QueryMerger"/> and the preservation pipeline.
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

        if (source._children is { Count: > 0 })
        {
            clone._children = new FieldChildren(source._children.Count);
            foreach (var child in source._children.AsSpan())
                clone._children.Append(child.DeepClone());
        }

        clone.SetOptionalState(
            DeepCloneFragments(source._fragments),
            source._spreadFragments is { Count: > 0 } ? new List<string>(source._spreadFragments) : null,
            source._directives is { Count: > 0 } ? new List<FieldDirective>(source._directives) : null,
            source._metadata is { Count: > 0 } ? new Dictionary<string, object?>(source._metadata) : null);

        return clone;
    }

    internal static InlineFragmentDefinition DeepClone(this InlineFragmentDefinition source)
    {
        var clone = new InlineFragmentDefinition(source.TypeName);
        CloneFragmentBody(source._fields, source._fragments, source._spreadFragments,
            out clone._fields, out clone._fragments, out clone._spreadFragments);

        // FieldDirective is an immutable record — sharing entries is safe; only the LIST needs to
        // be a fresh instance so mutating the clone's directives can never reach back into source.
        clone._directives = source._directives is { Count: > 0 }
            ? new List<FieldDirective>(source._directives)
            : null;

        return clone;
    }

    internal static NamedFragmentDefinition DeepClone(this NamedFragmentDefinition source)
    {
        var clone = new NamedFragmentDefinition(source.Name, source.OnType);
        CloneFragmentBody(source._fields, source._fragments, source._spreadFragments,
            out clone._fields, out clone._fragments, out clone._spreadFragments);
        return clone;
    }

    internal static Dictionary<string, InlineFragmentDefinition>? DeepCloneFragments(Dictionary<string, InlineFragmentDefinition>? fragments)
    {
        if (fragments is not { Count: > 0 }) return null;

        var clone = new Dictionary<string, InlineFragmentDefinition>(fragments.Count, StringComparer.Ordinal);
        foreach (var (typeName, fragment) in fragments)
        {
            clone[typeName] = fragment.DeepClone();
        }
        return clone;
    }

    private static void CloneFragmentBody(
        FieldChildren? sourceFields,
        Dictionary<string, InlineFragmentDefinition>? sourceFragments,
        List<string>? sourceSpreads,
        out FieldChildren? fields,
        out Dictionary<string, InlineFragmentDefinition>? fragments,
        out List<string>? spreads)
    {
        fields = null;
        if (sourceFields is { Count: > 0 })
        {
            fields = new FieldChildren(sourceFields.Count);
            foreach (var child in sourceFields.AsSpan())
                fields.Append(child.DeepClone());
        }

        fragments = DeepCloneFragments(sourceFragments);
        spreads = sourceSpreads is { Count: > 0 } ? new List<string>(sourceSpreads) : null;
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
        MergeFragmentState(existing, incoming);
        return existing;
    }

    /// <summary>
    /// Merges <paramref name="incoming"/>'s inline fragments, fragment spreads, and directives INTO
    /// <paramref name="existing"/>, mutating its collections in place. Everything carried over is
    /// deep-cloned (spreads and directives are copied into fresh lists; inline fragments are cloned
    /// via <see cref="DeepClone(InlineFragmentDefinition)"/>) so the merged field never aliases the
    /// source's fragment state. Spread insertion order is preserved and duplicate spread names are
    /// skipped, matching <see cref="FieldDefinition.AddSpreadFragment"/>. Directives are appended in
    /// order, skipping any that are structurally identical to one already present (so merging the
    /// same directive on a same-path field does not duplicate it). Shared by every field-level
    /// Include path.
    /// </summary>
    internal static void MergeFragmentState(FieldDefinition existing, FieldDefinition incoming)
    {
        MergeInlineFragmentsInPlace(existing, incoming._fragments);
        MergeSpreadsInPlace(existing, incoming._spreadFragments);
        MergeDirectivesInPlace(existing, incoming._directives);
    }

    private static void MergeInlineFragmentsInPlace(FieldDefinition existing, Dictionary<string, InlineFragmentDefinition>? incomingFragments)
    {
        if (incomingFragments is not { Count: > 0 }) return;

        existing._fragments ??= new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal);
        foreach (var (typeName, incomingFragment) in incomingFragments)
        {
            if (existing._fragments.TryGetValue(typeName, out var existingFragment))
            {
                MergeInlineFragmentBodyInPlace(existingFragment, incomingFragment);
            }
            else
            {
                existing._fragments[typeName] = incomingFragment.DeepClone();
            }
        }
    }

    // Merges two inline fragments of the same type: their field bodies, nested inline fragments,
    // and spreads. Each incoming child/fragment is deep-cloned before entering the target so the
    // merged fragment owns its subtree exclusively.
    private static void MergeInlineFragmentBodyInPlace(InlineFragmentDefinition existing, InlineFragmentDefinition incoming)
    {
        if (incoming._fields is { Count: > 0 })
        {
            var targetFields = existing._fields ??= new FieldChildren();
            foreach (var child in incoming._fields.AsSpan())
            {
                MergeChildInPlace(targetFields, child);
            }
        }

        if (incoming._fragments is { Count: > 0 })
        {
            existing._fragments ??= new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal);
            foreach (var (typeName, nested) in incoming._fragments)
            {
                if (existing._fragments.TryGetValue(typeName, out var existingNested))
                {
                    MergeInlineFragmentBodyInPlace(existingNested, nested);
                }
                else
                {
                    existing._fragments[typeName] = nested.DeepClone();
                }
            }
        }

        MergeSpreadListInPlace(ref existing._spreadFragments, incoming._spreadFragments);

        if (incoming._directives is { Count: > 0 } incomingDirectives)
        {
            foreach (var directive in incomingDirectives)
            {
                existing.AddDirective(directive);
            }
        }
    }

    private static void MergeSpreadsInPlace(FieldDefinition existing, List<string>? incomingSpreads)
    {
        var current = existing._spreadFragments;
        var spreads = current;
        MergeSpreadListInPlace(ref spreads, incomingSpreads);
        if (!ReferenceEquals(spreads, current)) existing._spreadFragments = spreads;
    }

    /// <summary>
    /// Merges <paramref name="incoming"/> inline fragments into <paramref name="target"/> (a fragment
    /// body's inline-fragment map), deep-cloning new entries and recursively merging matching type
    /// names. Used for named-fragment bodies, whose store is not a <see cref="FieldDefinition"/>.
    /// </summary>
    internal static void MergeInlineFragmentsInto(
        ref Dictionary<string, InlineFragmentDefinition>? target,
        Dictionary<string, InlineFragmentDefinition>? incoming)
    {
        if (incoming is not { Count: > 0 }) return;

        target ??= new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal);
        foreach (var (typeName, incomingFragment) in incoming)
        {
            if (target.TryGetValue(typeName, out var existingFragment))
            {
                MergeInlineFragmentBodyInPlace(existingFragment, incomingFragment);
            }
            else
            {
                target[typeName] = incomingFragment.DeepClone();
            }
        }
    }

    /// <summary>
    /// Merges <paramref name="incoming"/> spread names into <paramref name="target"/>, preserving
    /// insertion order and skipping duplicates. Public wrapper over the in-place list merge for the
    /// named-fragment body path.
    /// </summary>
    internal static void MergeSpreadNamesInto(ref List<string>? target, List<string>? incoming)
        => MergeSpreadListInPlace(ref target, incoming);

    // Appends incoming spread names not already present, preserving insertion order — mirrors the
    // dedup contract of FieldDefinition.AddSpreadFragment. Order is user-visible (directives can
    // differ per spread site) so it is never sorted.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "List.Contains is the filter AND List.Add the side effect; a Where(Contains) would hide the mutation and allocate an enumerator on the merge path.")]
    private static void MergeSpreadListInPlace(ref List<string>? existingSpreads, List<string>? incomingSpreads)
    {
        if (incomingSpreads is not { Count: > 0 }) return;

        existingSpreads ??= new List<string>();
        foreach (var name in incomingSpreads)
        {
            if (!existingSpreads.Contains(name))
            {
                existingSpreads.Add(name);
            }
        }
    }

    // Appends incoming directives in order via FieldDefinition.AddDirective — the same entry point
    // IncludeIf/SkipIf/Directive use, so merge gets the identical dedup AND last-call-wins collapse
    // rules for free: a structurally-identical directive already present is skipped (merging two
    // queries that each place the SAME directive on a same-path field must not emit it twice), and
    // for the two non-repeatable, spec-constrained names (`include`/`skip`) a differing incoming
    // directive REPLACES the existing one in place rather than appending — never emitting the
    // spec-invalid `@include(if:$x) @include(if:$x)` shape. Under MergeByFieldPath this branch is
    // only reached once CanMergeFields has already confirmed both sides carry the SAME conditional
    // state (see CanMergeFields's directive-identity check), so the replace path is unreachable
    // there in practice; under MergeByDefault, which merges same-name fields unconditionally, a
    // differing incoming @include/@skip legitimately overwrites the existing one — consistent with
    // how MergeByDefault already lets incoming field arguments silently override existing ones.
    // FieldDirective is an immutable record, so the shared reference is safe to hand to AddDirective
    // without cloning.
    private static void MergeDirectivesInPlace(FieldDefinition existing, List<FieldDirective>? incomingDirectives)
    {
        if (incomingDirectives is not { Count: > 0 }) return;

        foreach (var incoming in incomingDirectives)
        {
            existing.AddDirective(incoming);
        }
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

        // existing._children can legitimately still be null here: an object-typed field created
        // via AddField(name, "Object") with no sub-fields yet (or one whose type was promoted to
        // Object by ShouldConvertToObjectType without ever gaining a child) is a leaf-shaped node
        // that CanMergeFields already accepted as compatible with ANY incoming children —
        // ExistingExtrasCompatible early-outs as soon as existing's own children collection is
        // absent/empty, with nothing further to check on that side. So this is not an unenforced
        // invariant to trust; it is a real, reachable shape that must allocate on demand rather
        // than asserting non-null.
        var existingChildren = existing._children ??= new FieldChildren();
        var span = incomingChildren.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            MergeChildInPlace(existingChildren, span[i]);
        }

        // incomingChildren is evaluated for "any arguments anywhere" BEFORE the merge loop mutated
        // it into existingChildren's clones — AnyChildHasArguments only reads _arguments/_children,
        // which the merge loop does not mutate on the SOURCE side (MergeChildInPlace only clones
        // incoming nodes into the target; ExistingNested merges happen on the existing side).
        InvalidateMergeMemoAfterChildrenMerge(existing, AnyChildHasArguments(incomingChildren));
    }

    private static void MergeIncomingArgumentsInPlace(FieldDefinition existing, SortedDictionary<string, object?>? incomingArguments)
    {
        if (incomingArguments is not { Count: > 0 }) return;
        existing.MergeFieldArgumentsInPlace(incomingArguments);

        // No memo update needed here: CanMergeFields (which every MergeFieldsInPlace caller already
        // ran) requires AreArgumentsEqual(existing._arguments, incoming._arguments) at this exact
        // node, so reaching this line with a non-empty incomingArguments means existing._arguments
        // was ALREADY non-empty and equal — i.e. SubtreeHasAnyArguments(existing) was already true
        // (and therefore memoized true, or about to memoize true next read regardless). The values
        // merged in refine existing keys' nested dictionary contents (see MergeFieldArgumentsInPlace)
        // without changing the fingerprint-relevant "has any argument" shape at this node, but the
        // fingerprint VALUE can still change (nested value content differs) — so only the fingerprint
        // memo needs invalidating, and only when it was actually cached.
        if (existing._deepArgumentFingerprint is not null)
        {
            existing._deepArgumentFingerprint = null;
        }
    }

    /// <summary>
    /// Updates <see cref="FieldDefinition._subtreeHasAnyArguments"/> and
    /// <see cref="FieldDefinition._deepArgumentFingerprint"/> after merging incoming CHILDREN into
    /// <paramref name="existing"/>'s subtree, exploiting monotonicity instead of always nulling both
    /// memos (which forced an O(existing-subtree-size) recompute on every single Include — the
    /// O(N²) driver for a chain of N Includes into one shared parent).
    ///
    /// <para>
    /// <b>Monotonicity.</b> Merging NEVER removes an argument or a child — <c>MergeFieldArgumentsInPlace</c>
    /// only adds/overwrites keys, <c>MergeChildInPlace</c> only appends new children or recurses into
    /// existing ones (itself subject to this same monotonic rule). So once a subtree contains any
    /// argument anywhere, it always will — <c>_subtreeHasAnyArguments</c> can only ever transition
    /// false/null → true for a given live instance, never true → false. If it was already memoized
    /// <c>true</c>, no merge can invalidate that fact — both memos are left untouched (a global no-op,
    /// O(1)) beyond the fingerprint invalidation below.
    /// </para>
    ///
    /// <para>
    /// <b>The false/unknown case.</b> If it was NOT already known <c>true</c>, whether it becomes true
    /// now depends solely on whether THIS merge's incoming children introduced an argument anywhere —
    /// i.e. <paramref name="incomingChildrenHaveArguments"/>, which the caller computes over the
    /// (typically small, single-fragment) INCOMING side only, never by re-walking the accumulated
    /// existing subtree. If nothing was contributed, the subtree provably remains argument-free:
    /// <c>_subtreeHasAnyArguments</c> stays false (recorded explicitly, not left null, so a future
    /// call is also O(1)) — and per <c>ComputeDeepFingerprint</c>'s own short-circuit, an
    /// argument-free subtree's fingerprint is a fixed constant independent of its children, so the
    /// OLD fingerprint (if already memoized) is still correct and is likewise left untouched.
    /// </para>
    ///
    /// <para>
    /// <b>The third ("was false, incoming introduces a genuinely new argument") case.</b> On the
    /// <see cref="QueryMerger"/> path this shape cannot arise: <c>CanMergeFields</c> runs before
    /// <see cref="MergeFieldsInPlace"/> there and rejects it outright — <see cref="IsIncomingChildCompatible"/>
    /// only tolerates an incoming child ABSENT from existing when that child's whole subtree is
    /// argument-free, and any incoming child that DOES exist by name on the existing side must have
    /// <c>AreArgumentsEqual</c>-equal own arguments before recursing further. It IS reachable through
    /// the inline-fragment path, however: <see cref="MergeInlineFragmentBodyInPlace"/> calls
    /// <see cref="MergeChildInPlace"/> — and therefore <see cref="MergeFieldsInPlace"/> — with no
    /// <c>CanMergeFields</c> gate at all, so merging two same-typed inline fragments whose shared
    /// child gains an argument-bearing descendant lands here. Full invalidation is always safe, so
    /// this case simply forgoes the O(1) fast path and recomputes both memos on next read.
    /// </para>
    /// </summary>
    private static void InvalidateMergeMemoAfterChildrenMerge(FieldDefinition existing, bool incomingChildrenHaveArguments)
    {
        if (existing._subtreeHasAnyArguments == true)
        {
            // Already true — monotonicity guarantees it stays true; the exact fingerprint VALUE can
            // still change (new argument-bearing content deeper in an already-significant subtree),
            // so the fingerprint alone still needs invalidating.
            existing._deepArgumentFingerprint = null;
            return;
        }

        if (!incomingChildrenHaveArguments)
        {
            // Not already known true, and this merge's incoming children contributed nothing new.
            // Whether the subtree is argument-free now depends on existing's OWN arguments, not
            // just its children — recording a bare `false` here would be actively wrong for a
            // field that has its own arguments but had never had SubtreeHasAnyArguments computed
            // (memo still null) before this merge. Recompute from existing._arguments directly
            // (O(1) — no child recursion needed, since AnyChildHasArguments over existing's
            // children is exactly what's already known to be unaffected by this incoming merge).
            existing._subtreeHasAnyArguments = existing._arguments is { Count: > 0 };
            return;
        }

        // A previously argument-free subtree just gained an argument. Unreachable via QueryMerger
        // (CanMergeFields rejects that shape first) but genuinely reachable via the ungated
        // inline-fragment merge path — see the third-case remarks above. Drop both memos: full
        // invalidation is always correct, it only costs this method's O(1) fast path here.
        existing._subtreeHasAnyArguments = null;
        existing._deepArgumentFingerprint = null;
    }

    private static void MergeChildInPlace(FieldChildren existingChildren, FieldDefinition incomingChild)
    {
        var existingNested = FindChildByNameAndAlias(existingChildren, incomingChild);
        if (existingNested != null)
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

        existingField.InvalidateMergeIndex();

        if (existingField._arguments is null || existingField._arguments.Count == 0)
        {
            return existingField with
            {
                _arguments = AsSortedCaseInsensitive(newArguments),
                _deepArgumentFingerprint = null,
                _subtreeHasAnyArguments = null,
            };
        }

        var merged = CopyToSortedCaseInsensitive(existingField._arguments);
        ApplyArgumentOverrides(merged, newArguments);
        return existingField with
        {
            _arguments = merged,
            _deepArgumentFingerprint = null,
            _subtreeHasAnyArguments = null,
        };
    }

    private static SortedDictionary<string, object?> AsSortedCaseInsensitive(IDictionary<string, object?> source)
        => source is SortedDictionary<string, object?> sd
            ? sd
            : new SortedDictionary<string, object?>(source, StringComparer.OrdinalIgnoreCase);

    private static SortedDictionary<string, object?> CopyToSortedCaseInsensitive(IDictionary<string, object?> source)
    {
        // A source already sorted by the same comparer holds no case collisions, so the copy
        // constructor's linear-time path gives the same result as the overwrite loop below.
        if (source is SortedDictionary<string, object?> sorted && ReferenceEquals(sorted.Comparer, StringComparer.OrdinalIgnoreCase))
        {
            return new SortedDictionary<string, object?>(sorted, StringComparer.OrdinalIgnoreCase);
        }

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
