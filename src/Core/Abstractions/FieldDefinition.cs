using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NGql.Core.Extensions;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented")]
public sealed record FieldDefinition
{
    // Fields
    internal FieldChildren? _children;
    internal string? _type;
    internal string? _alias;
    internal SortedDictionary<string, object?>? _arguments;

    // Fragments, spreads, directives and metadata are absent on most fields. Keeping them behind
    // one reference saves three object slots per field. The holder is immutable and replaced on
    // assignment, so record copies that share it never observe one another's reassignments.
    private OptionalState? _optional;

    internal Dictionary<string, InlineFragmentDefinition>? _fragments
    {
        get => _optional?.Fragments;
        set => ReplaceOptional(static (current, fragments) => OptionalState.Create(fragments, current?.SpreadFragments, current?.Directives, current?.Metadata), value);
    }

    internal List<string>? _spreadFragments
    {
        get => _optional?.SpreadFragments;
        set => ReplaceOptional(static (current, spreads) => OptionalState.Create(current?.Fragments, spreads, current?.Directives, current?.Metadata), value);
    }

    internal List<FieldDirective>? _directives
    {
        get => _optional?.Directives;
        set => ReplaceOptional(static (current, directives) => OptionalState.Create(current?.Fragments, current?.SpreadFragments, directives, current?.Metadata), value);
    }

    internal Dictionary<string, object?>? _metadata
    {
        get => _optional?.Metadata;
        init => _optional = OptionalState.Create(_optional?.Fragments, _optional?.SpreadFragments, _optional?.Directives, value);
    }

    // Reading Metadata attaches a dictionary, so a reader can race a builder adding a directive
    // or fragment. Compare-and-swap keeps either replacement from discarding the other's member.
    private void ReplaceOptional<T>(Func<OptionalState?, T, OptionalState?> replace, T value)
    {
        while (true)
        {
            var current = Volatile.Read(ref _optional);
            if (ReferenceEquals(Interlocked.CompareExchange(ref _optional, replace(current, value), current), current)) return;
        }
    }

    /// <summary>
    /// Assigns all optional members with a single holder allocation (none when all are null).
    /// </summary>
    internal void SetOptionalState(Dictionary<string, InlineFragmentDefinition>? fragments, List<string>? spreadFragments,
        List<FieldDirective>? directives, Dictionary<string, object?>? metadata)
        => Volatile.Write(ref _optional, OptionalState.Create(fragments, spreadFragments, directives, metadata));

    internal string _effectiveName => !string.IsNullOrEmpty(_alias) ? _alias : Name;

    private sealed class OptionalState
    {
        internal readonly Dictionary<string, InlineFragmentDefinition>? Fragments;
        internal readonly List<string>? SpreadFragments;
        internal readonly List<FieldDirective>? Directives;
        internal readonly Dictionary<string, object?>? Metadata;

        private OptionalState(Dictionary<string, InlineFragmentDefinition>? fragments, List<string>? spreadFragments,
            List<FieldDirective>? directives, Dictionary<string, object?>? metadata)
        {
            Fragments = fragments;
            SpreadFragments = spreadFragments;
            Directives = directives;
            Metadata = metadata;
        }

        internal static OptionalState? Create(Dictionary<string, InlineFragmentDefinition>? fragments, List<string>? spreadFragments,
            List<FieldDirective>? directives, Dictionary<string, object?>? metadata)
            => fragments is null && spreadFragments is null && directives is null && metadata is null
                ? null
                : new OptionalState(fragments, spreadFragments, directives, metadata);
    }
    internal string Path { get; init; } = string.Empty;

    /// <summary>
    /// Cached result of "does this field's subtree contain any arguments?".
    /// Null = not yet computed. Reset to null whenever the subtree mutates.
    /// </summary>
    internal bool? _subtreeHasAnyArguments
    {
        get => _subtreeArgumentState == CachedBoolean.Unknown ? null : _subtreeArgumentState == CachedBoolean.True;
        set => _subtreeArgumentState = value switch
        {
            true => CachedBoolean.True,
            false => CachedBoolean.False,
            null => CachedBoolean.Unknown
        };
    }

