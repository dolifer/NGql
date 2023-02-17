using System;
using System.Collections.Generic;
using NGql.Core.Abstractions;

namespace NGql.Core
{
    public sealed class Query
    {
        public QueryBlock Block { get; }

        public Query()
        {
            Block = new QueryBlock(string.Empty, string.Empty);
        }

        public Query(string name, string? alias = null, params Variable[] variables)
        {
            Block = new QueryBlock(name, "query", alias, variables);
        }

        /// <inheritdoc cref="QueryBlock.Name"/>
        public string Name => Block.Name;

        /// <inheritdoc cref="QueryBlock.Alias"/>
        public string? Alias => Block.Alias;

        /// <inheritdoc cref="QueryBlock.FieldsList"/>
        public IEnumerable<object> FieldsList => Block.FieldsList;

        /// <inheritdoc cref="QueryBlock.Arguments"/>
        public IReadOnlyDictionary<string, object> Arguments => Block.Arguments;

        /// <inheritdoc cref="QueryBlock.Variables"/>
        public IEnumerable<Variable> Variables => Block.Variables;

        /// <inheritdoc cref="QueryBlock.AddVariable(NGql.Core.Variable)"/>
        public Query Variable(Variable variable)
        {
            Block.AddVariable(variable);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddVariable(String,String)"/>
        public Query Variable(string name, string type)
        {
            Block.AddVariable(name, type);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddField(System.Collections.Generic.IEnumerable{object})"/>
        public Query Select(IEnumerable<object> selectList)
        {
            Block.AddField(selectList);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddField(string[])"/>
        public Query Select(params string[] selects)
        {
            Block.AddField(selects);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddField(QueryBlock)"/>
        public Query Select(Query subQuery)
        {
            Block.AddField(subQuery.Block);
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
            Block.AddField(query.Block);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddArgument(string,object)"/>
        public Query Where(string key, object where)
        {
            Block.AddArgument(key, where);
            return this;
        }

        /// <inheritdoc cref="QueryBlock.AddArgument(Dictionary&lt;string, object&gt;)"/>
        public Query Where(Dictionary<string, object> dict)
        {
            Block.AddArgument(dict);
            return this;
        }

        public override string ToString() => Block.ToString();
        public static implicit operator string(Query query) => query.Block.ToString();
    }
}
