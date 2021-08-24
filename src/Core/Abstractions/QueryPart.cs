using System.Collections.Generic;

namespace NGql.Core.Abstractions
{
    public abstract class QueryPart : IQueryPart
    {
        private readonly QueryTextBuilder _queryTextBuilder = new();

        public List<object> FieldsList { get; } = new();
        public Dictionary<string, object> Arguments { get; } = new();
        public string Name { get; }
        public string? Alias { get; protected set; }

        protected virtual string Prefix => string.Empty;

        public IQueryPart Select(IEnumerable<object> selectList)
        {
            FieldsList.AddRange(selectList);
            return this;
        }

        public IQueryPart Select(params string[] selects)
        {
            FieldsList.AddRange(selects);
            return this;
        }

        public IQueryPart Select(IQueryPart subQueryPart)
        {
            FieldsList.Add(subQueryPart);
            return this;
        }

        public IQueryPart Where(string key, object where)
        {
            Arguments.Add(key, where);
            return this;
        }

        public IQueryPart Where(Dictionary<string, object> dict)
        {
            foreach (var (key, value) in dict)
                Arguments.Add(key, value);

            return this;
        }

        protected QueryPart(string name, string? alias = null)
        {
            Name = name;
            Alias = alias;
        }

        public override string ToString() => _queryTextBuilder.Build(this, prefix: Prefix);
    }
}
