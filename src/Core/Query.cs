using System.Text;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core;

/// <summary>
/// Classic-API query builder. Compose with <c>.Where(...)</c> for arguments and
/// <c>.Select(...)</c> for fields; nest queries by passing a <see cref="Query"/> to a parent's
/// <c>Select</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>New code should prefer <see cref="NGql.Core.Builders.QueryBuilder.CreateDefaultBuilder(string)"/>.</b>
/// The fluent <see cref="NGql.Core.Builders.QueryBuilder"/> surface is the recommended way to author
/// queries in NGql 2.x and beyond — it offers a richer API (<c>Include</c>, <c>WithMetadata</c>,
/// sub-field lambdas, <c>PreservationBuilder</c> support, dot-path field composition) over the
/// classic <c>Where</c> / <c>Select</c> idiom.
/// </para>
/// <para>
/// <see cref="Query"/> remains supported for backwards compatibility with NGql 1.x call sites and
/// continues to render the same GraphQL output. It is also the only way to attach arguments to
/// the root field of a <see cref="Mutation"/> built via the classic API; new mutation code should
/// use <see cref="NGql.Core.Builders.QueryBuilder.CreateMutationBuilder(string)"/> instead, which
/// avoids that round-trip. There is currently no removal timeline for <see cref="Query"/>.
/// </para>
/// </remarks>
public sealed class Query
{
    public QueryBlock Block { get; }

    public Query()
    {
        Block = new QueryBlock(string.Empty, string.Empty);
    }

    public Query(string name, params Variable[] variables)
    {
        Block = new QueryBlock(name, "query", null, variables);
    }

    public Query(string name, string? alias, params Variable[] variables)
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

    /// <inheritdoc cref="QueryBlockObjectExtensions.IncludeAtPath{T}"/>
    public Query IncludeAtPath<T>(string path, string name, string? alias = null)
    {
        Block.IncludeAtPath<T>(path, name, alias);
        return this;
    }

    /// <inheritdoc cref="QueryBlockObjectExtensions.Include{T}"/>
    public Query Include<T>(string name, string? alias = null)
    {
        Block.Include<T>(name, alias);
        return this;
    }

    /// <inheritdoc cref="QueryBlockObjectExtensions.Include"/>
    public Query Include(object obj)
    {
        Block.Include(obj);
        return this;
    }

    /// <summary>
    /// Adds the given sub query to the <see cref="QueryBlock.FieldsList"/> part of the query.
    /// </summary>
    /// <param name="name">A sub-query name.</param>
    /// <param name="alias">A sub-query alias.</param>
    /// <param name="action">Action to build subquery.</param>
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

    /// <inheritdoc cref="QueryBlock.AddArgument(IReadOnlyDictionary&lt;string, object&gt;)"/>
    public Query Where(IReadOnlyDictionary<string, object> dict)
    {
        Block.AddArgument(dict);
        return this;
    }

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => Block.ToString();

    /// <inheritdoc cref="QueryBlock.AppendTo(System.Text.StringBuilder)"/>
    public void AppendTo(StringBuilder builder) => Block.AppendTo(builder);

    /// <inheritdoc cref="QueryBlock.WriteTo(System.IO.TextWriter)"/>
    public void WriteTo(TextWriter writer) => Block.WriteTo(writer);

    public static implicit operator string(Query query) => query.Block.ToString();
}
