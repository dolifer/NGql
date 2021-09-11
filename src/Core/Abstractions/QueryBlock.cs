using System.Collections.Generic;

namespace NGql.Core.Abstractions
{
    public readonly struct QueryBlock
    {
        private readonly string _prefix;

        /// <summary>
        /// The list of fields to retrieve from GraphQL.
        /// </summary>
        public List<object> FieldsList { get; }

        /// <summary>
        /// The collection of arguments related to <see cref="FieldsList"/>.
        /// </summary>
        public Dictionary<string, object> Arguments { get; }

        /// <summary>
        /// The collection of variables related to <see cref="FieldsList"/>.
        /// </summary>
        public List<Variable> Variables { get; }

        /// <summary>
        /// The Query name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Query alias.
        /// </summary>
        public string? Alias { get; }

        /// <summary>
        /// Adds the variable with give name into <see cref="Variables"/> part of the query.
        /// </summary>
        /// <param name="name">The variable name</param>
        /// <param name="type">The value of the variable</param>
        public void AddVariable(string name, string type)
            => Variables.Add(new Variable(name, type));

        /// <summary>
        /// Adds the given generic list to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <remarks>
        /// Accepts any type of list, but must contain one of supported types of data.
        /// </remarks>
        /// <param name="selectList">Generic list of select fields.</param>
        public void AddField(IEnumerable<object> selectList)
            => FieldsList.AddRange(selectList);

        /// <summary>
        /// Adds the given list of strings to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <param name="selects">List of strings.</param>
        /// <returns>Query</returns>
        public void AddField(params string[] selects)
            => FieldsList.AddRange(selects);

        /// <summary>
        /// Adds the given sub query to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <param name="subQuery">A sub-query.</param>
        /// <returns>Query</returns>
        public void AddField(QueryBlock subQuery)
            => FieldsList.Add(subQuery);

        /// <summary>
        /// Adds the given key into <see cref="Arguments"/> part of the query.
        /// </summary>
        /// <param name="key">The Parameter Name</param>
        /// <param name="where">The value of the parameter, primitive or object</param>
        /// <returns></returns>
        public void AddArgument(string key, object where)
            => Arguments.Add(key, where);

        /// <summary>
        /// Add a dict of key value pairs &lt;string, object&gt; into <see cref="Arguments"/> part of the query.
        /// </summary>
        /// <param name="dict">An existing Dictionary that takes &lt;string, object&gt;</param>
        /// <returns>Query</returns>
        /// <throws>DuplicateKeyException and others</throws>
        public void AddArgument(Dictionary<string, object> dict)
        {
            foreach (var (key, value) in dict)
                Arguments.Add(key, value);
        }

        public QueryBlock(string name, string prefix, string? alias = null)
        {
            _prefix = prefix;
            Name = name;
            Alias = alias;

            FieldsList = new List<object>();
            Variables = new List<Variable>();
            Arguments = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets the string representation of the query.
        /// </summary>
        /// <returns>The GraphQL Query String</returns>
        /// <throws>ArgumentException</throws>
        public override string ToString() => new QueryTextBuilder().Build(this, prefix: _prefix);
    }
}
