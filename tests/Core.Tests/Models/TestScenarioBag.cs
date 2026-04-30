using System;

namespace NGql.Core.Tests.Models;

/// <summary>
/// Named test scenario with arrange/assert logic and descriptive name.
/// </summary>
public record TestScenario<T>(string Name, Func<T> Arrange, Action<T> Assert);

/// <summary>
/// Typed scenario registry for test arrange/assert patterns.
/// Enables fluent syntax: new TestScenarioBag&lt;T&gt;().Register("name", arrange, assert)...
/// This eliminates branching in Theory tests by colocating setup and assertions per scenario.
/// </summary>
public class TestScenarioBag<T> : ScenarioBag<TestScenario<T>, TestScenarioBag<T>>
{
    /// <summary>
    /// Register a named scenario with arrange and assert delegates.
    /// </summary>
    public TestScenarioBag<T> Register(string name, Func<T> arrange, Action<T> assert)
    {
        return Register(name, new TestScenario<T>(name, arrange, assert));
    }
}

/// <summary>
/// Named test scenario with arrange/assert logic and descriptive name (generic setup variant).
/// </summary>
#pragma warning disable S2326 // Unused generic type parameter TSetup is kept for semantic clarity / API symmetry
public record TestScenario<TSetup, T>(string Name, Func<T> Arrange, Action<T> Assert);
#pragma warning restore S2326

/// <summary>
/// Typed scenario registry for test arrange/assert patterns with different input/output types.
/// Enables fluent syntax for tests where setup type differs from result type.
/// </summary>
#pragma warning disable S2326 // Unused generic type parameter TSetup is kept for semantic clarity
public class TestScenarioBag<TSetup, T> : ScenarioBag<TestScenario<TSetup, T>, TestScenarioBag<TSetup, T>>
#pragma warning restore S2326
{
    /// <summary>
    /// Register a named scenario with arrange and assert delegates.
    /// </summary>
    public TestScenarioBag<TSetup, T> Register(string name, Func<T> arrange, Action<T> assert)
    {
        return Register(name, new TestScenario<TSetup, T>(name, arrange, assert));
    }
}
