using NGql.Core.Abstractions;

namespace NGql.Core;

/// <summary>
/// Builds a GraphQL <c>mutation</c> operation. Use <see cref="Select(NGql.Core.Query)"/> to
/// embed a sub-<see cref="Query"/> that carries arguments via <see cref="Query.Where(string, object)"/>,
/// or <see cref="Select(string[])"/> for plain field names.
/// </summary>
/// <remarks>
/// <para>
/// <b>New code should prefer <see cref="Builders.QueryBuilder.CreateMutationBuilder(string)"/>.</b>
/// The fluent <see cref="Builders.QueryBuilder"/> surface is the recommended way to author both
/// queries and mutations in NGql 2.x and beyond — it offers a richer API (<c>Include</c>,
/// <c>WithMetadata</c>, sub-field lambdas, <c>PreservationBuilder</c> support) and avoids the
/// classic <see cref="Query"/>+<c>Where</c> idiom required by <see cref="Mutation"/> for
/// argument-bearing root fields.
/// </para>
/// <para>
/// <see cref="Mutation"/> remains supported for backwards compatibility with NGql 1.x call sites
/// and continues to render the same GraphQL output. There is currently no removal timeline.
/// </para>
/// </remarks>
/// <param name="name">Operation name (rendered as <c>mutation Name(...)</c>).</param>
/// <param name="variables">Operation variables; their <c>$name:Type</c> declarations appear in the operation signature.</param>
public sealed class Mutation(string name, params Variable[] variables)
{
    private readonly QueryBlock _block = new(name, "mutation", variables: variables);

    /// <inheritdoc cref="QueryBlock.Name"/>
    public string Name => _block.Name;

    /// <inheritdoc cref="QueryBlock.FieldsList"/>
    public IEnumerable<object> FieldsList => _block.FieldsList;

    /// <inheritdoc cref="QueryBlock.Variables"/>
    public IEnumerable<Variable> Variables => _block.Variables;

    /// <inheritdoc cref="QueryBlock.AddVariable(NGql.Core.Variable)"/>
    public Mutation Variable(Variable variable)
    {
        _block.AddVariable(variable);
        return this;
    }

    /// <inheritdoc cref="QueryBlock.AddVariable(string,string)"/>
    public Mutation Variable(string name, string type)
    {
        _block.AddVariable(name, type);
        return this;
    }

    /// <inheritdoc cref="QueryBlock.AddField(System.Collections.Generic.IEnumerable{object})"/>
    public Mutation Select(IEnumerable<object> selectList)
    {
        _block.AddField(selectList);
        return this;
    }

    /// <inheritdoc cref="QueryBlock.AddField(string[])"/>
    public Mutation Select(params string[] selects)
    {
        _block.AddField(selects);
        return this;
    }

    /// <inheritdoc cref="QueryBlock.AddField(QueryBlock)"/>
    public Mutation Select(Query subQuery)
    {
        _block.AddField(subQuery.Block);
        return this;
    }

    public override string ToString() => _block.ToString();
    public static implicit operator string(Mutation mutation) => mutation._block.ToString();
}
