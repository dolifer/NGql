using System.Runtime.CompilerServices;
using System.Text;
using NGql.Core.Abstractions;
using NGql.Core.Features;
using NGql.Core.Pooling;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods for QueryDefinition and field navigation/lookup utilities.
/// </summary>
internal static class QueryDefinitionExtensions
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
        IReadOnlyDictionary<string, FieldDefinition>? fields,
        ReadOnlySpan<char> path,
        out string? resolvedPath,
        string? prependPath = null)
    {
        resolvedPath = null;
        if (fields is null || path.Length == 0) return null;

        // Build the resolved path into a pooled builder while walking — replaces the prior
        // List<string> + string.Join + prepend-concat trio with a single final ToString.
        using var pooled = LockFreeStringBuilderPool.Get();
        var pathBuilder = pooled.StringBuilder;
        if (prependPath is not null) pathBuilder.Append(prependPath).Append('.');

        var currentField = WalkPath(fields, path, pathBuilder);
        if (currentField is null) return null;

        resolvedPath = pathBuilder.ToString();
        return currentField;
    }

    private static FieldDefinition? WalkPath(IReadOnlyDictionary<string, FieldDefinition> fields, ReadOnlySpan<char> path, StringBuilder resolvedPath)
    {
        var currentFields = fields;
        FieldDefinition? currentField = null;
        var firstSegment = true;

        while (path.Length > 0)
        {
            var dotIndex = path.IndexOf('.');
            var segment = dotIndex >= 0 ? path[..dotIndex] : path;

            var match = PreserveExtensions.FindFieldByNameOrAlias(currentFields, segment);
            if (!match.HasValue) return null;

            currentField = match.Value.Value;
            if (!firstSegment) resolvedPath.Append('.');
            firstSegment = false;
            resolvedPath.Append(match.Value.Key);

            if (dotIndex < 0) break;
            if (currentField._children is null) return null;

            currentFields = currentField.Fields;
            path = path[(dotIndex + 1)..];
        }

        return currentField;
    }

    /// <summary>
    /// Recursively searches for a field by name through all descendant nodes.
    /// Returns all matching paths.
    /// </summary>
    internal static List<string> FindFieldRecursively(
        IReadOnlyDictionary<string, FieldDefinition> fields,
        string fieldName,
        string basePath)
    {
        var results = new List<string>();
        FindFieldRecursivelyCore(fields, fieldName, basePath, results);
        return results;
    }

    private static void FindFieldRecursivelyCore(
        IReadOnlyDictionary<string, FieldDefinition> fields,
        string fieldName,
        string basePath,
        List<string> results)
    {
        foreach (var (key, fieldDef) in fields)
        {
            var currentPath = string.IsNullOrEmpty(basePath) ? key : $"{basePath}.{key}";

            if (NameOrAliasMatches(fieldDef, fieldName))
            {
                results.Add(currentPath);
            }

            if (fieldDef.HasFields)
            {
                FindFieldRecursivelyCore(fieldDef._children!, fieldName, currentPath, results);
            }
        }
    }

    private static bool NameOrAliasMatches(FieldDefinition field, string name)
    {
        if (string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrEmpty(field.Alias)) return false;
        return string.Equals(field.Alias, name, StringComparison.OrdinalIgnoreCase);
    }
}
