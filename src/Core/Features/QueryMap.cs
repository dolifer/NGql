using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;

namespace NGql.Core.Features;

/// <summary>
/// Manages query name mappings and provides path resolution functionality.
/// Maps original query names to their merged definition names and handles path navigation.
/// </summary>
public sealed class QueryMap
{
    private readonly Dictionary<string, string> _mappings = new();

    /// <summary>
    /// Gets the current mappings as a read-only dictionary.
    /// </summary>
    public IReadOnlyDictionary<string, string> Mappings => _mappings;

    /// <summary>
    /// Updates the mapping for a specific query name.
    /// </summary>
    /// <param name="queryName">The original query name</param>
    /// <param name="mappedPath">The mapped path</param>
    public void UpdateMapping(string queryName, string mappedPath)
    {
        _mappings[queryName] = mappedPath;
    }

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
    /// Gets the mapped path for a query name, or returns the original name if no mapping exists.
    /// </summary>
    /// <param name="queryName">The query name to look up</param>
    /// <returns>The mapped path or the original query name</returns>
    public string GetMappedPath(string queryName)
    {
        return _mappings.GetValueOrDefault(queryName, queryName);
    }

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
            var fieldKey = !string.IsNullOrEmpty(firstField.Alias) ? firstField.Alias : firstField.Name;
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
    public string[] GetPathTo(string queryName, string? nodePath, QueryDefinition queryDefinition)
    {
        // Get the root path for the query from QueryMap
        var rootPath = GetMappedPath(queryName);
        
        // If the query doesn't exist in our map, return empty array
        if (string.IsNullOrEmpty(rootPath))
        {
            return [];
        }

        // If no node path specified, return just the root
        if (string.IsNullOrEmpty(nodePath))
        {
            return [rootPath];
        }
        
        // Find the root field - first try by the mapped path (which might be an alias),
        // then try to find by matching alias or name
        FieldDefinition? rootField = null;
        
        // Try direct lookup first (in case rootPath is the actual field name)
        if (queryDefinition.Fields.TryGetValue(rootPath, out rootField))
        {
            // Found it directly
        }
        else
        {
            // rootPath might be an alias, so search for a field with that alias
            rootField = queryDefinition.Fields.Values.FirstOrDefault(f => 
                string.Equals(f.Alias, rootPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(f.Name, rootPath, StringComparison.OrdinalIgnoreCase));
        }
        
        if (rootField != null)
        {
            var nodeSegments = nodePath.Split('.');
            var targetNode = nodeSegments[^1]; // Last segment is what we're looking for
            
            // Search for the target node and return the path to its parent
            var path = FindPathToNode(rootField, targetNode, [rootPath]);
            if (path is { Length: > 1 })
            {
                // Return path excluding the target node itself (path to parent)
                return path.Take(path.Length - 1).ToArray();
            }
        }
        
        return [rootPath];
    }
    
    /// <summary>
    /// Recursively searches for a target node and returns the full path to it.
    /// </summary>
    /// <param name="field">The field to search within</param>
    /// <param name="targetNode">The target node name to find</param>
    /// <param name="currentPath">The current path segments</param>
    /// <returns>The full path to the target node, or null if not found</returns>
    private static string[]? FindPathToNode(FieldDefinition field, string targetNode, string[] currentPath)
    {
        // Check if any direct child matches the target node
        foreach (var childField in field.Fields.Values)
        {
            var fieldIdentifier = !string.IsNullOrEmpty(childField.Alias) ? childField.Alias : childField.Name;
            
            // Check if the target matches either the field name or the alias
            var isMatch = childField.Name.Equals(targetNode, StringComparison.OrdinalIgnoreCase) ||
                         (!string.IsNullOrEmpty(childField.Alias) && childField.Alias.Equals(targetNode, StringComparison.OrdinalIgnoreCase));
            
            if (isMatch)
            {
                // Found the target node, return the path including the target
                return currentPath.Concat([fieldIdentifier]).ToArray();
            }

            var childPath = FindPathToNode(childField, targetNode, currentPath.Concat([fieldIdentifier]).ToArray());
            if (childPath != null)
            {
                return childPath;
            }
        }

        return null;
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void Clear()
    {
        _mappings.Clear();
    }

    /// <summary>
    /// Gets the count of current mappings.
    /// </summary>
    public int Count => _mappings.Count;
}
