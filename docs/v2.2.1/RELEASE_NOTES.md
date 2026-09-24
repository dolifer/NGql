# 2.2.1

NGql 2.2.1 deprecates the classic `Query`/`Mutation` API ahead of its removal in 3.0, fixes six bugs
that silently produced wrong or invalid queries, and makes every workload in the release benchmark
allocate less than 2.2.0. No public
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

These produced wrong or invalid queries without any error. All were present in 2.2.0 as well.

- **Aliased fields no longer replace plain ones.** `AddField("name").AddField("aaa:name")` rendered
  only `aaa:name`; the reverse order, nested fields, and two aliases of one root field
  (`a:user.id`, `b:user.name`) lost a field the same way. Every requested response key is now kept,
  and an alias never renames an existing field.
- **Conflicting variable types throw.** Declaring `$id` as both `ID!` and `Int` rendered
  `query Q($id:ID!, $id:Int)`, which servers reject. It now throws `ArgumentException`
  (`QueryMergeException` from `Include`) before the query changes.
- **Variables inside `FieldBuilder` lambdas are declared.** A `Variable` passed to a nested
  `AddField`, to `Where`, or inside a sub-field `FieldDefinition` rendered as `$n` with no
  declaration.
- **`Where` inside a lambda keeps the field attached.** On a field without arguments, `Where`
  detached the builder, so `u.Where("id", $id).AddField("name")` rendered a bare field.
- **The default merging strategy keeps each fragment's arguments.** Including `users(first:1){id}`
  and `users(first:2){name}` rendered `users(first:2){id name}`. The second is now kept as
  `users_1:users(first:2){name}`, and `GetPathTo` points its fragment there. Arguments only one side
  sets are still combined, so a base query that sets arguments plus fragments that add selections
  merge exactly as before.
- **Case-differing root fields merge.** A root field added with a lambda or arguments was matched
  case-sensitively, so `AddField("User", …)` then `AddField("user", …)` dropped the first field's
  children. The same lookup made adding many such roots quadratic: 1,000 roots with a lambda now take
  0.13 ms instead of 1.54 ms.

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

No public signature changed. Output changes only where 2.2.0 produced a wrong or invalid query:

- Paths that differ only by alias now yield both fields instead of one.
- Code that declared one variable name with two types now throws instead of rendering invalid
  GraphQL.
- Variables used only inside `FieldBuilder` lambdas now appear in the operation signature.
- `MergeByDefault` includes whose arguments conflict now produce an auto-aliased field; read its
  data through `GetPathTo(fragmentName)`.
- Root fields whose names differ only in case now merge.

## Tooling

`dotnet-ngql` 2.2.1 ships with the library. Install or update with `dotnet tool update -g dotnet-ngql`.
Snippets that use the classic API still render; the compiler warning does not stop them.

## Installation

```sh
dotnet add package NGql.Core --version 2.2.1
```
