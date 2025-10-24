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
    private static readonly ThreadLocal<StringBuilder> SignatureBuilder = new(() => new StringBuilder(256));

    /// <summary>
    /// Generates a unique hash signature for a collection of field definitions.
    /// </summary>
    /// <param name="fields">The field definitions to generate signature for.</param>
    /// <returns>A unique hash representing the filter signature of the fields.</returns>
    public static int GenerateSignature(SortedDictionary<string, FieldDefinition> fields)
    {
        if (fields.Count == 0)
        {
            return 0;
        }

        var builder = SignatureBuilder.Value!;
        builder.Clear();

        foreach (var field in fields.Values.OrderBy(f => f.Name))
        {
            AppendFieldSignature(builder, field, ReadOnlySpan<char>.Empty);
        }

        var signature = builder.ToString();
        return string.IsNullOrEmpty(signature) ? 0 : signature.GetHashCode(StringComparison.InvariantCulture);
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
            // Fallback to string concatenation for very long paths
            var currentPath = parentPath.IsEmpty ? field.Name : $"{parentPath.ToString()}.{field.Name}";
            builder.Append(currentPath);
            AppendFieldSignatureRemainder(builder, field, currentPath);
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
            foreach (var arg in field._arguments.OrderBy(a => a.Key))
            {
                builder.Append(arg.Key);
                builder.Append(':');
                AppendArgumentValue(builder, arg.Value);
                builder.Append(';');
            }
            builder.Append(']');
        }

        builder.Append('|');

        // Recursively process child fields
        if (field._fields != null)
        {
            foreach (var childField in field._fields.Values.OrderBy(f => f.Name))
            {
                AppendFieldSignature(builder, childField, currentPath);
            }
        }
    }

    /// <summary>
    /// Overload for string-based current path (fallback for very long paths).
    /// </summary>
    private static void AppendFieldSignatureRemainder(StringBuilder builder, FieldDefinition field, string currentPath)
    {
        // Add arguments if present
        if (field._arguments is { Count: > 0 })
        {
            builder.Append('[');
            foreach (var arg in field._arguments.OrderBy(a => a.Key))
            {
                builder.Append(arg.Key);
                builder.Append(':');
                AppendArgumentValue(builder, arg.Value);
                builder.Append(';');
            }
            builder.Append(']');
        }

        builder.Append('|');

        // Recursively process child fields
        if (field._fields != null)
        {
            foreach (var childField in field._fields.Values.OrderBy(f => f.Name))
            {
                AppendFieldSignature(builder, childField, currentPath.AsSpan());
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

        if (ValueFormatter.TryFormatPrimitiveType(value, out var formattedValue))
        {
            builder.Append(formattedValue);
            return;
        }

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
        => Helpers.WriteCollection('{', '}', dict.OrderBy(d => d.Key), builder, (sb, item) =>
        {
            var kvp = (KeyValuePair<string, object?>)item!;
            sb.Append(kvp.Key).Append(':');
            AppendArgumentValue(sb, kvp.Value);
        });

    private static void AppendEnumerable(StringBuilder builder, IEnumerable enumerable)
        => Helpers.WriteCollection('[', ']', enumerable, builder, (sb, item) => AppendArgumentValue(sb, item));
}
