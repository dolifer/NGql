using System.Collections.Generic;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

public static class QueryDefinitionExtensions
{
    /// <summary>
    ///     Converts the <see cref="QueryDefinition"/> to a <see cref="Query"/>.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <returns></returns>
    public static Query ToQuery(this QueryDefinition queryDefinition)
        => ApplyFieldDefinitions(queryDefinition.Name, queryDefinition.Alias, queryDefinition.Fields.Values);

    private static Query ToQuery(FieldDefinition fieldDefinition)
        => ApplyFieldDefinitions(fieldDefinition.Name, fieldDefinition.Alias, fieldDefinition.Fields.Values, fieldDefinition.Arguments);

    private static Query ApplyFieldDefinitions(string queryName, string? alias, IEnumerable<FieldDefinition> fields, Dictionary<string, object>? arguments = null)
    {
        var query = new Query(queryName, alias);
        
        if (arguments is not null)
        {
            query.Where(arguments);
        }

        foreach (var field in fields)
        {
            switch (field.Fields.Count)
            {
                case > 0:
                    query.Select(ToQuery(field));
                    break;
                default:
                    var valueToSelect = field.Alias is not null ? $"{field.Alias}:{field.Name}" : field.Name;
                    query.Select(valueToSelect);
                    break;
            }
        }

        return query;
    }
}
