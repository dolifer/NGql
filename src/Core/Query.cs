using NGql.Core.Abstractions;

namespace NGql.Core
{
    public sealed class Query : QueryPart
    {
        public Query(string name, string? alias = null)
            : base(name, alias)
        {
        }

        public IQueryPart AliasAs(string? alias)
        {
            Alias = alias;
            return this;
        }

        protected override string Prefix => "query";
    }
}
