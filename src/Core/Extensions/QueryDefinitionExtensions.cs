using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods for QueryDefinition.
/// </summary>
public static class QueryDefinitionExtensions
{
    /// <summary>
    /// Resolves a field by its dot-separated path.
    /// </summary>
    /// <param name="queryDefinition">The query definition to search in.</param>
    /// <param name="path">The dot-separated path to the field (e.g., "businessObjects.playerProfile.edges.node.ProfileData").</param>
    /// <returns>The field definition if found, null otherwise.</returns>
    public static FieldDefinition? ResolveFieldByPath(this QueryDefinition queryDefinition, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var segments = path.Split('.');
        var currentFields = queryDefinition.Fields;
        FieldDefinition? currentField = null;

        foreach (var segment in segments)
        {
            if (!currentFields.TryGetValue(segment, out currentField))
                return null;
            
            currentFields = currentField.Fields;
        }

        return currentField;
    }
}