    /// <summary>
    /// Cached conservative fingerprint over this field's own arguments plus, recursively, every
    /// descendant whose subtree carries arguments anywhere (see
    /// <see cref="Extensions.FieldDefinitionExtensions.ComputeDeepFingerprint"/>). Null = not yet
    /// computed. Reset to null at exactly the same two sites as <see cref="_subtreeHasAnyArguments"/>
    /// whenever the subtree mutates — the two caches share an invalidation contract by design.
    /// </summary>
    internal ulong? _deepArgumentFingerprint
    {
        get => _hasDeepArgumentFingerprint ? _deepArgumentFingerprintValue : null;
        set
        {
            if (value.HasValue)
            {
                _deepArgumentFingerprintValue = value.GetValueOrDefault();
                _hasDeepArgumentFingerprint = true;
            }
            else
            {
                _hasDeepArgumentFingerprint = false;
            }
        }
    }

    // Keep cache state in independent bytes. Packing unrelated caches into shared bit flags
    // would let concurrent readers lose one another's updates. Splitting the fingerprint's
    // payload from its presence flag also avoids Nullable<ulong>'s alignment padding.
    private ulong _deepArgumentFingerprintValue;
    private volatile bool _hasDeepArgumentFingerprint;
    private CachedBoolean _subtreeArgumentState;

    internal Features.MergeMemoTracker? _mergeMemoTracker;

    internal Features.MergeMemoTracker EnsureMergeMemoTracker()
        => LazyInitializer.EnsureInitialized(ref _mergeMemoTracker, static () => new Features.MergeMemoTracker());

    internal void InvalidateMergeIndex() => _mergeMemoTracker?.Invalidate();

    private CachedBoolean _isArray;
    private CachedBoolean _isNullable;

    private enum CachedBoolean : byte
    {
        Unknown,
        False,
        True
    }

    /// <summary>
    /// Creates a field definition with a name and optional type and alias.
    /// <paramref name="type"/> defaults to <see cref="Constants.DefaultFieldType"/> when null.
    /// </summary>
    /// <param name="name">Field name as it appears in the rendered GraphQL.</param>
    /// <param name="type">Optional type-annotation metadata (rendered nowhere; consumed by tooling).</param>
    /// <param name="alias">Optional response-side alias.</param>
    public FieldDefinition(string name, string? type = null, string? alias = null)
        : this(name, type ?? Constants.DefaultFieldType, alias, null)
    {
    }

    /// <summary>
    /// Creates a field definition with a pre-sorted argument dictionary and an optional
    /// child-field collection. Used internally on the hot path to avoid re-sorting.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <param name="type">Type-annotation metadata.</param>
    /// <param name="alias">Optional response-side alias.</param>
    /// <param name="sortedArguments">Pre-sorted argument map (case-insensitive); null/empty stores null.</param>
    /// <param name="fields">Optional initial children; null/empty leaves the field as a leaf.</param>
    public FieldDefinition(string name, string type, string? alias, SortedDictionary<string, object?>? sortedArguments = null, Dictionary<string, FieldDefinition>? fields = null)
    {
        Name = name;
        _alias = alias;
        _type = type;
        _arguments = sortedArguments?.Count > 0 ? sortedArguments : null;
        _children = AsChildren(fields);
    }

    /// <summary>
    /// Creates a field definition from an unsorted argument dictionary (e.g. one produced by
    /// callers using collection initializers). The dictionary is copied into a case-insensitive
    /// sorted store so output ordering is stable.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <param name="type">Type-annotation metadata.</param>
    /// <param name="alias">Optional response-side alias.</param>
    /// <param name="arguments">Unsorted argument map; null/empty stores null.</param>
    /// <param name="fields">Optional initial children; null/empty leaves the field as a leaf.</param>
    public FieldDefinition(string name, string type, string? alias, IDictionary<string, object?>? arguments, Dictionary<string, FieldDefinition>? fields = null)
    {
        Name = name;
        _alias = alias;
        _type = type;
        _arguments = ToSortedArguments(arguments);
        _children = AsChildren(fields);
    }

