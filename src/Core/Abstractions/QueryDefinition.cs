using System.Buffers;
using System.Text;
using System.Text.Json.Serialization;
using NGql.Core.Builders;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a query definition.
/// </summary>
public sealed record QueryDefinition(string Name, string Description = "")
{
    internal Dictionary<string, FieldDefinition>? _fields;
    internal SortedSet<Variable>? _variables;
    internal Dictionary<string, object?>? _metadata;
    internal Dictionary<string, NamedFragmentDefinition>? _namedFragments;

    /// <summary>
    /// The name of the query.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = Name;

    /// <summary>
    /// The description of the query.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = Description;

    /// <summary>
    ///     The collection of fields related to <see cref="QueryDefinition"/>.
    ///     Exposed read-only: mutating the field set from outside the assembly would desync
    ///     the builder's path index and query map. Fields are added through the
    ///     <see cref="QueryBuilder"/> API; internal code mutates via <see cref="FieldsInternal"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, FieldDefinition> Fields
        => _fields ??= new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Mutable view of <see cref="Fields"/> for internal builder and merge code. Lazily
    ///     initialized with the same case-insensitive comparer as the public getter.
    /// </summary>
    internal Dictionary<string, FieldDefinition> FieldsInternal
        => _fields ??= new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The collection of variables related to fields or arguments.
    /// </summary>
    [JsonIgnore]
    public SortedSet<Variable> Variables
    {
        get => _variables ??= new();
        internal set => _variables = value;
    }

    /// <summary>
    /// Metadata associated with the query definition.
    /// This can include additional information such as descriptions, tags, or any other relevant data.
    ///
    /// Not used during query text generation but can be useful for documentation or introspection purposes.
    ///
    /// Exposed read-only: replacing or mutating the bag from outside the assembly is not
    /// supported. Metadata is attached through <see cref="QueryBuilder.WithMetadata"/>;
    /// internal code mutates via <see cref="MetadataInternal"/>.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?> Metadata => _metadata ??= [];

    /// <summary>
    ///     Mutable view of <see cref="Metadata"/> for internal builder and preservation code.
    /// </summary>
    internal Dictionary<string, object?> MetadataInternal => _metadata ??= [];

    /// <summary>
    ///     Named fragments declared at this operation's top level, keyed by fragment name
    ///     (case-sensitive). Each fragment renders as <c>fragment Name on TypeName { … }</c>
    ///     after the operation block, sorted alphabetically by name.
    /// </summary>
    /// <remarks>
    ///     Fragments are referenced from a field's selection set via
    ///     <see cref="FieldDefinition.SpreadFragments"/>. NGql does not validate that every
    ///     spread points at a declared fragment — undeclared spreads render as <c>...Name</c>
    ///     and the server rejects them with a clear error. This keeps NGql schemaless.
    /// </remarks>
    [JsonPropertyName("namedFragments")]
    public IReadOnlyDictionary<string, NamedFragmentDefinition> NamedFragments
        => (IReadOnlyDictionary<string, NamedFragmentDefinition>?)_namedFragments ?? EmptyNamedFragments;

    private static readonly IReadOnlyDictionary<string, NamedFragmentDefinition> EmptyNamedFragments
        = new Dictionary<string, NamedFragmentDefinition>();

    /// <summary>
    /// Returns the existing named fragment for <paramref name="name"/>, or appends a new one
    /// declared on <paramref name="onType"/>. Used by the builder to back
    /// <c>QueryBuilder.AddFragment</c>. Throws <see cref="InvalidOperationException"/> if the
    /// fragment exists with a different <c>OnType</c> — silent override is the worst failure
    /// mode for a refactor that mistakenly reuses a fragment name.
    /// </summary>
    internal NamedFragmentDefinition GetOrAddNamedFragment(string name, string onType)
    {
        _namedFragments ??= new Dictionary<string, NamedFragmentDefinition>(StringComparer.Ordinal);
        if (_namedFragments.TryGetValue(name, out var fragment))
        {
            if (!string.Equals(fragment.OnType, onType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Named fragment '{name}' is already declared on type '{fragment.OnType}' — cannot redeclare on '{onType}'.");
            }
            return fragment;
        }

        fragment = new NamedFragmentDefinition(name, onType);
        _namedFragments[name] = fragment;
        return fragment;
    }

    /// <summary>
    /// Merging strategy for this query definition.
    /// </summary>
    [JsonPropertyName("mergingStrategy")]
    public MergingStrategy MergingStrategy { get; set; } = MergingStrategy.MergeByDefault;

    /// <summary>
    /// The kind of root operation rendered by this definition (query, mutation, or subscription).
    /// Selected by the factory on <see cref="QueryBuilder"/>; consumers do not set this directly.
    /// </summary>
    [JsonIgnore]
    internal OperationType OperationType { get; set; } = OperationType.Query;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString()
    {
        var builder = QueryTextBuilder.GetFromPool();
        try
        {
            return builder.Build(this);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(builder);
        }
    }

    /// <summary>
    /// Renders this definition's GraphQL and appends it to <paramref name="builder"/> without
    /// materializing the intermediate string that <see cref="ToString()"/> allocates. The
    /// appended text is byte-for-byte identical to <see cref="ToString()"/>.
    /// </summary>
    /// <param name="builder">The target <see cref="StringBuilder"/> to append to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public void AppendTo(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var textBuilder = QueryTextBuilder.GetFromPool();
        try
        {
            textBuilder.BuildInto(this, builder);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(textBuilder);
        }
    }

    /// <summary>
    /// Renders this definition's GraphQL and writes it to <paramref name="writer"/> without
    /// materializing the intermediate string that <see cref="ToString()"/> allocates. The
    /// written text is byte-for-byte identical to <see cref="ToString()"/>.
    /// </summary>
    /// <param name="writer">The target <see cref="TextWriter"/> to write to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> is null.</exception>
    public void WriteTo(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var textBuilder = QueryTextBuilder.GetFromPool();
        try
        {
            textBuilder.BuildInto(this, writer);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(textBuilder);
        }
    }

    /// <summary>
    /// Renders this definition's GraphQL and transcodes it as UTF-8 directly into
    /// <paramref name="bufferWriter"/>, with no intermediate <see cref="string"/> or <c>byte[]</c>
    /// allocation. The written bytes are identical to
    /// <c>System.Text.Encoding.UTF8.GetBytes(</c><see cref="ToString()"/><c>)</c>.
    /// </summary>
    /// <param name="bufferWriter">The target <see cref="IBufferWriter{Byte}"/> to write UTF-8 bytes to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bufferWriter"/> is null.</exception>
    public void WriteUtf8(IBufferWriter<byte> bufferWriter)
    {
        ArgumentNullException.ThrowIfNull(bufferWriter);
        var textBuilder = QueryTextBuilder.GetFromPool();
        try
        {
            textBuilder.BuildInto(this, bufferWriter);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(textBuilder);
        }
    }

    public static implicit operator string(QueryDefinition query) => query.ToString();
}
