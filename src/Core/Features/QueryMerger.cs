using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Exceptions;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Handles merging of query definitions using different strategies
/// </summary>
internal static class QueryMerger
{
    /// <summary>
    /// Merges an incoming query definition into existing fields using the specified strategy
    /// </summary>
    /// <param name="existingFields">Current field definitions</param>
    /// <param name="incomingQuery">Query definition to merge</param>
    /// <param name="rootStrategy">Merging strategy to use</param>
    /// <returns>Merge result containing updated mappings and fields</returns>
    private static MergeResult MergeQuery(Dictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery, MergingStrategy rootStrategy)
    {
        var strategy = GetEffectiveMergingStrategy(rootStrategy, incomingQuery.MergingStrategy);

        return strategy switch
        {
            MergingStrategy.MergeByDefault => HandleMergeByDefault(existingFields, incomingQuery),
            MergingStrategy.NeverMerge => HandleNeverMerge(existingFields, incomingQuery),
            MergingStrategy.MergeByFieldPath => HandleMergeByFieldPath(existingFields, incomingQuery),
            _ => throw new ArgumentOutOfRangeException(nameof(rootStrategy), $"Merging strategy {strategy} is not implemented"),
        };
    }

    /// <summary>
    /// Merges an incoming query definition into the target query definition, updating all related state.
    /// </summary>
    /// <param name="targetDefinition">The target query definition to merge into</param>
    /// <param name="queryMap">The query map to update with merge results</param>
    /// <param name="queryBuilder">Optional QueryBuilder instance to update path index (performance optimization)</param>
    /// <param name="incomingQuery">Query definition to merge</param>
    public static void MergeQuery(
        QueryDefinition targetDefinition, 
        QueryMap queryMap,
        QueryBuilder? queryBuilder,
        in QueryDefinition incomingQuery)
    {
        // FAST PATH: Skip merge entirely if incoming query has no fields
        if (incomingQuery._fields?.Count == 0 || incomingQuery._fields == null) return;

        // FAST PATH: Merge variables efficiently
        var targetVars = targetDefinition._variables;
        var incomingVars = incomingQuery._variables;

        if (targetVars is null && incomingVars is not null)
        {
            // Only incoming has variables
            targetDefinition.Variables = new SortedSet<Variable>(incomingVars!);
        }
        else if (targetVars is not null && incomingVars is not null)
        {
            // Both have variables - merge without LINQ Union() enumerable allocation
            var mergedVars = new SortedSet<Variable>(targetVars, targetVars.Comparer);
            foreach (var v in incomingVars)
                mergedVars.Add(v);
            targetDefinition.Variables = mergedVars;
        }
        // else: only target has variables (targetVars not null, incomingVars null) - already set, no change needed
        // else: both null - no variables, lazy-init will handle it if accessed

        // Perform the field merge
        var mergeResult = MergeQuery(targetDefinition.Fields, incomingQuery, targetDefinition.MergingStrategy);

        // FAST PATH: Skip field updates if no changes were made.
        // Use count + ContainsKey instead of SequenceEqual — Dictionary key order is not guaranteed.
        if (mergeResult.UpdatedFields.Count == targetDefinition.Fields.Count)
        {
            bool allKeysMatch = true;
            foreach (var key in mergeResult.UpdatedFields.Keys)
            {
                if (!targetDefinition.Fields.ContainsKey(key))
                {
                    allKeysMatch = false;
                    break;
                }
            }

            if (allKeysMatch)
            {
                // Check if values are actually the same (reference equality for performance)
                bool hasChanges = false;
                foreach (var (key, newField) in mergeResult.UpdatedFields)
                {
                    if (!ReferenceEquals(targetDefinition.Fields[key], newField))
                    {
                        hasChanges = true;
                        break;
                    }
                }

                if (!hasChanges)
                {
                    // No actual changes - just update query map and return
                    queryMap.UpdateMappings(mergeResult.QueryMap);
                    return;
                }
            }
        }

        // Apply the merge results to fields
        targetDefinition.Fields.Clear();
        foreach (var (key, field) in mergeResult.UpdatedFields)
        {
            targetDefinition.Fields[key] = field;
        }

        // Update QueryMap with merge results (only for incoming query)
        queryMap.UpdateMappings(mergeResult.QueryMap);

        // Update root query mapping if fields changed
        queryMap.UpdateRootMapping(targetDefinition);

        // TODO: Implement lazy path indexing to avoid eager computation
        // The path index will be computed on-demand in GetPathTo() after fixing alias resolution
        // For now, skip population to avoid the overhead of DFS traversal during Include()
    }

