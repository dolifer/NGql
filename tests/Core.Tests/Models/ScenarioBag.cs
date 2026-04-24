using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace NGql.Core.Tests.Models;

public abstract class ScenarioBag<T, TSelf> where TSelf : ScenarioBag<T, TSelf>
{
    private readonly Dictionary<string, T> _scenarios = new();

    public TSelf Register(string key, T scenario)
    {
        _scenarios[key] = scenario;
        return (TSelf)(object)this;
    }

    public T Get(string key)
    {
        if (!_scenarios.TryGetValue(key, out var scenario))
            throw new KeyNotFoundException($"Scenario '{key}' not registered");
        return scenario;
    }
}

public class ExpressionsBag<TModel> : ScenarioBag<Expression<Func<TModel, bool>>, ExpressionsBag<TModel>>
{
}
