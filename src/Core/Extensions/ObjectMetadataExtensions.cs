using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGql.Core.Extensions;

internal static class ObjectMetadataExtensions
{
    public static string? GetAlias(this PropertyInfo property)
    {
        // Check for DataMember attribute
        var dataMemberAttribute = property.GetCustomAttribute<DataMemberAttribute>();
        if (dataMemberAttribute != null)
        {
            return dataMemberAttribute.Name;
        }

        // Check for JsonProperty attribute
        var jsonPropertyAttribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyAttribute != null)
        {
            return jsonPropertyAttribute.Name;
        }
        
        return null;
    }
}
