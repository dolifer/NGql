using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NGql.Core.Abstractions;

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
            return 0;

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
        // Build current path efficiently using Span operations
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
        if (field.Arguments is { Count: > 0 })
        {
            builder.Append('[');
            foreach (var arg in field.Arguments.OrderBy(a => a.Key))
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
        foreach (var childField in field.Fields.Values.OrderBy(f => f.Name))
        {
            AppendFieldSignature(builder, childField, currentPath);
        }
    }

    /// <summary>
    /// Overload for string-based current path (fallback for very long paths).
    /// </summary>
    private static void AppendFieldSignatureRemainder(StringBuilder builder, FieldDefinition field, string currentPath)
    {
        // Add arguments if present
        if (field.Arguments is { Count: > 0 })
        {
            builder.Append('[');
            foreach (var arg in field.Arguments.OrderBy(a => a.Key))
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
        foreach (var childField in field.Fields.Values.OrderBy(f => f.Name))
        {
            AppendFieldSignature(builder, childField, currentPath.AsSpan());
        }
    }

    /// <summary>
    /// Appends argument value to the signature with optimized handling for common types.
    /// </summary>
    /// <param name="builder">StringBuilder to append to</param>
    /// <param name="value">Argument value to append</param>
    private static void AppendArgumentValue(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case string str:
                builder.Append('"').Append(str).Append('"');
                break;
            case int intValue:
                builder.Append(intValue);
                break;
            case long longValue:
                builder.Append(longValue);
                break;
            case bool boolValue:
                builder.Append(boolValue ? "true" : "false");
                break;
            case double doubleValue:
                builder.Append(doubleValue);
                break;
            case float floatValue:
                builder.Append(floatValue);
                break;
            case IDictionary<string, object?> dict:
                builder.Append('{');
                foreach (var kvp in dict.OrderBy(d => d.Key))
                {
                    builder.Append(kvp.Key).Append(':');
                    AppendArgumentValue(builder, kvp.Value);
                    builder.Append(',');
                }
                builder.Append('}');
                break;
            case System.Collections.IEnumerable enumerable when value is not string:
                builder.Append('[');
                foreach (var item in enumerable)
                {
                    AppendArgumentValue(builder, item);
                    builder.Append(',');
                }
                builder.Append(']');
                break;
            default:
                // Fallback to ToString() for other types
                var stringValue = value.ToString();
                if (stringValue != null)
                {
                    builder.Append(stringValue);
                }
                break;
        }
    }
}
