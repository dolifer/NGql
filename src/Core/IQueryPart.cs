using System.Collections.Generic;

namespace NGql.Core
{
    /// <summary>
    /// Represents the GraphQL query or mutation building block.
    /// Provides fluent-like methods to build the query.
    /// </summary>
    public interface IQueryPart
    {
        /// <summary>
        /// The list of fields to retrieve from GraphQL.
        /// </summary>
        List<object> FieldsList { get; }

        /// <summary>
        /// The collection of arguments related to <see cref="FieldsList"/>.
        /// </summary>
        Dictionary<string, object> Arguments { get; }

        /// <summary>
        /// The Query name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The Query alias.
        /// </summary>
        string? Alias { get; }

        /// <summary>
        /// Adds the given generic list to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <remarks>
        /// Accepts any type of list, but must contain one of supported types of data.
        /// </remarks>
        /// <param name="selectList">Generic list of select fields.</param>
        /// <returns>Query</returns>
        IQueryPart Select(IEnumerable<object> selectList);

        /// <summary>
        /// Adds the given list of strings to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <param name="selects">List of strings.</param>
        /// <returns>Query</returns>
        IQueryPart Select(params string[] selects);

        /// <summary>
        /// Adds the given sub query to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <param name="subQueryPart">A sub-query.</param>
        /// <returns>Query</returns>
        IQueryPart Select(IQueryPart subQueryPart);

        /// <summary>
        /// Adds the given key into <see cref="Arguments"/> part of the query.
        /// </summary>
        /// <param name="key">The Parameter Name</param>
        /// <param name="where">The value of the parameter, primitive or object</param>
        /// <returns></returns>
        IQueryPart Where(string key, object where);

        /// <summary>
        /// Add a dict of key value pairs &lt;string, object&gt; into <see cref="Arguments"/> part of the query.
        /// </summary>
        /// <param name="dict">An existing Dictionary that takes &lt;string, object&gt;</param>
        /// <returns>Query</returns>
        /// <throws>DuplicateKeyException and others</throws>
        IQueryPart Where(Dictionary<string, object> dict);

        /// <summary>
        /// Gets the string representation of the query.
        /// </summary>
        /// <returns>The GraphQL Query String</returns>
        /// <throws>ArgumentException</throws>
        string ToString();
    }
}
