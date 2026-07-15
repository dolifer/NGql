using System.Reflection;
using NGql.Core.Caching;

namespace NGql.Core.Builders;

/// <summary>
/// Handles expansion of navigation properties (getter-only computed properties)
/// to their underlying settable properties.
/// </summary>
internal static class NavigationPropertyExpander
{
    /// <summary>
    /// Expands a field name if it's a navigation property (getter-only computed property).
    /// For navigation properties, returns all settable properties from the parameter type.
    /// Handles nested paths like "profile.name" by only checking the first segment.
    /// </summary>
    public static HashSet<string> ExpandNavigationProperty(string fieldName, Type? parameterType)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (parameterType == null)
        {
            result.Add(fieldName);
            return result;
        }

        try
        {
            // Handle nested paths - only check the first segment for navigation properties
            var (firstSegment, remainingPath) = SplitPath(fieldName);

            // Get the property from the type (only first segment) via cached metadata
            var property = TypeMetadataCache.GetNavigationProperties(parameterType).GetProperty(firstSegment);
            if (property == null)
            {
                result.Add(fieldName);
                return result;
            }

            // Check if this is a navigation property (getter-only, no setter)
            if (IsNavigationProperty(property))
            {
                ExpandNavigationPropertyFields(parameterType, remainingPath, result);
            }
            else
            {
                HandleRegularProperty(fieldName, firstSegment, remainingPath, property, result);
            }
        }
        catch (InvalidOperationException)
        {
            // Reflection failed due to ambiguous or invalid operation
            result.Add(fieldName);
        }
        catch (AmbiguousMatchException)
        {
            // Multiple matches found for property - use original field name
            result.Add(fieldName);
        }

        return result;
    }

    private static (string FirstSegment, string? RemainingPath) SplitPath(string fieldName)
    {
        var dotIndex = fieldName.IndexOf('.');
        if (dotIndex > 0)
        {
            return (fieldName.Substring(0, dotIndex), fieldName.Substring(dotIndex + 1));
        }
        return (fieldName, null);
    }

    private static bool IsNavigationProperty(PropertyInfo property)
    {
        // Properties surfaced by GetProperty(BindingFlags.Public | BindingFlags.Instance)
        // always have a public getter, so GetGetMethod() never returns null in this scope.
        return property.SetMethod == null && property.GetGetMethod()!.IsPublic;
    }

    private static void ExpandNavigationPropertyFields(Type parameterType, string? remainingPath, HashSet<string> result)
    {
        // This is a navigation property - get all SETTABLE properties
        foreach (var prop in TypeMetadataCache.GetNavigationProperties(parameterType).Properties)
        {
            // Skip navigation properties themselves (getter-only)
            if (prop.SetMethod != null)
            {
                // If there was a remaining path, append it to each expanded property
                if (remainingPath != null)
                {
                    result.Add($"{prop.Name}.{remainingPath}");
                }
                else
                {
                    result.Add(prop.Name);
                }
            }
        }
    }

    private static void HandleRegularProperty(
        string fieldName,
        string firstSegment,
        string? remainingPath,
        PropertyInfo property,
        HashSet<string> result)
    {
        // Not a navigation property - check if we need to recurse for nested path
        if (remainingPath != null && property.PropertyType != null)
        {
            // Recurse on the remaining path with the property's type
            var nestedExpanded = ExpandNavigationProperty(remainingPath, property.PropertyType);
            foreach (var nestedField in nestedExpanded)
            {
                result.Add($"{firstSegment}.{nestedField}");
            }
        }
        else
        {
            // Just return the field name
            result.Add(fieldName);
        }
    }
}
