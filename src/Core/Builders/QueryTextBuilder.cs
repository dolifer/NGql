using System.Buffers;
using System.Collections;
using System.Text;
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
    private static readonly IComparer<FieldDefinition> FieldSortComparer =
        Comparer<FieldDefinition>.Create(static (a, b) =>
            StringComparer.OrdinalIgnoreCase.Compare(a.Alias ?? a.Name, b.Alias ?? b.Name));

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
        return _stringBuilder.ToString();
    }

    public string Build(QueryDefinition queryDefinition)
    {
        _stringBuilder.Clear();
        _stringBuilder.Append("query ");

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
        return _stringBuilder.ToString();
    }

    private void BuildFieldDefinitions(FieldChildren children, int indent)
    {
        var count = children.Count;
        var arr = ArrayPool<FieldDefinition>.Shared.Rent(count);
        try
        {
            children.AsSpan().CopyTo(arr);
            RenderSortedFields(arr, count, indent);
        }
        finally
        {
            Array.Clear(arr, 0, count);
            ArrayPool<FieldDefinition>.Shared.Return(arr, clearArray: false);
        }
    }

    private void BuildFieldDefinitions(SortedDictionary<string, FieldDefinition> fields, int indent)
    {
        var count = fields.Count;
        if (count == 0) return;

        // SortedDictionary already iterates in key order; sort the rented buffer anyway so the
        // render path uses _effectiveName ordering (alias-aware) rather than raw field name.
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

            if (field._children is { Count: > 0 })
            {
                _stringBuilder.AppendLine("{");
                BuildFieldDefinitions(field._children, indent + IndentSize);
                _stringBuilder.Append(padding);
                _stringBuilder.AppendLine("}");
            }
            else
            {
                _stringBuilder.AppendLine();
            }
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

    private void AddFields(QueryBlock queryBlock, string prevPad, int indent = 0)
    {
        if (queryBlock.IsEmpty)
        {
            return;
        }

        _stringBuilder.AppendLine("{");
        var padding = GetPadding(indent);

        // Sort fields by their effective name. QueryBlock.HandleAddField rejects everything
        // except string and QueryBlock at insert time, so a single ternary covers every
        // reachable case without a switch default.
        var orderedFields = queryBlock.FieldsList
            .OrderBy(field => field is QueryBlock block ? (block.Alias ?? block.Name) : (string)field,
                StringComparer.OrdinalIgnoreCase);

        foreach (var field in orderedFields)
        {
            switch (field)
            {
                case string strValue:
                    _stringBuilder.Append(padding);
                    _stringBuilder.AppendLine(strValue);
                    break;
                case QueryBlock subQuery:
                    this.Build(subQuery, indent); 
                    _stringBuilder.AppendLine();
                    break;
            }
        }

        _stringBuilder.Append(prevPad);
        _stringBuilder.Append('}');
    }

    private void AddArguments(QueryBlock queryBlock, bool isRootElement)
    {
        var arguments = queryBlock.GetArguments(isRootElement);

        if (arguments.Count == 0)
        {
            return;
        }

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
