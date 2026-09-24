using System.Buffers;
using System.Collections;
using System.Text;
using NGql.Core.Builders;
using NGql.Core.Extensions;

namespace NGql.Core.Abstractions;

/// <summary>
/// Represents a GraphQL Query Block.
/// </summary>
public sealed class QueryBlock
{
    private readonly string _prefix;
    // Most blocks carry no arguments or variables, so both collections are created on first use.
    private SortedDictionary<string, object>? _arguments;
    private SortedSet<Variable>? _variables;
    private readonly List<object> _fieldsList;

    /// <summary>
    /// The list of fields to retrieve from GraphQL.
    /// </summary>
    public IReadOnlyList<object> FieldsList => _fieldsList;

    /// <summary>
    /// The collection of arguments related to <see cref="FieldsList"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Arguments => ArgumentsInternal;

    // Arguments are never removed, so while exactly one exists it is the first key written, in
    // its original casing. Rendering reads it directly: enumerating a SortedDictionary allocates
    // a traversal stack, and doing so through the interface also boxes the enumerator.
    private string? _firstArgumentKey;

    internal SortedDictionary<string, object> ArgumentsInternal
        => _arguments ??= new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    internal SortedSet<Variable> VariablesInternal => _variables ??= [];

    internal int ArgumentCount => _arguments?.Count ?? 0;

    internal int VariableCount => _variables?.Count ?? 0;

