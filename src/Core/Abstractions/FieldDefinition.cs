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
    internal Dictionary<string, InlineFragmentDefinition>? _fragments;
    internal List<string>? _spreadFragments;
    internal List<FieldDirective>? _directives;
    internal string? _type;
    internal string? _alias;
    internal string _effectiveName;
    internal SortedDictionary<string, object?>? _arguments;
    internal Dictionary<string, object?>? _metadata;
    internal string Path { get; init; } = string.Empty;

    /// <summary>
    /// Cached result of "does this field's subtree contain any arguments?".
    /// Null = not yet computed. Reset to null whenever the subtree mutates.
    /// </summary>
    internal bool? _subtreeHasAnyArguments;

    /// <summary>
    /// Cached conservative fingerprint over this field's own arguments plus, recursively, every
    /// descendant whose subtree carries arguments anywhere (see
    /// <see cref="Extensions.FieldDefinitionExtensions.ComputeDeepFingerprint"/>). Null = not yet
    /// computed. Reset to null at exactly the same two sites as <see cref="_subtreeHasAnyArguments"/>
    /// whenever the subtree mutates — the two caches share an invalidation contract by design.
    /// </summary>
    internal ulong? _deepArgumentFingerprint;

    /// <summary>
    /// Process-wide counter bumped every time ANY <see cref="FieldDefinition"/>'s
    /// <see cref="_deepArgumentFingerprint"/> is invalidated outside of
    /// <see cref="Features.FieldMergeIndex"/>'s own bookkeeping (i.e. by <see cref="ClearMergeMemo"/>
    /// or <see cref="Builders.FieldBuilder.Where"/>'s direct writes). <see cref="Features.FieldMergeIndex"/>
    /// compares this against a snapshot taken when it last trusted its fingerprint sub-buckets: any
    /// change proves a mutation happened somewhere it was not explicitly told about (out-of-band
    /// <c>AddField</c>, a nested <c>Action&lt;FieldBuilder&gt;</c>, direct <c>Where()</c> calls, …),
    /// and the sub-buckets are invalidated wholesale rather than trusted. Deliberately process-wide
    /// rather than per-field or per-name: the mutation sites that must bump it are scattered across
    /// <see cref="Builders.FieldFactory"/> and <see cref="Builders.FieldBuilder"/> by design (each
    /// ancestor on the path to a mutation clears its own memo independently), so there is no single
    /// field or root dictionary to scope a counter to without re-threading every one of those call
    /// sites with index-awareness they should not need. A global counter costs one extra volatile
    /// read per <c>FindMergeTarget</c> call and, in the overwhelming common case (a chain of
    /// <c>Include()</c>s with no interleaved out-of-band mutation), never actually changes mid-chain
    /// — so the fingerprint sub-buckets stay warm across the whole chain regardless.
    /// </summary>
    private static long _mergeMemoEpoch;

    /// <summary>
    /// Bumps <see cref="_mergeMemoEpoch"/>. Called from <see cref="ClearMergeMemo"/> and from
    /// <see cref="Builders.FieldBuilder.Where"/>'s direct fingerprint-memo writes — the two places
    /// a field's deep fingerprint can be invalidated outside <see cref="Features.FieldMergeIndex"/>'s
    /// own bookkeeping.
    /// </summary>
    internal static void BumpMergeMemoEpoch() => System.Threading.Interlocked.Increment(ref _mergeMemoEpoch);

    /// <summary>
    /// Reads the current value of <see cref="_mergeMemoEpoch"/>, for <see cref="Features.FieldMergeIndex"/>
    /// to compare against its own last-synced snapshot.
    /// </summary>
    internal static long ReadMergeMemoEpoch() => System.Threading.Interlocked.Read(ref _mergeMemoEpoch);

    private bool? _isArray;
    private bool? _isNullable;

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
        _effectiveName = !string.IsNullOrEmpty(_alias) ? _alias : Name;
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
        _effectiveName = !string.IsNullOrEmpty(_alias) ? _alias : Name;
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
        init
        {
            _alias = value;
            _effectiveName = !string.IsNullOrEmpty(value) ? value : Name;
        }
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
        _spreadFragments ??= new List<string>();
        // List<string>.Contains is ordinal by default; avoids the enumerator allocation of the
        // LINQ Contains(value, comparer) overload.
        if (!_spreadFragments.Contains(name))
        {
            _spreadFragments.Add(name);
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
    /// <c>.IncludeIf("$x").IncludeIf("$x")</c> on one field renders <c>@include(if:$x)</c> once
    /// rather than emitting the spec-invalid <c>@include(if:$x) @include(if:$x)</c>. This mirrors
    /// the structural dedup on the fragment-merge path. Directives that differ (name or arguments)
    /// are all kept.
    /// </summary>
    [SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "The plain loop short-circuits without allocating an enumerator on the builder hot path.")]
    internal void AddDirective(FieldDirective directive)
    {
        _directives ??= new List<FieldDirective>();
        foreach (var existing in _directives)
        {
            if (existing.IsStructurallyEqualTo(directive))
            {
                return;
            }
        }
        _directives.Add(directive);
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
        get => _metadata ??= [];
        set => _metadata = value;
    }

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
    public bool IsArray => _isArray ??= _type.IsArrayType();

    /// <summary>
    /// Gets a value indicating whether this field type is nullable.
    /// </summary>
    [JsonIgnore]
    public bool IsNullable => _isNullable ??= _type.IsNullableType();

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
        _fragments ??= new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal);
        if (!_fragments.TryGetValue(typeName, out var fragment))
        {
            fragment = new InlineFragmentDefinition(typeName);
            _fragments[typeName] = fragment;
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
        BumpMergeMemoEpoch();
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
