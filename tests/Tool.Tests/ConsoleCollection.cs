using Xunit;

namespace NGql.Tool.Tests;

/// <summary>
/// <see cref="Console"/> and <see cref="ToolEnvironment"/> are process-global, so every class
/// that redirects them runs in this single non-parallel collection.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public static class ConsoleCollection
{
    public const string Name = "console";
}