    internal bool TryGetSingleArgument([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out object? value)
    {
        key = _firstArgumentKey;
        value = null;
        return ArgumentCount == 1 && key is not null && _arguments!.TryGetValue(key, out value);
    }

    /// <summary>
    /// The collection of variables related to <see cref="FieldsList"/> or <see cref="Arguments"/>.
    /// </summary>
    public IReadOnlyCollection<Variable> Variables => VariablesInternal;

    /// <summary>
    /// The Query name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The Query alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Indicates if the query is empty.
    /// </summary>
    internal bool IsEmpty = false;

    /// <summary>
    /// Adds the variable with give name into <see cref="Variables"/> part of the query.
    /// </summary>
    /// <param name="variable">The variable</param>
    public void AddVariable(Variable variable)
        => HandleAddVariable(variable);

    /// <summary>
    /// Adds the variable with give name into <see cref="Variables"/> part of the query.
    /// </summary>
    /// <param name="name">The variable name</param>
    /// <param name="type">The value of the variable</param>
    public void AddVariable(string name, string type)
        => HandleAddVariable(new Variable(name, type));

    /// <summary>
    /// Adds the given generic list to the <see cref="FieldsList"/> part of the query.
    /// </summary>
    /// <remarks>
    /// Accepts any type of list, but must contain one of supported types of data.
    /// </remarks>
    /// <param name="selectList">Generic list of select fields.</param>
    public void AddField(IEnumerable<object> selectList)
        => HandleAddField(selectList);

    /// <summary>
    /// Adds the given list of strings to the <see cref="FieldsList"/> part of the query.
    /// </summary>
    /// <param name="selects">List of strings.</param>
    /// <returns>Query</returns>
    public void AddField(params string[] selects)
        => HandleAddField(selects);

    /// <summary>
    /// Adds the given sub query to the <see cref="FieldsList"/> part of the query.
    /// </summary>
    /// <param name="subQuery">A sub-query.</param>
    /// <returns>Query</returns>
    public void AddField(QueryBlock subQuery)
        => HandleAddField(subQuery);

    /// <summary>
    /// Adds the given key into <see cref="Arguments"/> part of the query.
    /// </summary>
    /// <param name="key">The Parameter Name</param>
    /// <param name="where">The value of the parameter, primitive or object</param>
    /// <returns></returns>
    public void AddArgument(string key, object where)
        => HandleAddArgument(key, where);

    /// <summary>
    /// Add a dict of key value pairs &lt;string, object&gt; into <see cref="Arguments"/> part of the query.
    /// </summary>
    /// <param name="dict">An existing Dictionary that takes &lt;string, object&gt;</param>
    /// <returns>Query</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when two keys in <paramref name="dict"/> collide under case-insensitive comparison
    /// (either against each other or against a key already present in <see cref="Arguments"/>), or
    /// when sorting a nested dictionary/decomposed-object value inside <paramref name="dict"/>
    /// uncovers the same kind of collision.
    /// <para>
    /// For <paramref name="dict"/> with more than one entry, every value is validated and sorted
    /// into a staging buffer before anything is committed to this block, so a throw — top-level or
    /// nested — leaves <see cref="Arguments"/> and <see cref="Variables"/> exactly as they were
    /// beforehand.
    /// </para>
    /// <para>
    /// For a single-entry <paramref name="dict"/>, this guarantee does NOT extend to
    /// <see cref="Variables"/>: that path routes through <see cref="HandleAddArgument"/>, which
    /// extracts variables from the value before sorting it, so a throw caused by a nested
    /// collision inside that single value can leave an extracted variable in <see cref="Variables"/>
    /// even though <see cref="Arguments"/> itself was never written. This is a pre-existing
    /// characteristic of the single-entry fast path, not new behavior.
    /// </para>
    /// </exception>
    public void AddArgument(IReadOnlyDictionary<string, object> dict)
    {
        if (dict.Count == 1)
        {
            // Single entry: no cross-entry collision is possible within dict itself, and the
            // nested-collision staging below would just allocate a one-element buffer for
            // nothing. HandleAddArgument validates against stored keys before touching this
            // block, but — unlike the staged multi-entry path below — it extracts variables from
            // the value before sorting it, so a throw from a nested collision inside the value can
            // still leave a variable behind (see the AddArgument XML doc's single-entry caveat).
            foreach (var (key, value) in dict)
                HandleAddArgument(key, value);
            return;
        }

        ValidateNoCaseCollisions(dict);

        // Stage every value's sorted form before committing anything: SortArgumentValue can
        // itself throw on an OrdinalIgnoreCase collision inside a nested dictionary or a
        // decomposed object's properties, and that must not leave _arguments or _variables
        // partially written (see AddArgument XML doc).
        var staged = new (string Key, object? Value)[dict.Count];
        var i = 0;
        foreach (var (key, value) in dict)
        {
            staged[i++] = (key, Helpers.SortArgumentValue(value));
        }

        foreach (var (key, sortedValue) in staged)
        {
            Helpers.ExtractVariablesFromValue(sortedValue, VariablesInternal);
            if (ArgumentCount == 0) _firstArgumentKey = key;
            ArgumentsInternal[key] = sortedValue!; // SortArgumentValue preserves non-null input
        }
    }

    /// <summary>
    /// Validates that no key in <paramref name="dict"/> collides, under OrdinalIgnoreCase, with
    /// another key in <paramref name="dict"/> or with a key already stored in <see cref="Arguments"/>.
    /// Run before any entry is applied so a rejected call leaves the block untouched.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Plain foreach avoids allocating an enumerator/closure per incoming dictionary; mirrors CollidesByCase's style.")]
    private void ValidateNoCaseCollisions(IReadOnlyDictionary<string, object> dict)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in dict.Keys)
        {
            if (!seen.Add(key))
            {
                throw new ArgumentException(
                    $"An item with the same key has already been added. Colliding key: '{key}'.",
                    nameof(dict));
            }

            if (CollidesByCase(key))
            {
                throw new ArgumentException(
                    $"An item with the same key has already been added. Colliding key: '{key}'.",
                    nameof(dict));
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryBlock"/> class.
    /// </summary>
    public QueryBlock(string name, string prefix = "", string? alias = null, params Variable[]? variables)
    {
        _prefix = prefix;
        Name = name;
        Alias = alias;

        _fieldsList = new List<object>();
        _variables = variables is { Length: > 0 } ? [.. variables.DistinctBy(x => x.Name)] : null;
    }

    public override string ToString()
    {
        var builder = QueryTextBuilder.GetFromPool();
        try
        {
            return builder.Build(this, prefix: _prefix);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(builder);
        }
    }

    /// <summary>
    /// Renders this block's GraphQL and appends it to <paramref name="builder"/> without
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
            textBuilder.BuildInto(this, builder, _prefix);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(textBuilder);
        }
    }