    /// <summary>
    /// Updates the path index with newly merged or updated fields.
    /// This enables O(1) lookups in GetPathTo() instead of O(N×depth) DFS traversal.
    /// </summary>
    private static void UpdatePathIndex(
        Dictionary<string, string[]> pathIndex,
        QueryDefinition targetDefinition,
        Dictionary<string, FieldDefinition> updatedFields)
    {
        // For each updated field, compute and cache its path from root
        foreach (var fieldName in updatedFields.Keys)
        {
            if (targetDefinition.Fields.TryGetValue(fieldName, out var field))
            {
                var path = ComputeFieldPath(targetDefinition, fieldName);
                if (path != null)
                {
                    pathIndex[fieldName] = path;
                    
                    // Also index by alias if it differs from name
                    if (!ReferenceEquals(field._effectiveName, fieldName) &&
                        !pathIndex.ContainsKey(field._effectiveName))
                    {
                        pathIndex[field._effectiveName] = path;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Computes the path from root to a specific field.
    /// Returns the segments as a string array (e.g., ["player", "businessObjects", "playerProfile"]).
    /// </summary>
    private static string[]? ComputeFieldPath(QueryDefinition definition, string fieldName)
    {
        // For top-level fields, just return single-element array
        if (definition.Fields.TryGetValue(fieldName, out var field))
        {
            return new[] { field._effectiveName };
        }

        // For nested fields, search recursively
        var segments = new List<string>();
        foreach (var rootField in definition.Fields.Values)
        {
            segments.Add(rootField._effectiveName);
            if (FindPathSegments(rootField, fieldName, segments))
            {
                return segments.ToArray();
            }
            segments.Clear();
        }

        return null;
    }

    /// <summary>
    /// Recursively finds path segments to reach a target field name.
    /// Uses DFS with backtracking to build the complete path.
    /// </summary>
    private static bool FindPathSegments(
        FieldDefinition parent,
        string targetName,
        List<string> segments)
    {
        foreach (var field in parent.Fields.Values)
        {
            segments.Add(field._effectiveName);

            // Check if this field is the target (by effective name or original name)
            if (string.Equals(field._effectiveName, targetName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Recursively search in nested fields
            if (FindPathSegments(field, targetName, segments))
            {
                return true;
            }

            // Backtrack: remove this segment since target wasn't found here
            segments.RemoveAt(segments.Count - 1);
        }

        return false;
    }

    private static MergingStrategy GetEffectiveMergingStrategy(MergingStrategy rootStrategy, MergingStrategy childStrategy)
    {
        // Child NeverMerge always takes precedence
        if (childStrategy == MergingStrategy.NeverMerge)
        {
            return MergingStrategy.NeverMerge;
        }

        return rootStrategy switch
        {
            MergingStrategy.NeverMerge => MergingStrategy.NeverMerge,
            MergingStrategy.MergeByDefault => childStrategy,
            _ => rootStrategy,
        };
    }

    private static MergeResult HandleMergeByDefault(Dictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        return ProcessFieldMerge(existingFields, incomingQuery, (updatedFields, originalFieldKey, incomingField, queryMap) =>
        {
            FieldBuilder.Include(updatedFields, incomingField);
            queryMap[incomingQuery.Name] = originalFieldKey;
        });
    }

    private static MergeResult HandleNeverMerge(Dictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        return ProcessFieldMerge(existingFields, incomingQuery, (updatedFields, originalFieldKey, incomingField, queryMap) =>
        {
            // Mark the field as coming from a NeverMerge query so other queries don't merge into it
            var fieldWithMetadata = MarkAsNeverMerge(incomingField);
            AddFieldWithUniqueKey(updatedFields, originalFieldKey, fieldWithMetadata, queryMap, incomingQuery.Name);
        });
    }

    private static MergeResult HandleMergeByFieldPath(Dictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        return ProcessFieldMerge(existingFields, incomingQuery, (updatedFields, originalFieldKey, incomingField, queryMap) =>
        {
            var mergeTarget = FindMergeTarget(updatedFields, incomingField);

            if (mergeTarget != null)
            {
                try
                {
                    var mergedField = FieldDefinitionExtensions.MergeFields(mergeTarget.Value.Field, incomingField);
                    updatedFields[mergeTarget.Value.Key] = mergedField;
                    queryMap[incomingQuery.Name] = mergeTarget.Value.Key;
                }
                catch (QueryMergeException ex)
                {
                    throw new QueryMergeException($"Cannot merge query '{incomingQuery.Name}' due to type conflicts in field '{incomingField.Name}'", ex);
                }
            }
            else
            {
                AddFieldWithUniqueKey(updatedFields, originalFieldKey, incomingField, queryMap, incomingQuery.Name);
            }
        });
    }

    private static MergeResult ProcessFieldMerge(
        Dictionary<string, FieldDefinition> existingFields,
        QueryDefinition incomingQuery,
        Action<Dictionary<string, FieldDefinition>, string, FieldDefinition, Dictionary<string, string>> fieldProcessor)
    {
        var updatedFields = new Dictionary<string, FieldDefinition>(existingFields, StringComparer.OrdinalIgnoreCase);
        var queryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (originalFieldKey, incomingField) in incomingQuery.Fields)
        {
            fieldProcessor(updatedFields, originalFieldKey, incomingField, queryMap);
        }

        return new MergeResult(queryMap, updatedFields);
    }

    private static void AddFieldWithUniqueKey(
        Dictionary<string, FieldDefinition> updatedFields,
        string originalFieldKey,
        FieldDefinition incomingField,
        Dictionary<string, string> queryMap,
        string queryName)
    {
        var uniqueKey = KeyGenerator.GenerateUniqueKey(incomingField._effectiveName, updatedFields.Keys);

        var fieldToAdd = !string.Equals(uniqueKey, originalFieldKey, StringComparison.OrdinalIgnoreCase)
            ? incomingField with { Alias = uniqueKey, _effectiveName = uniqueKey}
            : incomingField;

        updatedFields[uniqueKey] = fieldToAdd;
        queryMap[queryName] = uniqueKey;
    }

    private static (string Key, FieldDefinition Field)? FindMergeTarget(Dictionary<string, FieldDefinition> existingFields, FieldDefinition incomingField)
    {
        foreach (var (key, existingField) in existingFields)
        {
            // First check if the root field names match
            if (!string.Equals(existingField.Name, incomingField.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip fields that came from NeverMerge queries - they should never be merge targets
            if (IsMarkedAsNeverMerge(existingField))
            {
                continue;
            }

            // Check if the field structures are compatible for merging
            if (FieldDefinitionExtensions.CanMergeFields(existingField, incomingField))
            {
                return (key, existingField);
            }
        }

        return null;
    }

    /// <summary>
    /// Marks a field as coming from a NeverMerge query.
    /// This prevents other queries from merging into this field.
    /// </summary>
    private static FieldDefinition MarkAsNeverMerge(FieldDefinition field)
        => field.IsNeverMerge ? field : field with { IsNeverMerge = true };

    /// <summary>
    /// Checks if a field is marked as coming from a NeverMerge query.
    /// </summary>
    private static bool IsMarkedAsNeverMerge(FieldDefinition field)
        => field.IsNeverMerge;
}
