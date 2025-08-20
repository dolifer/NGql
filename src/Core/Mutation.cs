using System.Collections.Generic;
using NGql.Core.Abstractions;

namespace NGql.Core;

public sealed class Mutation
{
    private readonly QueryBlock _block;

    public Mutation(string name, params Variable[] variables)
    {
        _block = new QueryBlock(name, "mutation", variables: variables);
    }

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
