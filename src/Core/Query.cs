using System;
using System.Collections.Generic;
using NGql.Core.Abstractions;

namespace NGql.Core
{
    public readonly struct Query
    {
        internal readonly QueryBlock _block;

        public Query(string name, string? alias = null)
            => _block = new QueryBlock(name, "query", alias);

        /// <inheritdoc cref="QueryBlock.Name"/>
        public string Name => _block.Name;

        /// <inheritdoc cref="QueryBlock.Alias"/>
        public string? Alias => _block.Alias;

        /// <inheritdoc cref="QueryBlock.FieldsList"/>
        public IEnumerable<object> FieldsList => _block.FieldsList;

        /// <inheritdoc cref="QueryBlock.Arguments"/>
        public IReadOnlyDictionary<string, object> Arguments => _block.Arguments;

        /// <inheritdoc cref="QueryBlock.Variables"/>
        public IEnumerable<Variable> Variables => _block.Variables;

        /// <inheritdoc cref="QueryBlock.AddVariable"/>
        public Query Variable(string name, string type)
        {
            _block.AddVariable(name, type);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddField(System.Collections.Generic.IEnumerable{object})"/>
        public Query Select(IEnumerable<object> selectList)
        {
            _block.AddField(selectList);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddField(string[])"/>
        public Query Select(params string[] selects)
        {
            _block.AddField(selects);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddField(QueryBlock)"/>
        public Query Select(Query subQuery)
        {
            _block.AddField(subQuery._block);
            return this;
        }

        /// <summary>
        /// Adds the given sub query to the <see cref="QueryBlock.FieldsList"/> part of the query.
        /// </summary>
        /// <param name="name">A sub-query name.</param>
        /// <param name="alias">A sub-query alias.</param>
        /// <param name="action">Action to build sub-query.</param>
        /// <returns>Query</returns>
        public Query Include(string name, Action<Query> action, string? alias = null)
        {
            var query = new Query(name, alias);
            action.Invoke(query);
            _block.AddField(query._block);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddArgument(string,object)"/>
        public Query Where(string key, object where)
        {
            _block.AddArgument(key, where);
           return this;
        }

        /// <inheritdoc cref="QueryBlock.AddArgument(Dictionary&lt;string, object&gt;)"/>
        public Query Where(Dictionary<string, object> dict)
        {
            _block.AddArgument(dict);
            return this;
        }

        public override string ToString() => _block.ToString();
        public static implicit operator string(Query query) => query._block.ToString();
    }
}
