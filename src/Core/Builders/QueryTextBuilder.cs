using System.Buffers;
using System.Collections;
using System.Text;
using NGql.Core;
using NGql.Core.Abstractions;
using NGql.Core.Caching;
using NGql.Core.Extensions;

namespace NGql.Core.Builders;

internal sealed class QueryTextBuilder
{
    private readonly StringBuilder _stringBuilder;
    
    // Constructor for creating new instances
    private QueryTextBuilder() => _stringBuilder = new StringBuilder();

    private const int IndentSize = 4;
    private const int MaxBuilderCapacity = 256 * 1024;  // 256KB threshold before reset

    // Pre-allocated padding strings for common indentation levels to avoid repeated allocations
    private static readonly string[] PaddingCache = new string[20];

    // Singleton comparer — Dictionary<string,FieldDefinition> is unordered, so we sort at render time.
    // Static readonly avoids any per-call allocation; the static lambda is stored as a cached delegate.
    //
    // Array.Sort with a comparer is an unstable introsort, so the primary key (effective name,
    // case-insensitive) alone leaves fields that share an effective key — e.g. a plain `a` and a
    // `b` aliased to `a` — in an order chosen arbitrarily by introsort internals, which can differ
    // across runtimes and .NET versions and breaks the deterministic render guarantee. The Name and
    // Alias tiebreakers (ordinal) turn the comparison into a total order derived purely from each
    // field's own data, so equal effective keys resolve to one well-defined order with no extra
    // allocation and no insertion-index threading. Primary key semantics are unchanged.
    private static readonly IComparer<FieldDefinition> FieldSortComparer =
        Comparer<FieldDefinition>.Create(static (a, b) =>
        {
            var effectiveNameComparison =
                StringComparer.OrdinalIgnoreCase.Compare(a.Alias ?? a.Name, b.Alias ?? b.Name);
            if (effectiveNameComparison != 0) return effectiveNameComparison;

            var nameComparison = string.CompareOrdinal(a.Name, b.Name);
            if (nameComparison != 0) return nameComparison;

            return string.CompareOrdinal(a.Alias, b.Alias);
        });

    // SHARED thread-local builder pool used by both QueryBlock and QueryDefinition
    // This consolidates the pooling strategy and prevents duplicate ThreadLocal instances
    private static readonly ThreadLocal<Stack<QueryTextBuilder>> SharedBuilderStack =
        new(() => new Stack<QueryTextBuilder>());

    private const int MaxPooledBuilders = 4;

    static QueryTextBuilder()
    {
        for (int i = 0; i < PaddingCache.Length; i++)
        {
            PaddingCache[i] = new string(' ', i * IndentSize);
        }
    }

    /// <summary>
    /// Gets a builder instance from the thread-local pool or creates a new one.
    /// Caller must return the builder via <see cref="ReturnToPool(QueryTextBuilder)"/>.
    /// </summary>
    internal static QueryTextBuilder GetFromPool()
    {
        var stack = SharedBuilderStack.Value!;
        return stack.Count > 0 ? stack.Pop() : new QueryTextBuilder();
    }

    /// <summary>
    /// Returns a builder to the thread-local pool for reuse.
    /// Clears the builder and checks capacity to prevent unbounded memory growth.
    /// </summary>
    internal static void ReturnToPool(QueryTextBuilder builder)
    {
        builder._stringBuilder.Clear();

        // Don't repool builders that have grown too large (prevents memory leak)
        if (builder._stringBuilder.Capacity > MaxBuilderCapacity)
        {
            return;
        }

        var stack = SharedBuilderStack.Value!;
        if (stack.Count < MaxPooledBuilders)
        {
            stack.Push(builder);
        }
        // If pool is full, let GC handle it
    }

    /// <summary>
    /// Gets padding string for the specified indent level, using cache for common levels.
    /// </summary>
    /// <param name="indent">Indentation level</param>
    /// <returns>Padding string</returns>
    private static string GetPadding(int indent)
    {
        var paddingLevel = indent / IndentSize;
        return paddingLevel < PaddingCache.Length
            ? PaddingCache[paddingLevel]
            : new string(' ', indent);
    }

