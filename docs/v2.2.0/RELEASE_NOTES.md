# 2.2.0

NGql 2.2 completes the GraphQL operation surface — subscriptions, named fragments and directives — adds allocation-free output sinks, and ships a second performance pass: every workload in the release benchmark now allocates less than 2.1.0, and none more. The full list of changes is in the [changelog](../../CHANGELOG.md); this page covers what matters for an upgrade decision.

## Highlights

### Subscriptions, named fragments and directives

<table>
<tr><th>C#</th><th>GraphQL</th></tr>
<tr><td>

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("Users")
    .AddFragment("Identity", "User", f => f
        .AddField("id")
        .AddField("name"))
    .AddField("viewer", v => v
        .SpreadFragment("Identity")
        .AddField("email", e => e
            .IncludeIf(new Variable(
                "$withEmail", "Boolean!"))));
```

</td><td>

```graphql
query Users($withEmail:Boolean!){
    viewer{
        email @include(if:$withEmail)
        ...Identity
    }
}
fragment Identity on User{
    id
    name
}
```

</td></tr>
</table>

- `QueryBuilder.CreateSubscriptionBuilder(name)` renders `subscription Name { … }` with the same fluent surface as queries and mutations.
- `AddFragment` + `SpreadFragment` render `fragment Name on Type { … }` and `...Name`.
- `IncludeIf`, `SkipIf` and `Directive` attach directives to fields and inline fragments. A `Variable` passed to `IncludeIf`/`SkipIf` is declared in the operation signature for you.
- `Include` understands all of it: fragment definitions, spreads and directives merge, and two fragments that request the same path under different `@include`/`@skip` conditions are kept apart instead of silently sharing one condition.

### Output without intermediate strings

`AppendTo(StringBuilder)`, `WriteTo(TextWriter)` and `WriteUtf8(IBufferWriter<byte>)` on `QueryBuilder`, `Query` and `Mutation` render straight into your buffer. Output is byte-identical to `ToString()`; a warm render of a query with sorted fields or a single argument into a sink allocates nothing.

### Performance

Measured with the same benchmark source against the NGql.Core 2.1.0 package (BenchmarkDotNet, .NET 9, Apple M4; managed bytes allocated per operation):

| Workload | 2.1.0 | 2.2.0 | Change |
| --- | ---: | ---: | ---: |
| Simple query | 1,597 B | 1,413 B | −12% |
| Complex query with merging | 6,380 B | 4,721 B | −26% |
| Dictionary arguments ×50 | 208,804 B | 185,999 B | −11% |
| Expression preservation ×50 | 180,879 B | 147,528 B | −18% |
| `ToString` ×50 | 42,015 B | 32,195 B | −23% |
| 200 dotted paths | 236,984 B | 232,059 B | −2% |
| 500-field flat selection | 129,444 B | 129,280 B | −0.1% |
| Classic nested query, depth 30 | 685,332 B | 56,556 B | −92% |

In the in-process benchmark job, 22 of the 24 workloads are also faster than 2.1.0 with non-overlapping confidence intervals, typically by 9–22%: a simple query −11%, a complex merge −15%, `ToString` ×50 −22%, a depth-30 classic nested query −75%. Large flat selections are unchanged. Merging many fragments that share a path but differ in arguments is now linear instead of quadratic (800 such fragments: about 117 ms → 1.4 ms).

Memory held by long-lived state is bounded: the per-thread render-builder pool keeps at most 262,144 characters in total, custom type names are cached in two generations of 2,048, and reflection caches no longer keep collectible assemblies alive. A merged query held in memory retains 2,352 B where 2.1.0 retained 3,008 B.

These are microbenchmarks on one machine; they do not predict the throughput of a service. Full tables are in [BENCHMARKS.md](BENCHMARKS.md); method and the costs accepted along the way are in [`docs/PERFORMANCE_REVIEW.md` at 2.2.0](https://github.com/dolifer/NGql/blob/2.2.0/docs/PERFORMANCE_REVIEW.md).

**Costs to know about.** Fields that carry metadata, directives or fragments pay a 48 B holder, and cold builds of metadata-bearing fields are about 12% slower than before the layout change. Signed integers render about 4 ns slower each, in exchange for an ASCII minus sign under every culture.

## Breaking changes

- `QueryDefinition.Fields` and `QueryDefinition.Metadata` return `IReadOnlyDictionary<,>` and `Metadata` has no public setter. Reads are unchanged; mutate through `QueryBuilder`/`FieldBuilder`.
- Behavior: signed integers always render with the ASCII `-`, regardless of `CultureInfo.CurrentCulture`.

No other public signature changed. Interned type-name strings are no longer guaranteed to be the same instance for the life of the process; equality is unaffected.

## Fixed

- Same-name/different-alias sibling fields no longer overwrite each other (present in 2.1.0 and earlier).
- `AddArgument(IReadOnlyDictionary)` and the single-key overload are atomic: a rejected call leaves the block untouched.
- `NullReferenceException` when merging into a childless object-typed field.
- `PreservationBuilder.Build()` results no longer share state with their source builder.
- `dotnet-ngql`: `ngql -` now reads the snippet from stdin as documented since 2.1.

Fixed since the 2.2.0 previews (never in a stable release): stale merge-index entries after mutating an indexed query through a captured `FieldBuilder`, and `WriteUtf8` throwing when the buffer writer returned a short span.

## Tooling

`dotnet-ngql` 2.2.0 ships with the library. Install or update with `dotnet tool update -g dotnet-ngql`.

## Installation

```sh
dotnet add package NGql.Core --version 2.2.0
```
