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

    public static async Task<SnippetResult> CompileAndRun(string snippet)
    {
        try
        {
            var result = await CSharpScript.EvaluateAsync<object?>(snippet, Options);
            return result is null
                ? SnippetResult.Failed("snippet produced null — its final expression must yield a builder/object with ToString()")
                : SnippetResult.Rendered(result.ToString() ?? string.Empty);
        }
        catch (CompilationErrorException ex)
        {
            return SnippetResult.Failed("compile error:\n" + string.Join('\n', ex.Diagnostics));
        }
        catch (Exception ex)
        {
            return SnippetResult.Failed($"runtime error: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static bool IsMutation(string rendered) =>
        rendered.TrimStart().StartsWith("mutation ", StringComparison.Ordinal);
}

/// <summary>
/// Outcome of compiling and evaluating a snippet: exactly one of <see cref="Output"/> (when
/// <see cref="Ok"/>) or <see cref="Error"/> carries text, so callers never need a null fallback.
/// </summary>
internal sealed record SnippetResult(bool Ok, string Output, string Error)
{
    public static SnippetResult Rendered(string output) => new(true, output, string.Empty);

    public static SnippetResult Failed(string error) => new(false, string.Empty, error);
}
