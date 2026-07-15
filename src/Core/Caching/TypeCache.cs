using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NGql.Core.Caching;

/// <summary>
/// Simple cache for common GraphQL types with memory and CPU optimizations
/// </summary>
[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class TypeCache
{
    private static readonly ConcurrentDictionary<string, string> CustomTypes = new();

    // Pre-intern the most common GraphQL types (ordered by frequency)
    private static readonly string[] CommonTypes =
    [
        Constants.DefaultFieldType,    // "String" - most common
        "Int", "Boolean", "ID",        // Other common scalars
        Constants.ObjectFieldType,     // "object" - for nested fields
        "String!", "Int!", "Boolean!", // Non-null variants
        "Float", "Float!",             // Less common but still frequent
        Constants.ArrayTypeMarker      // "[]" - array marker
    ];

    // Pre-intern common nullable type patterns
    private static readonly string[] CommonNullableTypes =
    [
        "String?", "Int?", "Boolean?", "ID?", "Float?"
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetInternedType(ReadOnlySpan<char> type)
    {
        // ULTRA FAST PATH: Empty type defaults to String
        if (type.IsEmpty)
        {
            return Constants.DefaultFieldType;
        }

        // OPTIMIZED PATH: Single combined check for both common and nullable types
        // This eliminates the O(n+m) double-loop pattern and does a single pass
        var allTypes = CombinedCommonTypes.Value;
        foreach (var commonType in allTypes)
        {
            if (type.SequenceEqual(commonType.AsSpan()))
            {
                return commonType; // Already interned
            }
        }

        // Standard path for other types
        var typeString = type.ToString();
        return CustomTypes.GetOrAdd(typeString, typeString);
    }

    // Lazy-initialized combined array to avoid allocation at static init time
    private static readonly Lazy<string[]> CombinedCommonTypes = new(() =>
    {
        var combined = new string[CommonTypes.Length + CommonNullableTypes.Length];
        CommonTypes.CopyTo(combined, 0);
        CommonNullableTypes.CopyTo(combined, CommonTypes.Length);
        return combined;
    });

    /// <summary>
    /// Interns a type string for memory efficiency - alias for GetInternedType
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string InternType(ReadOnlySpan<char> type) => GetInternedType(type);
}

/// <summary>
/// Caches reflection metadata for type introspection
/// </summary>
internal static class TypeMetadataCache
{
    /// <summary>
    /// Caches PropertyInfo pairs (Key, Value) for KeyValuePair&lt;,&gt; generic types.
    /// Caller must guarantee the cached type is a closed KeyValuePair&lt;TKey,TValue&gt; — those
    /// always expose Key and Value properties, so the cached pair is non-nullable.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, (PropertyInfo Key, PropertyInfo Value)> KvpPropertyCache = new();

    /// <summary>
    /// Caches PropertyInfo[] per object type for the default WriteObject reflection branch.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, PropertyInfo[]> ObjectPropertyCache = new();

    /// <summary>
    /// Caches the public-instance property metadata used by navigation-property expansion, so
    /// repeated <c>PreserveFromExpression&lt;T&gt;</c> calls don't re-walk the same type. The entry
    /// pairs the full <see cref="PropertyInfo"/> array (mirrors
    /// <c>GetProperties(Public | Instance)</c>) with a case-sensitive name lookup that reproduces
    /// <c>GetProperty(name, Public | Instance)</c> semantics, including the ambiguous-match throw
    /// when a name is shadowed by a <c>new</c> property.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, NavigationPropertyMetadata> NavigationPropertyCache = new();

    /// <summary>
    /// Returns the cached navigation-property metadata for <paramref name="type"/>, building it on
    /// first access via <c>GetProperties(Public | Instance)</c>.
    /// </summary>
    internal static NavigationPropertyMetadata GetNavigationProperties(Type type)
        => NavigationPropertyCache.GetOrAdd(type, static t => NavigationPropertyMetadata.Build(t));
}

/// <summary>
/// Immutable per-type snapshot of public-instance properties used by navigation-property expansion.
/// </summary>
internal sealed class NavigationPropertyMetadata
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    // Names shadowed by a `new` property map to null, matching Type.GetProperty's ambiguous throw.
    private readonly Dictionary<string, PropertyInfo?> _byName;

    private NavigationPropertyMetadata(PropertyInfo[] properties, Dictionary<string, PropertyInfo?> byName)
    {
        Properties = properties;
        _byName = byName;
    }

    /// <summary>All public instance properties, mirroring <c>GetProperties(Public | Instance)</c>.</summary>
    public PropertyInfo[] Properties { get; }

    internal static NavigationPropertyMetadata Build(Type type)
    {
        var properties = type.GetProperties(PublicInstance);
        var byName = new Dictionary<string, PropertyInfo?>(properties.Length, StringComparer.Ordinal);

#pragma warning disable S3267
        foreach (var property in properties)
        {
            // A duplicate name means the property is shadowed (e.g. `new`); Type.GetProperty throws
            // AmbiguousMatchException for those, so mark the slot as ambiguous with a null sentinel.
            if (!byName.TryAdd(property.Name, property))
            {
                byName[property.Name] = null;
            }
        }
#pragma warning restore S3267

        return new NavigationPropertyMetadata(properties, byName);
    }

    /// <summary>
    /// Case-sensitive lookup reproducing <c>GetProperty(name, Public | Instance)</c>: returns null
    /// when no property matches and throws <see cref="AmbiguousMatchException"/> when the name is
    /// shadowed.
    /// </summary>
    public PropertyInfo? GetProperty(string name)
    {
        if (!_byName.TryGetValue(name, out var property))
        {
            return null;
        }

        if (property == null)
        {
            throw new AmbiguousMatchException();
        }

        return property;
    }
}
