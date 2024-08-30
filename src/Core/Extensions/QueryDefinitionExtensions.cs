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
        => ApplyFieldDefinitions(queryDefinition.Name, queryDefinition.Fields.Values);

    private static Query ToQuery(FieldDefinition fieldDefinition)
        => ApplyFieldDefinitions(fieldDefinition.Name, fieldDefinition.Fields.Values);

    private static Query ApplyFieldDefinitions(string queryName, IEnumerable<FieldDefinition> fields)
    {
        var query = new Query(queryName);
        foreach (var field in fields)
        {
            switch (field.Fields.Count)
            {
                case > 0:
                    query.Select(ToQuery(field));
                    break;
                default:
                    query.Select(field.Name);
                    break;
            }
        }

        return query;
    }
}