    private static SortedDictionary<string, object?>? ToSortedArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null) return null;
        if (arguments.Count == 0) return null;
        var sorted = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in arguments) sorted[kvp.Key] = kvp.Value;
        return sorted;
    }

    private static FieldChildren? AsChildren(Dictionary<string, FieldDefinition>? fields)
    {
        if (fields is null || fields.Count == 0) return null;
        var children = new FieldChildren(fields.Count);
        foreach (var kvp in fields) children.Append(kvp.Value);
        return children;
    }

    // Properties
    /// <summary>
    /// The name of the field.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// The type of the field. Defaults to <see cref="Constants.DefaultFieldType"/> if not specified.
    /// This is used to define the data type of the field, such as "String", "Int", "Boolean", etc.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type
    {
        get => _type;
        init => _type = value;
    }

    /// <summary>
    /// The alias of the field, if any. This is used to provide a more readable or meaningful name for the field in queries.
    /// If not specified, the field will use its original name.
    /// </summary>
    [JsonPropertyName("alias")]
    public string? Alias
    {
        get => _alias;
        init => _alias = value;
    }

    private static readonly IReadOnlyDictionary<string, FieldDefinition> EmptyReadOnlyFields
        = new FieldChildren();

    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, FieldDefinition> Fields
        => _children ?? EmptyReadOnlyFields;

    private static readonly IReadOnlyDictionary<string, InlineFragmentDefinition> EmptyReadOnlyFragments
        = new Dictionary<string, InlineFragmentDefinition>();

    /// <summary>
    /// Inline fragments attached to this field, keyed by the fragment's GraphQL type name
    /// (case-sensitive). Each fragment renders as <c>... on TypeName { … }</c> after the
    /// field's plain children, alphabetical by type name.
    /// </summary>
    /// <remarks>
    /// Used when the field's schema return type is a union or interface and the caller needs
    /// type narrowing. See <see cref="NGql.Core.Builders.FieldBuilder.OnType(string, Action{NGql.Core.Builders.FieldBuilder})"/>
    /// for the builder-side API. Multiple <c>OnType</c> calls for the same type name merge
    /// into one fragment definition.
    /// </remarks>
    [JsonPropertyName("inlineFragments")]
    public IReadOnlyDictionary<string, InlineFragmentDefinition> InlineFragments
        => (IReadOnlyDictionary<string, InlineFragmentDefinition>?)_fragments ?? EmptyReadOnlyFragments;

    private static readonly IReadOnlyList<string> EmptyReadOnlySpreadFragments = Array.Empty<string>();

    /// <summary>
    /// Names of <see cref="NamedFragmentDefinition"/>s spread into this field's selection set
    /// (rendered as <c>...Name</c>). Order is preserved from <c>SpreadFragment</c> calls; the
    /// renderer emits spreads in declaration order after plain fields and inline fragments.
    /// </summary>
    /// <remarks>
    /// A list, not a set: the same fragment may legitimately appear multiple times at the
    /// same selection set once directives are added in a future release (e.g.
    /// <c>...Foo @include(if: $a) ...Foo @include(if: $b)</c>). Today, repeated spreads of
    /// the same name produce duplicate <c>...Name</c> output that the server collapses; the
    /// builder de-duplicates eagerly at <c>FieldBuilder.SpreadFragment</c> to keep render
    /// output minimal.
    ///
    /// Spreads are not validated against <see cref="QueryDefinition.NamedFragments"/> — an
    /// undeclared spread renders verbatim and the server rejects it. NGql is schemaless.
    /// </remarks>
    [JsonPropertyName("spreadFragments")]
    public IReadOnlyList<string> SpreadFragments
        => (IReadOnlyList<string>?)_spreadFragments ?? EmptyReadOnlySpreadFragments;

    /// <summary>
    /// Append a fragment-spread reference to this field's selection set. No-ops when the
    /// fragment name is already present, keeping the render output minimal until directives
    /// (which would distinguish duplicate spreads) are supported.
    /// </summary>
    internal void AddSpreadFragment(string name)
    {
        var spreads = _spreadFragments;
        if (spreads is null)
        {
            spreads = new List<string>();
            _spreadFragments = spreads;
        }

        // List<string>.Contains is ordinal by default; avoids the enumerator allocation of the
        // LINQ Contains(value, comparer) overload.
        if (!spreads.Contains(name))
        {
            spreads.Add(name);
        }
    }

    private static readonly IReadOnlyList<FieldDirective> EmptyReadOnlyDirectives = Array.Empty<FieldDirective>();

    /// <summary>
    /// Directives attached to this field, in the order they were added. Rendered after the field's
    /// name and arguments and before its selection set, e.g. <c>@include(if:$a) @skip(if:$b)</c>.
    /// Returns an empty list (never null) when the field has no directives.
    /// </summary>
    /// <remarks>
    /// Order is user-visible and preserved verbatim — directives are never sorted. See
    /// <see cref="NGql.Core.Builders.FieldBuilder.IncludeIf(Variable)"/>,
    /// <see cref="NGql.Core.Builders.FieldBuilder.SkipIf(Variable)"/>, and
    /// <see cref="NGql.Core.Builders.FieldBuilder.Directive(string, System.Collections.Generic.Dictionary{string, object?})"/>
    /// for the builder-side API.
    /// </remarks>
    [JsonPropertyName("directives")]
    public IReadOnlyList<FieldDirective> Directives
        => (IReadOnlyList<FieldDirective>?)_directives ?? EmptyReadOnlyDirectives;

    /// <summary>
    /// Gets a value indicating whether this field carries any directives. Unlike reading
    /// <see cref="Directives"/>, this check allocates nothing on directive-less fields — prefer it
    /// as the guard when scanning field trees.
    /// </summary>
    [JsonIgnore]
    public bool HasDirectives => _directives is { Count: > 0 };

    /// <summary>
    /// Append a directive to this field's directive list. Order is preserved. A directive that is
    /// structurally identical to one already present is skipped, so calling e.g.
    /// <c>.Directive("format", …).Directive("format", …)</c> with identical arguments on one field
    /// renders the directive once rather than duplicating it. Directives that differ (name or
    /// arguments) are all kept, EXCEPT for the two non-repeatable, spec-constrained directive names
    /// <c>include</c> and <c>skip</c> — see <see cref="DirectiveListOps.Add"/>, which every
    /// <c>@include</c>/<c>@skip</c> call site (both <see cref="Builders.FieldBuilder.IncludeIf(Variable)"/>/
    /// <see cref="Builders.FieldBuilder.SkipIf(Variable)"/> and the generic
    /// <see cref="Builders.FieldBuilder.Directive(string, System.Collections.Generic.Dictionary{string, object?})"/>
    /// called with <c>"include"</c>/<c>"skip"</c> directly — routes through) shares with
    /// <see cref="InlineFragmentDefinition.AddDirective"/>.
    /// </summary>
    internal void AddDirective(FieldDirective directive)
    {
        var current = _directives;
        var directives = current;
        DirectiveListOps.Add(ref directives, directive);
        if (!ReferenceEquals(directives, current)) _directives = directives;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyReadOnlyArguments
        = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sorted, case-insensitive view of the field's GraphQL arguments. Returns an empty
    /// dictionary (never null) when the field has no arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public IReadOnlyDictionary<string, object?> Arguments
        => _arguments ?? EmptyReadOnlyArguments;

    /// <summary>
    /// Metadata associated with the field definition.
    /// This can include additional information such as descriptions, tags, or any other relevant data.
    ///
    /// Not used during query text generation but can be useful for documentation or introspection purposes.
    /// </summary>
    /// <remarks>
    /// Reading this property materializes (and permanently attaches) an empty dictionary on
    /// fields that have no metadata, so the returned instance is always safe to mutate. When
    /// walking large trees just to test for metadata, check <see cref="HasMetadata"/> first —
    /// it allocates nothing.
    /// </remarks>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata
    {
        get
        {
            var metadata = _metadata;
            if (metadata is not null) return metadata;

            ReplaceOptional(static (current, created) => current?.Metadata is not null
                ? current
                : OptionalState.Create(current?.Fragments, current?.SpreadFragments, current?.Directives, created), new Dictionary<string, object?>());
            return _metadata!;
        }
        set => SetMetadata(value);
    }

    private void SetMetadata(Dictionary<string, object?>? metadata)
        => ReplaceOptional(static (current, value) => OptionalState.Create(current?.Fragments, current?.SpreadFragments, current?.Directives, value), metadata);

    /// <summary>
    /// Gets a value indicating whether this field carries any metadata. Unlike reading
    /// <see cref="Metadata"/>, this check does not allocate or attach an empty dictionary
    /// to metadata-less fields — prefer it as the guard when scanning field trees.
    /// </summary>
    [JsonIgnore]
    public bool HasMetadata => _metadata is { Count: > 0 };

    /// <summary>
    /// Gets a value indicating whether this field type is an array.
    /// </summary>
    [JsonIgnore]
    public bool IsArray
    {
        get
        {
            if (_isArray == CachedBoolean.Unknown)
                _isArray = _type.IsArrayType() ? CachedBoolean.True : CachedBoolean.False;
            return _isArray == CachedBoolean.True;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this field type is nullable.
    /// </summary>
    [JsonIgnore]
    public bool IsNullable
    {
        get
        {
            if (_isNullable == CachedBoolean.Unknown)
                _isNullable = _type.IsNullableType() ? CachedBoolean.True : CachedBoolean.False;
            return _isNullable == CachedBoolean.True;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this field has child fields.
    /// </summary>
    [JsonIgnore]
    public bool HasFields => _children is { Count: > 0 };

    /// <summary>
    /// Gets a value indicating whether this field has any inline fragments.
    /// </summary>
    [JsonIgnore]
    public bool HasInlineFragments => _fragments is { Count: > 0 };

    /// <summary>
    /// Returns the existing inline fragment for <paramref name="typeName"/>, or appends a new
    /// one. Used by the builder to merge multiple <c>OnType("Repository", …)</c> calls on the
    /// same parent into a single fragment definition.
    /// </summary>
    internal InlineFragmentDefinition GetOrAddInlineFragment(string typeName)
    {
        var fragments = _fragments;
        if (fragments is null)
        {
            fragments = new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal);
            _fragments = fragments;
        }

        if (!fragments.TryGetValue(typeName, out var fragment))
        {
            fragment = new InlineFragmentDefinition(typeName);
            fragments[typeName] = fragment;
        }
        return fragment;
    }

    /// <summary>
    /// When <c>true</c>, the merger treats this field as opaque — it will not be merged with
    /// other fields of the same path; instead it gets aliased (<c>name_1</c>, <c>name_2</c>, …)
    /// during <see cref="NGql.Core.Builders.QueryBuilder.Include(NGql.Core.Builders.QueryBuilder)"/>.
    /// The setter is internal; the flag is set by <see cref="MergingStrategy.NeverMerge"/>.
    /// </summary>
    [JsonIgnore]
    public bool IsNeverMerge { get; internal set; }

    /// <summary>
    /// Clears this field's memoized <see cref="_deepArgumentFingerprint"/> and
    /// <see cref="_subtreeHasAnyArguments"/> caches. Both caches summarize this field's entire
    /// subtree, so any code path that mutates arguments or children anywhere beneath a field —
    /// not just on the field itself — must call this on every ancestor between the root and the
    /// mutation point, not only on the mutated node. A stale fingerprint that undercounts a
    /// descendant's arguments causes <see cref="NGql.Core.Features.QueryMerger"/> to skip a
    /// genuine merge candidate (a silent false split), which is strictly worse than the cost of
    /// clearing eagerly.
    /// </summary>
    internal void ClearMergeMemo()
    {
        _deepArgumentFingerprint = null;
        _subtreeHasAnyArguments = null;
        InvalidateMergeIndex();
    }

    // Methods
    public bool Equals(FieldDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(_type, other._type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_alias, other._alias, StringComparison.OrdinalIgnoreCase)
            && IsNeverMerge == other.IsNeverMerge;
    }

    // FieldDefinition holds mutable internal state by design (in-place merging in QueryMerger).
    // The hash captures identity at evaluation time; callers do not stash hashes across mutations.
#pragma warning disable S2328
    public override int GetHashCode()
    {
        // Comparer-based hashing for the case-insensitive members — equal under
        // OrdinalIgnoreCase implies equal hash, without the ToLowerInvariant string allocations.
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(Path);
        hash.Add(_type, StringComparer.OrdinalIgnoreCase);
        hash.Add(_alias, StringComparer.OrdinalIgnoreCase);
        hash.Add(IsNeverMerge);
        return hash.ToHashCode();
    }
#pragma warning restore S2328

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            return string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
        }

        return string.IsNullOrWhiteSpace(Alias) ? $"{Type} {Name}" : $"{Type} {Alias}:{Name}";
    }
}
