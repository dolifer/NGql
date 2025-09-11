using System.Runtime.CompilerServices;
using NGql.Core.Builders;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods for preserving specific QueryBuilder fields based on specified paths.
/// </summary>
public static class PreserveExtensions
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

        if (dotIndex == -1)
        {
            // Fast path: single segment - direct lookup
            var field = FindFieldByNameOrAlias(sourceFields, path);
            if (field.HasValue)
            {
                targetFields[field.Value.Key] = field.Value.Value;
            }
            return;
        }

        // Multi-segment path
        var rootName = path[..dotIndex];
        var remainingPath = path[(dotIndex + 1)..];

        var rootField = FindFieldByNameOrAlias(sourceFields, rootName);
        if (!(rootField.HasValue && rootField.Value.Value._fields != null))
        {
            return;
        }

        var (rootKey, rootFieldDef) = rootField.Value;

        if (!targetFields.TryGetValue(rootKey, out var existingRootField))
        {
            existingRootField = new FieldDefinition(rootFieldDef.Name, rootFieldDef.Type ?? Constants.DefaultFieldType, rootFieldDef.Alias, 
                rootFieldDef._arguments, new SortedDictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase))
            {
                _fields = []
            };

            if (rootFieldDef._metadata != null)
            {
                foreach (var meta in rootFieldDef._metadata)
                {
                    existingRootField.Metadata[meta.Key] = meta.Value;
                }
            }

            targetFields[rootKey] = existingRootField;
        }
 
        ExtractMatchingFields(rootFieldDef.Fields, existingRootField.Fields, remainingPath);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeyValuePair<string, FieldDefinition>? FindFieldByNameOrAlias(SortedDictionary<string, FieldDefinition> fields, ReadOnlySpan<char> nameOrAlias)
    {
        foreach (var kvp in fields)
        {
            if (nameOrAlias.SequenceEqual(kvp.Value.Name.AsSpan()) ||
                (!string.IsNullOrEmpty(kvp.Value.Alias) && nameOrAlias.SequenceEqual(kvp.Value.Alias.AsSpan())))
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
