using System.Buffers;
using System.Collections;
using System.Text;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Generates unique signatures for field definitions based on their arguments/filters.
/// Optimized for performance using Span operations and reduced string allocations.
/// </summary>
public static class FieldSignatureGenerator
{
    // Pre-allocated StringBuilder for signature generation to avoid repeated allocations
    private static readonly ThreadLocal<StringBuilder> SignatureBuilder = new(() => new StringBuilder(InitialBuilderCapacity));

    private const int InitialBuilderCapacity = 256;
    // One oversized signature must not permanently bloat the thread-local builder — drop and
    // replace it past this threshold (same discipline as QueryTextBuilder's pool).
    private const int MaxBuilderCapacity = 64 * 1024;

    // Singleton comparer for sorting by Name for deterministic signature generation.
    private static readonly IComparer<FieldDefinition> FieldNameComparer =
        Comparer<FieldDefinition>.Create(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

    /// <summary>
    /// Generates a unique hash signature for a collection of field definitions.
    /// </summary>
    /// <param name="fields">The field definitions to generate signature for.</param>
    /// <returns>A unique hash representing the filter signature of the fields.</returns>
    public static int GenerateSignature(Dictionary<string, FieldDefinition> fields)
    {
        if (fields.Count == 0)
        {
            return 0;
        }

        var builder = SignatureBuilder.Value!;
        builder.Clear();

        foreach (var field in fields.Values)
        {
            AppendFieldSignature(builder, field, ReadOnlySpan<char>.Empty);
        }

        // builder.Length is always > 0 here: the empty-fields case was handled above and
        // every field appends at least its name to the signature.
        // Hash directly over StringBuilder chunks — avoids allocating a temporary string.
        int hash;
        unchecked
        {
            hash = 5381;
            foreach (var chunk in builder.GetChunks())
            {
                foreach (var c in chunk.Span)
                    hash = (hash << 5) + hash + c; // djb2: hash*33 + c
            }
        }

        if (builder.Capacity > MaxBuilderCapacity)
        {
            SignatureBuilder.Value = new StringBuilder(InitialBuilderCapacity);
        }

        return hash;
    }

    /// <summary>
    /// Appends field signature to the builder using Span operations for efficient path construction.
    /// </summary>
    /// <param name="builder">StringBuilder to append to</param>
    /// <param name="field">Field definition to process</param>
    /// <param name="parentPath">Parent path as ReadOnlySpan to avoid string allocations</param>
    private static void AppendFieldSignature(StringBuilder builder, FieldDefinition field, ReadOnlySpan<char> parentPath)
    {
        // Build the current path efficiently using Span operations
        Span<char> currentPathBuffer = stackalloc char[256]; // Stack allocation for path building
        int pathLength = 0;

        if (!parentPath.IsEmpty)
        {
            parentPath.CopyTo(currentPathBuffer);
            pathLength = parentPath.Length;
            currentPathBuffer[pathLength++] = '.';
        }

        var fieldNameSpan = field.Name.AsSpan();
        if (pathLength + fieldNameSpan.Length < currentPathBuffer.Length)
        {
            fieldNameSpan.CopyTo(currentPathBuffer[pathLength..]);
            pathLength += fieldNameSpan.Length;
        }
        else
        {
            // Fallback to string concatenation for very long paths.
            var currentPath = parentPath.IsEmpty ? field.Name : $"{parentPath.ToString()}.{field.Name}";
            builder.Append(currentPath);
            AppendFieldSignatureRemainder(builder, field, currentPath.AsSpan());
            return;
        }

        var currentPathSpan = currentPathBuffer[..pathLength];

        // Append the path to the signature
        builder.Append(currentPathSpan);

        AppendFieldSignatureRemainder(builder, field, currentPathSpan);
    }

    /// <summary>
    /// Appends the remainder of the field signature (arguments and child fields).
    /// </summary>
    /// <param name="builder">StringBuilder to append to</param>
    /// <param name="field">Field definition to process</param>
    /// <param name="currentPath">Current path (either as span or string)</param>
    private static void AppendFieldSignatureRemainder(StringBuilder builder, FieldDefinition field, ReadOnlySpan<char> currentPath)
    {
        // Add arguments if present
        if (field._arguments is { Count: > 0 })
        {
            builder.Append('[');
            // _arguments is SortedDictionary — already ordered by key, no OrderBy needed.
            foreach (var arg in field._arguments)
            {
                builder.Append(arg.Key);
                builder.Append(':');
                AppendArgumentValue(builder, arg.Value);
                builder.Append(';');
            }
            builder.Append(']');
        }

        builder.Append('|');

        // Recursively process child fields sorted by Name for a deterministic hash.
        // Dictionary<TKey,TValue> is unordered; explicit sort ensures signature stability.
        if (field._children is { Count: > 0 })
        {
            var fieldCount = field._children.Count;
            var rented = ArrayPool<FieldDefinition>.Shared.Rent(fieldCount);
            try
            {
                field._children.AsSpan().CopyTo(rented);
                Array.Sort(rented, 0, fieldCount, FieldNameComparer);
                for (var i = 0; i < fieldCount; i++)
                    AppendFieldSignature(builder, rented[i], currentPath);
            }
            finally
            {
                ArrayPool<FieldDefinition>.Shared.Return(rented, clearArray: false);
            }
        }
    }


    /// <summary>
    /// Appends argument value to the signature with optimized handling for common types.
    /// </summary>
    /// <param name="builder">StringBuilder to append to</param>
    /// <param name="value">Argument value to append</param>
    private static void AppendArgumentValue(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        if (ValueFormatter.TryAppendPrimitive(value, builder))
            return;

        AppendComplexValue(builder, value);
    }

    private static void AppendComplexValue(StringBuilder builder, object value)
    {
        switch (value)
        {
            case IDictionary<string, object?> dict:
                AppendDictionary(builder, dict);
                break;
            case IEnumerable enumerable when value is not string:
                AppendEnumerable(builder, enumerable);
                break;
            default:
                builder.Append(value);
                break;
        }
    }

    private static void AppendDictionary(StringBuilder builder, IDictionary<string, object?> dict)
    {
        // Argument dictionaries reach here through Helpers.SortArgumentValue, which converts
        // every nested dict to SortedDictionary; the IOrderedEnumerable fallback for plain
        // Dictionary instances was a defensive guard that the public API never triggers.
        Helpers.WriteCollection('{', '}', dict, builder, (sb, item) =>
        {
            var kvp = (KeyValuePair<string, object?>)item!;
            sb.Append(kvp.Key).Append(':');
            AppendArgumentValue(sb, kvp.Value);
        });
    }

    private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable)
        => Helpers.WriteCollection('[', ']', enumerable, builder, (sb, item) => AppendArgumentValue(sb, item));
}
