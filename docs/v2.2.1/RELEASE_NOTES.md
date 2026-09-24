# 2.2.1

NGql 2.2.1 deprecates the classic `Query`/`Mutation` API ahead of its removal in 3.0, fixes a root-field
merge bug, and makes every workload in the release benchmark allocate less than 2.2.0. No public
signature changes. The full list of changes is in the [changelog](../../CHANGELOG.md); this page
covers what matters for an upgrade decision.

## Deprecated: the classic API

`Query`, `Mutation` and `QueryBlock` are now marked `[Obsolete]` with their own diagnostic ID,
**`NGQL0001`**, and will be **removed in NGql 3.0**. They keep working unchanged in 2.x.

<table>
<tr><th>Classic (deprecated)</th><th>QueryBuilder</th></tr>
<tr><td>

```csharp
var query = new Query("GetUser")
    .Select(new Query("user")
        .Where("id", 1)
        .Select("name", "email"));
```

</td><td>

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user",
        new Dictionary<string, object?> { ["id"] = 1 },
        new[] { "name", "email" });
```

</td></tr>
</table>

Mutations move to `QueryBuilder.CreateMutationBuilder`; the [migration guide](../reference/MIGRATION.md)
covers every classic construct.

**If your build treats warnings as errors**, upgrading to 2.2.1 fails wherever the classic types are
used. To keep building while you migrate, suppress only this deprecation:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);NGQL0001</NoWarn>
</PropertyGroup>
```

or wrap the call sites in `#pragma warning disable NGQL0001`. Other obsolete-API warnings stay on.

## Fixed

A root field added with a lambda or with arguments was matched against existing roots
case-sensitively, while the root dictionary is case-insensitive. `AddField("User", …)` followed by
`AddField("user", …)` therefore replaced the first field and **silently dropped its children**. The
second call now merges into the existing root, as nested fields already did. The same lookup made
adding many such roots quadratic: 1,000 roots with a lambda now take 0.13 ms instead of 1.54 ms.

## Performance

Every workload in the release benchmark allocates less than 2.2.0, and none more. Managed bytes per
operation, .NET 9:

| Workload | 2.2.0 | 2.2.1 | Change |
| --- | ---: | ---: | ---: |
| Simple query | 1,413 B | 1,034 B | −27% |
| Dictionary arguments ×50 | 185,999 B | 126,802 B | −32% |
| Directives ×50 | 210,002 B | 138,404 B | −34% |
| Classic nested query, depth 10 | 13,363 B | 6,001 B | −55% |
| Building 100 queries | 204,001 B | 160,000 B | −22% |
| 200 dotted paths | 232,059 B | 202,988 B | −12.5% |
| Re-adding fields with arguments | 6,272 B | 2,680 B | −57% |

In the in-process benchmark job, 21 of the 30 workloads are faster than 2.2.0 with non-overlapping
confidence intervals and none is slower: a simple query −34%, directives ×50 −32%, dictionary
arguments ×50 −24%.

Where it comes from: a new field's name, key and path share one string; arguments are sorted once
and enumerated without boxing; collections that most queries never use (argument and variable sets,
cycle detection, name maps, merge-index bookkeeping) are created on first use; the classic API sorts
without LINQ; and path classification, name validation and string escaping use vectorized searches.

These are microbenchmarks on one machine; they do not predict the throughput of a service. Full
tables and the command to reproduce them are in [BENCHMARKS.md](BENCHMARKS.md).

**Not done, on purpose.** Three further gains were measured and left out because each would change
observable behavior: caching the sorted child order (would change the order `Fields` enumerates in,
or add memory to every first render), replacing `SortedDictionary` as argument storage (the
`FieldDefinition` constructor shares the dictionary you pass, and `Arguments` returns it live), and
dropping the per-collection lock object (the collection is reachable through `Fields`).

## Compatibility

No public signature changed. Rendered output is unchanged, except for the fix above: two root
fields whose names differ only in case now merge instead of the second replacing the first.

## Tooling

`dotnet-ngql` 2.2.1 ships with the library. Install or update with `dotnet tool update -g dotnet-ngql`.
Snippets that use the classic API still render; the compiler warning does not stop them.

## Installation

```sh
dotnet add package NGql.Core --version 2.2.1
```
