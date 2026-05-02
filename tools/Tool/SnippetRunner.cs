using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

using NGql.Core;
using NGql.Core.Builders;

namespace NGql.Tool;

/// <summary>
/// Compiles + evaluates a C# script snippet against the bundled NGql.Core, returning the
/// ToString() of the final expression's value.
/// </summary>
internal static class SnippetRunner
{
    private static readonly ScriptOptions Options = ScriptOptions.Default
        .WithReferences(
            typeof(QueryBuilder).Assembly,
            typeof(EnumValue).Assembly,
            typeof(Dictionary<,>).Assembly,
            typeof(System.Linq.Enumerable).Assembly,
            typeof(object).Assembly)
        .WithImports(
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "NGql.Core",
            "NGql.Core.Builders");

    public static async Task<(bool ok, string? output, string? error)> CompileAndRun(string snippet)
    {
        try
        {
            var result = await CSharpScript.EvaluateAsync<object?>(snippet, Options);
            return result is null
                ? (false, null, "snippet produced null — its final expression must yield a builder/object with ToString()")
                : (true, result.ToString() ?? string.Empty, null);
        }
        catch (CompilationErrorException ex)
        {
            return (false, null, "compile error:\n" + string.Join('\n', ex.Diagnostics));
        }
        catch (Exception ex)
        {
            return (false, null, $"runtime error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static bool IsMutation(string rendered) =>
        rendered.TrimStart().StartsWith("mutation ", StringComparison.Ordinal);
}
