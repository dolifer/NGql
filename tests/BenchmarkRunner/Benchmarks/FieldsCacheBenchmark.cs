using BenchmarkDotNet.Attributes;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class FieldsCacheBenchmark
{
    [Benchmark]
    public void NoCache()
    {
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("all_users", new QueryBuilderOptions
            {
                UseFieldsCache = false
            });

        DoWork(queryBuilder);
    }

    [Benchmark(Baseline = true)]
    public void WithCache()
    {
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("all_users", new QueryBuilderOptions
            {
                UseFieldsCache = true
            });

        DoWork(queryBuilder);
    }

    private const string ShortFieldName =
        "root.node1.node2.node3.child1.child2.child3.child4.child5.child6.node4.node5.node6.node7" +
        ".node8.node9.node10.node11.node12.node13.node14.node15.node16.node17.node18.node19.node20" +
        ".child7.child8.child9.child10.child11.child12.child13.child14.child15.child16.child17.child18" +
        ".child19.child20.child21.child22.child23.child24.child25.child26.child27.child28.child29.child30" +
        ".node21.node22.node23.node24.node25.node26.node27.node28.node29.node30.node31.node32.node33";
    
    private const string LongFieldName = ShortFieldName +
        ".node34.node35.node36.node37.node38.node39.node40.node41.node42.node43.node44.node45.node46" +
        ".child31.child32.child33.child34.child35.child36.child37.child38.child39.child40.child41.child42" +
        ".child43.child44.child45.child46.child47.child48.child49.child50.child51.child52.child53.child54" +
        ".node47.node48.node49.node50.node51.node52.node53.node54.node55.node56.node57.node58.node59" +
        ".child55.child56.child57.child58.child59.child60.child61.child62.child63.child64.child65.child66" +
        ".node60.node61.node62.node63.node64.node65.node66.node67.node68.node69.node70.node71.node72" +
        ".child67.child68.child69.child70.child71.child72.child73.child74.child75.child76.child77.child78" +
        ".node73.node74.node75.node76.node77.node78.node79.node80.node81.node82.node83.node84.node85" +
        ".child79.child80.child81.child82.child83.child84.child85.child86.child87.child88.child89.child90";
    
    private static void DoWork(QueryBuilder queryBuilder)
    {
        for (var i = 0; i < 10000; i++)
        {
            queryBuilder.AddField(ShortFieldName);
            queryBuilder.AddField(LongFieldName);
        }
    }
}
