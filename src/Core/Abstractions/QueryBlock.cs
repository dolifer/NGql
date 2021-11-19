using System.Collections.Generic;

namespace NGql.Core.Abstractions
{
    public sealed class QueryBlock
    {
        private readonly string _prefix;
        private readonly Dictionary<string, object> _arguments;
        private readonly List<Variable> _variables;
        private readonly List<object> _fieldsList;

        /// <summary>
        /// The list of fields to retrieve from GraphQL.
        /// </summary>
        public IReadOnlyList<object> FieldsList => _fieldsList;

        /// <summary>
        /// The collection of arguments related to <see cref="FieldsList"/>.
        /// </summary>
        public IReadOnlyDictionary<string, object> Arguments => _arguments;

        /// <summary>
        /// The collection of variables related to <see cref="FieldsList"/>.
        /// </summary>
        public IReadOnlyList<Variable> Variables => _variables;

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
        /// <param name="variable">The variable</param>
        public void AddVariable(Variable variable)
            => _variables.Add(variable);

        /// <summary>
        /// Adds the variable with give name into <see cref="Variables"/> part of the query.
        /// </summary>
        /// <param name="name">The variable name</param>
        /// <param name="type">The value of the variable</param>
        public void AddVariable(string name, string type)
            => _variables.Add(new Variable(name, type));

        /// <summary>
        /// Adds the given generic list to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <remarks>
        /// Accepts any type of list, but must contain one of supported types of data.
        /// </remarks>
        /// <param name="selectList">Generic list of select fields.</param>
        public void AddField(IEnumerable<object> selectList)
            => _fieldsList.AddRange(selectList);

        /// <summary>
        /// Adds the given list of strings to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <param name="selects">List of strings.</param>
        /// <returns>Query</returns>
        public void AddField(params string[] selects)
            => _fieldsList.AddRange(selects);

        /// <summary>
        /// Adds the given sub query to the <see cref="FieldsList"/> part of the query.
        /// </summary>
        /// <param name="subQuery">A sub-query.</param>
        /// <returns>Query</returns>
        public void AddField(QueryBlock subQuery)
            => _fieldsList.Add(subQuery);

        /// <summary>
        /// Adds the given key into <see cref="Arguments"/> part of the query.
        /// </summary>
        /// <param name="key">The Parameter Name</param>
        /// <param name="where">The value of the parameter, primitive or object</param>
        /// <returns></returns>
        public void AddArgument(string key, object where)
            => _arguments.Add(key, where);

        /// <summary>
        /// Add a dict of key value pairs &lt;string, object&gt; into <see cref="Arguments"/> part of the query.
        /// </summary>
        /// <param name="dict">An existing Dictionary that takes &lt;string, object&gt;</param>
        /// <returns>Query</returns>
        /// <throws>DuplicateKeyException and others</throws>
        public void AddArgument(Dictionary<string, object> dict)
        {
            foreach (var (key, value) in dict)
                _arguments.Add(key, value);
        }

        public QueryBlock(string name, string prefix, string? alias = null, params Variable[]? variables)
        {
            _prefix = prefix;
            Name = name;
            Alias = alias;

            _fieldsList = new List<object>();
            _variables = variables is null ? new List<Variable>() : new List<Variable>(variables);
            _arguments = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets the string representation of the query.
        /// </summary>
        /// <returns>The GraphQL Query String</returns>
        /// <throws>ArgumentException</throws>
        public override string ToString() => new QueryTextBuilder().Build(this, prefix: _prefix);
    }
}
