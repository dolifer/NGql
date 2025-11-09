using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Features;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods for QueryDefinition and field navigation/lookup utilities.
/// </summary>
public static class QueryDefinitionExtensions
{
    /// <summary>
    /// Navigates through a dot-separated path using name or alias matching.
    /// Returns the final field and optionally builds the resolved path.
    /// Optimized with ReadOnlySpan to avoid allocations.
    /// </summary>
    /// <param name="fields">Starting field collection</param>
    /// <param name="path">Dot-separated path (can use ReadOnlySpan for zero-alloc)</param>
    /// <param name="resolvedPath">Optional: outputs the actual path with field keys (not aliases)</param>
    /// <param name="prependPath">Optional: path to prepend to resolvedPath</param>
    /// <returns>The final field definition, or null if not found</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition? NavigatePath(
        SortedDictionary<string, FieldDefinition>? fields,
        ReadOnlySpan<char> path,
        out string? resolvedPath,
        string? prependPath = null)
    {
        if (fields == null || path.Length == 0)
        {
            resolvedPath = null;
            return null;
        }
        
        var currentFields = fields;
        FieldDefinition? currentField = null;
        List<string>? pathSegments = null;

        while (path.Length > 0)
        {
            var dotIndex = path.IndexOf('.');
            var segment = dotIndex >= 0 ? path[..dotIndex] : path;

            var match = PreserveExtensions.FindFieldByNameOrAlias(currentFields, segment);
            if (!match.HasValue)
            {
                resolvedPath = null;
                return null;
            }

            currentField = match.Value.Value;
            pathSegments ??= new List<string>();
            pathSegments.Add(match.Value.Key);

            if (dotIndex < 0)
                break; // Last segment

            // Navigate to next level (but allow last segment to be a leaf)
            if (currentField._fields == null)
            {
                resolvedPath = null;
                return null;
            }

            currentFields = currentField.Fields;
            path = path[(dotIndex + 1)..];
        }

        if (pathSegments == null)
        {
            resolvedPath = null;
        }
        else
        {
            var joinedPath = string.Join(".", pathSegments);
            resolvedPath = prependPath != null
                ? $"{prependPath}.{joinedPath}"
                : joinedPath;
        }

        return currentField;
    }

    /// <summary>
    /// Recursively searches for a field by name through all descendant nodes.
    /// Returns all matching paths.
    /// </summary>
    internal static List<string> FindFieldRecursively(
        SortedDictionary<string, FieldDefinition> fields,
        string fieldName,
        string basePath)
    {
        var results = new List<string>();
        FindFieldRecursivelyCore(fields, fieldName, basePath, results);
        return results;
    }

    private static void FindFieldRecursivelyCore(
        SortedDictionary<string, FieldDefinition> fields,
        string fieldName,
        string basePath,
        List<string> results)
    {
        foreach (var (key, fieldDef) in fields)
        {
            var currentPath = string.IsNullOrEmpty(basePath) ? key : $"{basePath}.{key}";

            // Check if this field matches by name or alias
            if (string.Equals(fieldDef.Name, fieldName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(fieldDef.Alias) && string.Equals(fieldDef.Alias, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(currentPath);
            }

            // Recursively search child fields
            if (fieldDef.HasFields)
            {
                FindFieldRecursivelyCore(fieldDef._fields!, fieldName, currentPath, results);
            }
        }
    }
}
