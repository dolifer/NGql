using System;
using BenchmarkDotNet.Running;

namespace Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        // Force the loader to accept our local version even if it's looking for 1.5.0
        AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
        {
            if (resolveArgs.Name.StartsWith("NGql.Core"))
            {
                return typeof(NGql.Core.Abstractions.QueryBlock).Assembly;
            }
            return null;
        };
        
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