    public string Build(QueryBlock queryBlock, int indent = 0, string? prefix = null)
    {
        // Clear only at the top-level call; recursive sub-query calls (indent > 0) accumulate.
        if (indent == 0) _stringBuilder.Clear();

        BuildBlock(queryBlock, indent, prefix);
        return _stringBuilder.ToString();
    }

    public string Build(QueryDefinition queryDefinition)
    {
        _stringBuilder.Clear();
        _stringBuilder.Append(queryDefinition.OperationType == OperationType.Mutation ? "mutation " : "query ");

        if (!string.IsNullOrEmpty(queryDefinition.Name))
        {
            _stringBuilder.Append(queryDefinition.Name);
        }

        if (queryDefinition._variables?.Count > 0)
        {
            _stringBuilder.Append('(');
            bool first = true;
            foreach (var variable in queryDefinition._variables)
            {
                if (!first)
                {
                    _stringBuilder.Append(", ");
                }

                first = false;
                variable.Print(_stringBuilder, variable.Name, true);
            }

            _stringBuilder.Append(')');
        }

        _stringBuilder.AppendLine("{");

        BuildFieldDefinitions(queryDefinition.Fields, IndentSize);

        _stringBuilder.Append("}");

        if (queryDefinition._namedFragments is { Count: > 0 })
        {
            BuildNamedFragmentDefinitions(queryDefinition._namedFragments);
        }

        return _stringBuilder.ToString();
    }

    /// <summary>
    /// Appends one query block (and, via <see cref="AddFields"/>, its sub-blocks) to the shared
    /// builder without materializing a string. Only the top-level <see cref="Build(QueryBlock, int, string?)"/>
    /// call pays for ToString.
    /// </summary>
    private void BuildBlock(QueryBlock queryBlock, int indent, string? prefix = null)
    {
        var pad = GetPadding(indent);
        var prevPad = pad;

        if (!string.IsNullOrWhiteSpace(queryBlock.Alias) && indent != 0)
        {
            _stringBuilder.Append(pad);
            _stringBuilder.Append(queryBlock.Alias);
            _stringBuilder.Append(':');
            pad = "";
        }

        _stringBuilder.Append(pad);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            _stringBuilder.Append(prefix).Append(' ');
        }

        _stringBuilder.Append(queryBlock.Name);

        AddArguments(queryBlock, indent == 0);
        indent += IndentSize;

