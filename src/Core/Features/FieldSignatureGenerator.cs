using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGql.Core.Abstractions;

namespace NGql.Core.Features;

/// <summary>
/// Generates unique signatures for field definitions based on their arguments/filters.
/// </summary>
public static class FieldSignatureGenerator
{
    /// <summary>
    /// Generates a unique hash signature for a collection of field definitions.
    /// </summary>
    /// <param name="fields">The field definitions to generate signature for.</param>
    /// <returns>A unique hash representing the filter signature of the fields.</returns>
    public static int GenerateSignature(SortedDictionary<string, FieldDefinition> fields)
    {
        if (fields.Count == 0)
            return 0;

        var signatureBuilder = new StringBuilder();

        foreach (var field in fields.Values.OrderBy(f => f.Name))
        {
            AppendFieldSignature(signatureBuilder, field, "");
        }

        var signature = signatureBuilder.ToString();
        return string.IsNullOrEmpty(signature) ? 0 : signature.GetHashCode(StringComparison.InvariantCulture);
    }

    private static void AppendFieldSignature(StringBuilder builder, FieldDefinition field, string parentPath)
    {
        var currentPath = string.IsNullOrEmpty(parentPath) ? field.Name : $"{parentPath}.{field.Name}";

        // Always include the field path in the signature
        builder.Append(currentPath);

        // Add arguments if present
        if (field.Arguments != null && field.Arguments.Count > 0)
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
                builder.Append(value.ToString());
                break;
        }
    }
}
