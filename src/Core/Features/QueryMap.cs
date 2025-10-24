using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Manages query name mappings and provides path resolution functionality.
/// Maps original query names to their merged definition names and handles path navigation.
/// </summary>
internal sealed class QueryMap
{
    private readonly Dictionary<string, string> _mappings = new();

    /// <summary>
    /// Updates multiple mappings at once.
    /// </summary>
    /// <param name="mappings">Dictionary of mappings to add or update</param>
    public void UpdateMappings(Dictionary<string, string> mappings)
    {
        foreach (var (key, value) in mappings)
        {
            _mappings[key] = value;
        }
    }

    /// <summary>
    /// Gets the mapped path for a query name or returns the original name if no mapping exists.
    /// </summary>
    /// <param name="queryName">The query name to look up</param>
    /// <returns>The mapped path or the original query name</returns>
    private string GetMappedPath(string queryName) => _mappings.GetValueOrDefault(queryName, queryName);

    /// <summary>
    /// Updates the QueryMap entry for the root query to point to its first field's alias/name.
    /// This is typically called after field modifications to maintain accurate mappings.
    /// </summary>
    /// <param name="definition">The query definition to update mapping for</param>
    public void UpdateRootMapping(QueryDefinition definition)
    {
        if (definition.Fields.Count > 0)
        {
            // Keep existing root mapping if it exists and points to a valid field
            if (_mappings.TryGetValue(definition.Name, out var existingPath) &&
                definition.Fields.ContainsKey(existingPath))
            {
                return; // Keep the existing valid mapping
            }

            // If we don't have a valid mapping, use the first field
            var firstField = definition.Fields.Values.First();
            var fieldKey = firstField.GetEffectiveName();
            _mappings[definition.Name] = fieldKey;
        }
    }

    /// <summary>
    /// Gets the path segments to reach a specific node within a query.
    /// </summary>
    /// <param name="queryName">The name of the query to find the path for.</param>
    /// <param name="nodePath">The optional node path within the query (e.g., "edges.node").</param>
    /// <param name="queryDefinition">The query definition to search within</param>
    /// <returns>An array of path segments to reach the specified node.</returns>
    internal string[] GetPathTo(string queryName, string? nodePath, QueryDefinition queryDefinition)
    {
        var rootPath = GetMappedPath(queryName);
        if (string.IsNullOrEmpty(rootPath))
        {
            return [];
        }

        if (string.IsNullOrEmpty(nodePath))
        {
            return [rootPath];
        }

        var rootField = FindRootField(queryDefinition, rootPath);
        return rootField == null ? [rootPath] : BuildPathToNode(rootField, nodePath);
    }

    private static FieldDefinition? FindRootField(QueryDefinition queryDefinition, string rootPath)
    {
        if (queryDefinition.Fields.TryGetValue(rootPath, out var field))
        {
            return field;
        }

        return queryDefinition.Fields.Values.FirstOrDefault(f =>
            string.Equals(f.Alias, rootPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(f.Name, rootPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string[] BuildPathToNode(FieldDefinition rootField, string nodePath)
    {
        var targetNode = GetTargetNodeName(nodePath);
        var initialPath = rootField.GetEffectiveName();

        // Use List for efficient path building without allocations on each recursion
        var pathBuilder = new List<string>(capacity: 8) { initialPath };

        if (FindPathToNodeOptimized(rootField, targetNode, pathBuilder))
        {
            // Remove the target node itself, keep only the path to it
            if (pathBuilder.Count > 1)
            {
                pathBuilder.RemoveAt(pathBuilder.Count - 1);
            }
            return pathBuilder.ToArray();
        }

        return [rootField.GetEffectiveName()];
    }

    private static string GetTargetNodeName(string nodePath)
    {
        var nodePathSpan = nodePath.AsSpan();
        var lastDotIndex = nodePathSpan.LastIndexOf('.');

        return lastDotIndex == -1
            ? nodePathSpan.ToString()
            : nodePathSpan[(lastDotIndex + 1)..].ToString();
    }

    /// <summary>
    /// Recursively searches for a target node and builds the path using a shared List.
    /// Uses backtracking to avoid allocating new arrays on each recursive call.
    /// </summary>
    /// <param name="field">The field to search within</param>
    /// <param name="targetNode">The target node name to find</param>
    /// <param name="pathBuilder">Shared list for building the path (modified in-place)</param>
    /// <returns>True if a target node was found, false otherwise</returns>
    private static bool FindPathToNodeOptimized(FieldDefinition field, string targetNode, List<string> pathBuilder)
    {
        // Check if any direct child matches the target node
        foreach (var childField in field.Fields.Values)
        {
            var fieldIdentifier = childField.GetEffectiveName();

            // Check if the target matches either the field name or the alias
            var isMatch = childField.Name.Equals(targetNode, StringComparison.OrdinalIgnoreCase) ||
                         (!string.IsNullOrEmpty(childField.Alias) && childField.Alias.Equals(targetNode, StringComparison.OrdinalIgnoreCase));

            if (isMatch)
            {
                // Found the target node, add it to a path
                pathBuilder.Add(fieldIdentifier);
                return true;
            }

            // Try searching in nested fields
            pathBuilder.Add(fieldIdentifier);
            if (FindPathToNodeOptimized(childField, targetNode, pathBuilder))
            {
                return true;
            }

            // Backtrack: remove this field from a path since the target wasn't found here
            pathBuilder.RemoveAt(pathBuilder.Count - 1);
        }

        return false;
    }
}
