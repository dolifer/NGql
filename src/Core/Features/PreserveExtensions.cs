using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Extension methods for preserving specific QueryBuilder fields based on specified paths.
/// </summary>
internal static class PreserveExtensions
{
    /// <summary>
    /// Preserves only the fields that match or are descendants of the specified paths.
    /// </summary>
    /// <param name="query">The query builder to preserve fields from.</param>
    /// <param name="fieldPaths">The field paths to preserve.</param>
    /// <returns>A new QueryBuilder instance with only the preserved fields.</returns>
    public static QueryBuilder Preserve(this QueryBuilder query, params string[]? fieldPaths)
    {
        if (fieldPaths == null || fieldPaths.Length == 0)
            return query;

        var newQuery = QueryBuilder.CreateDefaultBuilder(query.Definition.Name);
        
        // Copy merging strategy and metadata
        newQuery.Definition.MergingStrategy = query.Definition.MergingStrategy;
        if (query.Definition._metadata != null)
        {
            foreach (var metadata in query.Definition.Metadata)
            {
                newQuery.Definition.Metadata[metadata.Key] = metadata.Value;
            }
        }

        foreach (var targetPath in fieldPaths)
        {
            ExtractMatchingFields(query.Definition.Fields, newQuery.Definition.Fields, targetPath.AsSpan());
        }

        // Extract variables from all preserved fields
        ExtractVariablesFromFields(newQuery.Definition.Fields, newQuery.Definition.Variables);

        return newQuery;
    }

    private static void ExtractMatchingFields(SortedDictionary<string, FieldDefinition> sourceFields,
        SortedDictionary<string, FieldDefinition> targetFields, ReadOnlySpan<char> path)
    {
        var dotIndex = path.IndexOf('.');
        var isLeafPath = dotIndex == -1;

        // Extract current segment
        var currentSegment = isLeafPath ? path : path[..dotIndex];

        var field = FindFieldByNameOrAlias(sourceFields, currentSegment);
        if (!field.HasValue)
        {
            return;
        }

        var (fieldKey, fieldDef) = field.Value;

        // FAST PATH: Leaf node - copy field directly
        if (isLeafPath)
        {
            targetFields[fieldKey] = fieldDef;
            return;
        }

        // SLOW PATH: Intermediate node - ensure field exists in target and recurse
        if (fieldDef._fields == null)
        {
            return;
        }

        if (!targetFields.TryGetValue(fieldKey, out var existingField))
        {
            existingField = CloneFieldWithoutChildren(fieldDef);
            targetFields[fieldKey] = existingField;
        }

        var remainingPath = path[(dotIndex + 1)..];
        ExtractMatchingFields(fieldDef.Fields, existingField.Fields, remainingPath);
    }

    /// <summary>
    /// Creates a shallow clone of a field without its children, preserving arguments and metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition CloneFieldWithoutChildren(FieldDefinition source)
    {
        var cloned = new FieldDefinition(
            source.Name,
            source.Type ?? Constants.DefaultFieldType,
            source.Alias,
            source._arguments,
            new SortedDictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase))
        {
            _fields = []
        };

        // Copy metadata if present
        if (source._metadata != null)
        {
            foreach (var meta in source._metadata)
            {
                cloned.Metadata[meta.Key] = meta.Value;
            }
        }

        return cloned;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static KeyValuePair<string, FieldDefinition>? FindFieldByNameOrAlias(SortedDictionary<string, FieldDefinition>? fields, ReadOnlySpan<char> nameOrAlias)
    {
        if (fields == null || nameOrAlias == null || nameOrAlias.Length == 0)
        {
            return null;
        }
        
        foreach (var kvp in fields)
        {
            // Use case-insensitive comparison to match SortedDictionary's StringComparer.OrdinalIgnoreCase
            if (nameOrAlias.Equals(kvp.Key.AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                nameOrAlias.Equals(kvp.Value.Name.AsSpan(), StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(kvp.Value.Alias) && nameOrAlias.Equals(kvp.Value.Alias.AsSpan(), StringComparison.OrdinalIgnoreCase)))
            {
                return kvp;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractVariablesFromFields(SortedDictionary<string, FieldDefinition> fields, SortedSet<Variable> variables)
    {
        foreach (var field in fields.Values)
        {
            // Extract variables from field arguments
            if (field._arguments?.Count > 0)
            {
                Helpers.ExtractVariablesFromValue(field._arguments, variables);
            }

            // Recursively extract from nested fields
            if (field._fields?.Count > 0)
            {
                ExtractVariablesFromFields(field._fields, variables);
            }
        }
    }
}
