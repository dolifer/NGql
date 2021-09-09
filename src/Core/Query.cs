using System;
using System.Collections.Generic;
using NGql.Core.Abstractions;

namespace NGql.Core
{
    public sealed class Query : QueryBase
    {
        public Query(string name, string? alias = null)
            : base(name, "query", alias)
        {
        }

        /// <summary>
        /// Sets the Query alias.
        /// </summary>
        /// <param name="alias"></param>
        /// <returns></returns>
        public Query AliasAs(string? alias)
        {
            Alias = alias;
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddVariable"/>
        public Query Variable(string name, string type)
        {
            AddVariable(name, type);
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddField(System.Collections.Generic.IEnumerable{object})"/>
        public Query Select(IEnumerable<object> selectList)
        {
            AddField(selectList);
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddField(string[])"/>
        public Query Select(params string[] selects)
        {
            AddField(selects);
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddField(QueryBase)"/>
        public Query Select(Query subQuery)
        {
            AddField(subQuery);
            return this;
        }

        /// <summary>
        /// Adds the given sub query to the <see cref="QueryBase.FieldsList"/> part of the query.
        /// </summary>
        /// <param name="name">A sub-query name.</param>
        /// <param name="action">Action to build sub-query.</param>
        /// <returns>Query</returns>
        public Query Include(string name, Action<Query> action)
        {
            var query = new Query(name);
            action.Invoke(query);
            AddField(query);
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddArgument(string,object)"/>
        public Query Where(string key, object where)
        {
           AddArgument(key, where);
           return this;
        }

        /// <inheritdoc cref="QueryBase.AddArgument(Dictionary&lt;string, object&gt;)"/>
        public Query Where(Dictionary<string, object> dict)
        {
            AddArgument(dict);
            return this;
        }
    }
}