    /// <summary>
    /// Renders this block's GraphQL and writes it to <paramref name="writer"/> without
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
            textBuilder.BuildInto(this, writer, _prefix);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(textBuilder);
        }
    }

    /// <summary>
    /// Renders this block's GraphQL and transcodes it as UTF-8 directly into
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
            textBuilder.BuildInto(this, bufferWriter, _prefix);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(textBuilder);
        }
    }

    private void HandleAddField(object value)
    {
        switch (value)
        {
            case QueryBlock subQuery:
                AddSubQuery(subQuery);
                return;
            case string field:
                AddStringField(field);
                return;
            case IList list:
                AddListItems(list);
                return;
            default:
                throw new InvalidOperationException("Unsupported Field type found, must be a `string` or `QueryBlock`");
        }
    }

    private void AddSubQuery(QueryBlock subQuery)
    {
        // Enumerating a SortedSet allocates its traversal stack, and through the interface also
        // boxes the enumerator; most sub-queries declare no variables.
        if (subQuery._variables is { Count: > 0 } subQueryVariables)
        {
            foreach (var variable in subQueryVariables)
            {
                VariablesInternal.Add(variable);
            }
        }
        _fieldsList.Add(subQuery);
    }

    private void AddStringField(string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return;

        // Insert before the first string field that does not sort below `field`; non-string
        // entries are skipped without advancing the index (same semantics as the previous
        // OfType<string>().TakeWhile().Count() chain, without the LINQ allocations).
        var insertIndex = 0;
        foreach (var existing in _fieldsList)
        {
            if (existing is not string existingField) continue;
            if (string.Compare(existingField, field, StringComparison.OrdinalIgnoreCase) >= 0) break;
            insertIndex++;
        }
        _fieldsList.Insert(insertIndex, field);
    }

    private void AddListItems(IList list)
    {
        // A stable sort by name with the default string comparer: the order LINQ OrderBy produced,
        // without its iterator, sorter and key buffers.
        var count = list.Count;
        var entries = ArrayPool<(string? Key, int Index, object Item)>.Shared.Rent(count);
        try
        {
            for (var i = 0; i < count; i++)
            {
                var item = list[i]!;
                entries[i] = (ListSortKey(item), i, item);
            }
            entries.AsSpan(0, count).Sort(ListItemComparer);

            for (var i = 0; i < count; i++)
            {
                HandleAddField(entries[i].Item);
            }
        }
        finally
        {
            Array.Clear(entries, 0, count);
            ArrayPool<(string? Key, int Index, object Item)>.Shared.Return(entries);
        }
    }

    private static string? ListSortKey(object item) => item switch
    {
        string s => s,
        QueryBlock q => q.Name,
        _ => item.ToString()
    };

    private static readonly Comparison<(string? Key, int Index, object Item)> ListItemComparer =
        static (a, b) =>
        {
            var keyComparison = Comparer<string?>.Default.Compare(a.Key, b.Key);
            return keyComparison != 0 ? keyComparison : a.Index.CompareTo(b.Index);
        };

    private void HandleAddVariable(Variable variable)
    {
        VariablesInternal.Add(variable);
    }

    private void HandleAddArgument(string key, object value)
    {
        // Mirror the case-collision policy enforced for nested dictionary/object argument values
        // (which build via Dictionary.Add and throw on an OrdinalIgnoreCase collision): a key that
        // differs only by case from an existing one is a distinct GraphQL argument at the wire level,
        // so silently overwriting it via the indexer would lose data. Re-setting the exact same key
        // is still allowed; only a case-differing collision throws.
        //
        // This check runs before any mutation of the block (variable extraction, sorting, or the
        // dictionary write below) so a throw leaves this block exactly as it was beforehand — no
        // orphaned variable declarations, no partially-applied argument.
        if (CollidesByCase(key))
        {
            throw new ArgumentException(
                $"An item with the same key has already been added. Colliding key: '{key}'.",
                nameof(key));
        }

        Helpers.ExtractVariablesFromValue(value, VariablesInternal);
        var sortedValue = Helpers.SortArgumentValue(value);
        if (ArgumentCount == 0) _firstArgumentKey = key;
        ArgumentsInternal[key] = sortedValue!; // SortArgumentValue preserves non-null input
    }

    /// <summary>
    /// Returns true when <paramref name="key"/> matches a stored argument key only by case.
    /// New keys take the O(log n) membership check alone; the scan runs only when an existing
    /// key is set again, to tell an exact re-set (allowed) from a case-differing one (rejected).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Plain foreach over SortedDictionary.Keys short-circuits on the exact match without allocating a closure.")]
    private bool CollidesByCase(string key)
    {
        if (_arguments is null || !_arguments.ContainsKey(key)) return false;

        foreach (var storedKey in _arguments.Keys)
        {
            if (string.Equals(storedKey, key, StringComparison.Ordinal)) return false;
        }

        return true;
    }
}