        AddFields(queryBlock, prevPad, indent);
    }

    private void BuildFieldDefinitions(FieldChildren children, int indent)
    {
        // Take ONE snapshot of the concurrent collection. FieldChildren.Count and AsSpan() each do
        // an independent volatile read; reading them separately can tear if a concurrent Append lands
        // between the two, yielding a span longer than the rented count and silently dropping the tail
        // field(s). Rent, copy, render, clear and return all key off this single snapshot's length.
        var span = children.AsSpan();
        var count = span.Length;
        var arr = ArrayPool<FieldDefinition>.Shared.Rent(count);
        try
        {
            span.CopyTo(arr);
            RenderSortedFields(arr, count, indent);
        }
        finally
        {
            Array.Clear(arr, 0, count);
            ArrayPool<FieldDefinition>.Shared.Return(arr, clearArray: false);
        }
    }

    private void BuildFieldDefinitions(Dictionary<string, FieldDefinition> fields, int indent)
    {
        var count = fields.Count;
        if (count == 0) return;

        // Dictionary<TKey,TValue> is insertion-ordered, not alphabetical. Copy values to a
        // pooled buffer so RenderSortedFields can sort once and render with a stable order.
        var arr = ArrayPool<FieldDefinition>.Shared.Rent(count);
        try
        {
            int i = 0;
            foreach (var f in fields.Values) arr[i++] = f;
            RenderSortedFields(arr, count, indent);
        }
        finally
        {
            Array.Clear(arr, 0, count);
            ArrayPool<FieldDefinition>.Shared.Return(arr, clearArray: false);
        }
    }

    /// <summary>
    /// Sorts <paramref name="arr"/> in place via <see cref="FieldSortComparer"/> and writes the
    /// rendered fields into <see cref="_stringBuilder"/>. Shared rendering core for both backing
    /// collection types (Dictionary at the root, FieldChildren for nested levels).
    /// </summary>
    private void RenderSortedFields(FieldDefinition[] arr, int count, int indent)
    {
        Array.Sort(arr, 0, count, FieldSortComparer);
        var padding = GetPadding(indent);

        for (int j = 0; j < count; j++)
        {
            var field = arr[j];
            _stringBuilder.Append(padding);

            if (field.Alias != null)
            {
                _stringBuilder.Append(field.Alias);
                _stringBuilder.Append(':');
            }

            _stringBuilder.Append(field.Name);

            if (field._arguments is { Count: > 0 })
            {
                BuildFieldArguments(field._arguments);
            }

            // A field gets a `{ … }` block when it has child fields, inline fragments, or
            // named-fragment spreads. Plain leaf fields (none of the above) render as a bare
            // name + newline.
            var hasSelections = field._children is { Count: > 0 }
                || field._fragments is { Count: > 0 }
                || field._spreadFragments is { Count: > 0 };

            if (hasSelections)
            {
                _stringBuilder.AppendLine("{");
                BuildSelectionSetBody(field._children, field._fragments, field._spreadFragments, indent + IndentSize);
                _stringBuilder.Append(padding);
                _stringBuilder.AppendLine("}");
            }
            else
            {
                _stringBuilder.AppendLine();
            }
        }
    }

    /// <summary>
    /// Renders one selection-set body — plain fields, then inline fragments, then named-fragment
    /// spreads. Single shared implementation for the three nesting contexts (regular fields,
    /// inline fragments, named-fragment definitions) so the member order and Count guards live
    /// in exactly one place.
    /// </summary>
    private void BuildSelectionSetBody(
        FieldChildren? fields,
        Dictionary<string, InlineFragmentDefinition>? fragments,
        List<string>? spreadFragments,
        int indent)
    {
        if (fields is { Count: > 0 })
        {
            BuildFieldDefinitions(fields, indent);
        }
        if (fragments is { Count: > 0 })
        {
            BuildInlineFragments(fragments, indent);
        }
        if (spreadFragments is { Count: > 0 })
        {
            BuildSpreadFragments(spreadFragments, indent);
        }
    }

    /// <summary>
    /// Renders the inline fragments attached to a field. Each fragment is written as
    /// <c>... on TypeName { … }</c> with its own selection set rendered recursively.
    /// Fragments are sorted alphabetically by type name (case-sensitive — GraphQL type names
    /// are case-sensitive) for deterministic output.
    /// </summary>
    private void BuildInlineFragments(Dictionary<string, InlineFragmentDefinition> fragments, int indent)
    {
        // Snapshot keys AND values into paired pooled buffers and sort them together — keeps
        // the output stable regardless of insertion order without re-looking each fragment up
        // by key after the sort.
        var count = fragments.Count;
        var typeNames = ArrayPool<string>.Shared.Rent(count);
        var definitions = ArrayPool<InlineFragmentDefinition>.Shared.Rent(count);
        try
        {
            int i = 0;
            foreach (var (key, value) in fragments)
            {
                typeNames[i] = key;
                definitions[i] = value;
                i++;
            }
            Array.Sort(typeNames, definitions, 0, count, StringComparer.Ordinal);

            var padding = GetPadding(indent);
            for (int j = 0; j < count; j++)
            {
                var fragment = definitions[j];
                _stringBuilder.Append(padding);
                _stringBuilder.Append("... on ");
                _stringBuilder.Append(fragment.TypeName);
                _stringBuilder.AppendLine("{");

                BuildSelectionSetBody(fragment._fields, fragment._fragments, fragment._spreadFragments, indent + IndentSize);

                _stringBuilder.Append(padding);
                _stringBuilder.AppendLine("}");
            }
        }
        finally
        {
            Array.Clear(typeNames, 0, count);
            Array.Clear(definitions, 0, count);
            ArrayPool<string>.Shared.Return(typeNames, clearArray: false);
            ArrayPool<InlineFragmentDefinition>.Shared.Return(definitions, clearArray: false);
        }
    }

    /// <summary>
    /// Renders <c>...Name</c> spreads of named fragments inside a selection set. Order is
    /// preserved as recorded by <c>FieldBuilder.SpreadFragment</c> calls — we don't sort
    /// because spread order can become user-visible once directives (which differ per spread
    /// site) ship in a future release.
    /// </summary>
    private void BuildSpreadFragments(List<string> spreadFragments, int indent)
    {
        var padding = GetPadding(indent);
        for (int i = 0; i < spreadFragments.Count; i++)
        {
            _stringBuilder.Append(padding);
            _stringBuilder.Append("...");
            _stringBuilder.AppendLine(spreadFragments[i]);
        }
    }

    /// <summary>
    /// Renders the operation's named-fragment definitions as <c>fragment Name on Type{ … }</c>
    /// blocks after the operation's closing brace. Fragments are sorted alphabetically by name
    /// (case-sensitive) for deterministic output, consistent with how inline fragments and
    /// fields are sorted elsewhere.
    /// </summary>
    private void BuildNamedFragmentDefinitions(Dictionary<string, NamedFragmentDefinition> fragments)
    {
        var count = fragments.Count;
        var names = ArrayPool<string>.Shared.Rent(count);
        var definitions = ArrayPool<NamedFragmentDefinition>.Shared.Rent(count);
        try
        {
            int i = 0;
            foreach (var (key, value) in fragments)
            {
                names[i] = key;
                definitions[i] = value;
                i++;
            }
            Array.Sort(names, definitions, 0, count, StringComparer.Ordinal);

            for (int j = 0; j < count; j++)
            {
                var fragment = definitions[j];
                // First fragment: operation ended with `}` (no trailing newline) — emit a
                // newline to break onto its own line. Subsequent fragments: previous fragment
                // already ended with `}\n`, so go straight to `fragment` with no extra blank line.
                if (j == 0)
                {
                    _stringBuilder.AppendLine();
                }
                _stringBuilder.Append("fragment ");
                _stringBuilder.Append(fragment.Name);
                _stringBuilder.Append(" on ");
                _stringBuilder.Append(fragment.OnType);
                _stringBuilder.AppendLine("{");

                BuildSelectionSetBody(fragment._fields, fragment._fragments, fragment._spreadFragments, IndentSize);

                _stringBuilder.AppendLine("}");
            }
        }
        finally
        {
            Array.Clear(names, 0, count);
            Array.Clear(definitions, 0, count);
            ArrayPool<string>.Shared.Return(names, clearArray: false);
            ArrayPool<NamedFragmentDefinition>.Shared.Return(definitions, clearArray: false);
        }
    }

    private void BuildFieldArguments(IReadOnlyDictionary<string, object?> arguments)
    {
        _stringBuilder.Append('(');

        bool first = true;
        foreach (var (key, value) in arguments)
        {
            if (!first)
            {
                _stringBuilder.Append(", ");
            }

            first = false;

            _stringBuilder.Append(key);
            _stringBuilder.Append(':');
            WriteObject(_stringBuilder, value);
        }

        _stringBuilder.Append(')');
    }

    internal static void WriteObject(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        if (ValueFormatter.TryAppendPrimitive(value, builder))
            return;

        var valueType = value.GetType();
        if (ExtractKeyValuePairProperties(builder, value, valueType)) return;

        switch (value)
        {
            case IList listValue:
                {
                    Helpers.WriteCollection('[', ']', listValue, builder, WriteObject);
                    break;
                }

            // Arguments are normalized to SortedDictionary<string, object?> (see
            // Helpers.SortArgumentValue), so the common case implements the generic interface.
            // Enumerating it through the generic enumerator writes each key directly and recurses
            // on the value with no boxed KeyValuePair and no reflected PropertyInfo.GetValue —
            // exactly the same {k:v, …} output the non-generic IDictionary path below would emit.
            case IDictionary<string, object?> typedDict:
                {
                    WriteTypedDictionary(builder, typedDict);
                    break;
                }

            case IDictionary dictValue:
                {
                    Helpers.WriteCollection('{', '}', dictValue, builder, WriteObject);
                    break;
                }

            default:
                {
                    WriteObjectReflection(builder, value, valueType);
                    break;
                }
        }
    }

    private static void WriteTypedDictionary(StringBuilder builder, IDictionary<string, object?> dictionary)
    {
        builder.Append('{');
        bool first = true;
        foreach (var kvp in dictionary)
        {
            if (!first) builder.Append(", ");
            first = false;
            builder.Append(kvp.Key);
            builder.Append(':');
            WriteObject(builder, kvp.Value);
        }
        builder.Append('}');
    }

    private static bool ExtractKeyValuePairProperties(StringBuilder builder, object value, Type valueType)
    {
        if (!valueType.IsGenericType || valueType.GetGenericTypeDefinition() != typeof(KeyValuePair<,>))
        {
            return false;
        }

        // KeyValuePair<,> is a sealed BCL struct that always exposes Key and Value properties —
        // GetProperty cannot return null here, so cache the pair without nullable wrapping.
        var (keyProp, valueProp) = TypeMetadataCache.KvpPropertyCache.GetOrAdd(
            valueType,
            static t => (t.GetProperty("Key")!, t.GetProperty("Value")!));

        builder.Append(keyProp.GetValue(value));
        builder.Append(':');
        WriteObject(builder, valueProp.GetValue(value));
        return true;
    }

    private static void WriteObjectReflection(StringBuilder builder, object value, Type valueType)
    {
        var props = TypeMetadataCache.ObjectPropertyCache.GetOrAdd(valueType, static t => t.GetProperties());
        builder.Append('{');
        bool first = true;
        foreach (var prop in props)
        {
            if (!first) builder.Append(", ");
            first = false;
            builder.Append(prop.Name);
            builder.Append(':');
            WriteObject(builder, prop.GetValue(value));
        }
        builder.Append('}');
    }

    // Stable sort key for QueryBlock field lists: effective name first, original index as the
    // tiebreaker so equal names keep insertion order (matching LINQ OrderBy's stability).
    private static readonly Comparer<(string Key, int Index, object Item)> BlockFieldComparer =
        Comparer<(string Key, int Index, object Item)>.Create(static (a, b) =>
        {
            var nameComparison = StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
            return nameComparison != 0 ? nameComparison : a.Index.CompareTo(b.Index);
        });

    private void AddFields(QueryBlock queryBlock, string prevPad, int indent = 0)
    {
        if (queryBlock.IsEmpty)
        {
            return;
        }

        _stringBuilder.AppendLine("{");
        var padding = GetPadding(indent);

        // Sort fields by their effective name into a pooled buffer. QueryBlock.HandleAddField
        // rejects everything except string and QueryBlock at insert time, so a single ternary
        // covers every reachable case without a switch default.
        var fields = queryBlock.FieldsList;
        var count = fields.Count;
        var entries = ArrayPool<(string Key, int Index, object Item)>.Shared.Rent(count);
        try
        {
            for (int i = 0; i < count; i++)
            {
                var field = fields[i];
                var key = field is QueryBlock block ? (block.Alias ?? block.Name) : (string)field;
                entries[i] = (key, i, field);
            }

            Array.Sort(entries, 0, count, BlockFieldComparer);

            for (int i = 0; i < count; i++)
            {
                switch (entries[i].Item)
                {
                    case string strValue:
                        _stringBuilder.Append(padding);
                        _stringBuilder.AppendLine(strValue);
                        break;
                    case QueryBlock subQuery:
                        BuildBlock(subQuery, indent);
                        _stringBuilder.AppendLine();
                        break;
                }
            }
        }
        finally
        {
            Array.Clear(entries, 0, count);
            ArrayPool<(string Key, int Index, object Item)>.Shared.Return(entries, clearArray: false);
        }

        _stringBuilder.Append(prevPad);
        _stringBuilder.Append('}');
    }

    private void AddArguments(QueryBlock queryBlock, bool isRootElement)
    {
        // GetArguments allocates a fresh SortedDictionary; skip it entirely when nothing can
        // render — no explicit arguments, and no root variables that would be injected.
        // Whenever it IS called, the result is non-empty: explicit arguments are copied over,
        // and root variables are injected for any names not already present.
        if (queryBlock.Arguments.Count == 0 && (!isRootElement || queryBlock.Variables.Count == 0))
        {
            return;
        }

        var arguments = queryBlock.GetArguments(isRootElement);

        _stringBuilder.Append('(');

        bool first = true;
        foreach (var (key, value) in arguments)
        {
            if (!first)
            {
                _stringBuilder.Append(", ");
            }

            first = false;
            if (value is Variable variable)
            {
                variable.Print(_stringBuilder, key, isRootElement);
                continue;
            }

            _stringBuilder.Append(key);
            _stringBuilder.Append(':');

            WriteObject(_stringBuilder, value);
        }

        _stringBuilder.Append(')');
    }
}
