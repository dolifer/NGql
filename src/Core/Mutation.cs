using System.Collections.Generic;
using NGql.Core.Abstractions;

namespace NGql.Core
{
    public sealed class Mutation : QueryBase
    {
        public Mutation(string name)
            : base(name, "mutation")
        {
        }

        /// <inheritdoc cref="QueryBase.AddField(System.Collections.Generic.IEnumerable{object})"/>
        public Mutation Select(IEnumerable<object> selectList)
        {
            AddField(selectList);
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddField(string[])"/>
        public Mutation Select(params string[] selects)
        {
            AddField(selects);
            return this;
        }

        /// <inheritdoc cref="QueryBase.AddField(QueryBase)"/>
        public Mutation Select(Query subQuery)
        {
            AddField(subQuery);
            return this;
        }
    }
}
