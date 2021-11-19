using System;
using BenchmarkDotNet.Attributes;
using NGql.Core;

namespace Benchmarks.Benchmarks
{
    [MemoryDiagnoser]
    public class QueryBuilderBenchmark
    {
        private readonly string _queryText;

        public QueryBuilderBenchmark() => _queryText = GetQuery();

        [Benchmark]
        public object GetStatic() => new
        {
            Query = _queryText,
            Variables = new
            {
                date = DateTimeOffset.UtcNow
            }
        };

        [Benchmark]
        public object GetNew() => new
        {
            Query = GetQuery(),
            Variables = new
            {
                date = DateTimeOffset.UtcNow
            }
        };

        private static Query GetQuery() => new Query("PersonAndFilms")
            .Select(new Query("person")
                .Where("id", "cGVvcGxlOjE=")
                .Select("name")
                .Select(new Query("filmConnection")
                    .Select(new Query("films")
                        .Select("title")))
            );
    }
}
