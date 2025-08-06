using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Builders;
using NGql.Core.Extensions;

namespace NGql.Core.Abstractions;

/// <summary>
/// Represents a GraphQL Query Block.
/// </summary>
public sealed class QueryBlock
{
    private readonly string _prefix;
    private readonly SortedDictionary<string, object> _arguments;
    private readonly SortedSet<Variable> _variables;
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
    /// The collection of variables related to <see cref="FieldsList"/> or <see cref="Arguments"/>.
    /// </summary>
    public IReadOnlyCollection<Variable> Variables => _variables;

    /// <summary>
    /// The Query name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The Query alias.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Indicates if the query is empty.
    /// </summary>
    internal bool IsEmpty = false;

    /// <summary>
    /// Adds the variable with give name into <see cref="Variables"/> part of the query.
    /// </summary>
    /// <param name="variable">The variable</param>
    public void AddVariable(Variable variable)
        => HandleAddVariable(variable);

    /// <summary>
    /// Adds the variable with give name into <see cref="Variables"/> part of the query.
    /// </summary>
    /// <param name="name">The variable name</param>
    /// <param name="type">The value of the variable</param>
    public void AddVariable(string name, string type)
        => HandleAddVariable(new Variable(name, type));

    /// <summary>
    /// Adds the given generic list to the <see cref="FieldsList"/> part of the query.
    /// </summary>
    /// <remarks>
    /// Accepts any type of list, but must contain one of supported types of data.
    /// </remarks>
    /// <param name="selectList">Generic list of select fields.</param>
    public void AddField(IEnumerable<object> selectList)
        => HandleAddField(selectList);

    /// <summary>
    /// Adds the given list of strings to the <see cref="FieldsList"/> part of the query.
    /// </summary>
    /// <param name="selects">List of strings.</param>
    /// <returns>Query</returns>
    public void AddField(params string[] selects)
        => HandleAddField(selects);

    /// <summary>
    /// Adds the given sub query to the <see cref="FieldsList"/> part of the query.
    /// </summary>
    /// <param name="subQuery">A sub-query.</param>
    /// <returns>Query</returns>
    public void AddField(QueryBlock subQuery)
        => HandleAddField(subQuery);

    /// <summary>
    /// Adds the given key into <see cref="Arguments"/> part of the query.
    /// </summary>
    /// <param name="key">The Parameter Name</param>
    /// <param name="where">The value of the parameter, primitive or object</param>
    /// <returns></returns>
    public void AddArgument(string key, object where)
        => HandleAddArgument(key, where);

    /// <summary>
    /// Add a dict of key value pairs &lt;string, object&gt; into <see cref="Arguments"/> part of the query.
    /// </summary>
    /// <param name="dict">An existing Dictionary that takes &lt;string, object&gt;</param>
    /// <returns>Query</returns>
    /// <throws>DuplicateKeyException and others</throws>
    public void AddArgument(IReadOnlyDictionary<string, object> dict)
    {
        foreach (var (key, value) in dict)
            HandleAddArgument(key, value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryBlock"/> class.
    /// </summary>
    public QueryBlock(string name, string prefix = "", string? alias = null, params Variable[]? variables)
    {
        _prefix = prefix;
        Name = name;
        Alias = alias;

        _fieldsList = new List<object>();
        _variables = variables is null ? [] : [.. variables.DistinctBy(x => x.Name)];
        _arguments = new SortedDictionary<string, object>();
    }

    /// <summary>
    /// Gets the string representation of the query.
    /// </summary>
    /// <returns>The GraphQL Query String</returns>
    /// <throws>ArgumentException</throws>
    public override string ToString() => new QueryTextBuilder().Build(this, prefix: _prefix);

    private void HandleAddField(object value)
    {
        if (value is QueryBlock subQuery)
        {
            foreach (var variable in subQuery.Variables)
            {
                _variables.Add(variable);
            }

            _fieldsList.Add(subQuery);

            return;
        }

        if (value is string field)
        {
            if (string.IsNullOrWhiteSpace(field))
            {
                return;
            }

            // Add string field in sorted order
            var insertIndex = _fieldsList.OfType<string>()
                .TakeWhile(existing => string.Compare(existing, field, StringComparison.OrdinalIgnoreCase) < 0)
                .Count();
           
            _fieldsList.Insert(insertIndex, field);
            return;
        }

        if (value is IList list)
        {
            // Sort the list items before adding them
            var sortedItems = list.Cast<object>()
                .OrderBy(x => x switch
                {
                    string s => s,
                    QueryBlock q => q.Name,
                    _ => x.ToString()
                })
                .ToList();
            
            foreach (var item in sortedItems)
            {
                HandleAddField(item);
            }

            return;
        }

        throw new InvalidOperationException("Unsupported Field type found, must be a `string` or `QueryBlock`");
    }

    private void HandleAddVariable(Variable variable)
    {
        _variables.Add(variable);
    }

    private void HandleAddArgument(string key, object value)
    {
        Helpers.ExtractVariablesFromValue(value, _variables);
        var sortedValue = Helpers.SortArgumentValue(value);
        
        _arguments[key] = sortedValue;
    }
}
