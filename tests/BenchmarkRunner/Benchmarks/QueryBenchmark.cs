using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class QueryBenchmark
{
    [Benchmark(Baseline = true)]
    public string Query()
    {
        var query = new Query("PersonAndFilms")
            .Select(new Query("person")
                .Where("id", "cGVvcGxlOjE=")
                .Select("name")
                .Select(new Query("filmConnection")
                    .Select(new Query("films")
                        .Select("title")))
            );
        
        return query.ToString();
    }
    
    [Benchmark]
    public string Builder()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("PersonAndFilms")
            .AddField("person", new Dictionary<string, object?>
            {
                ["id"] = "cGVvcGxlOjE=",
            }, subFields: ["name", "filmConnection.films.title"]);
        
        return builder.ToString();
    }
}
