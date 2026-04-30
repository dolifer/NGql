# NGql 2.0 is out

Hi folks 👋

NGql 2.0 is live on NuGet. This is the biggest release the library has had — eight months of work between the type-drift fix that started the branch in August 2025 and the polishing that wrapped up today.

If you're new here: NGql is a **zero-dependency, schema-less GraphQL query builder for .NET**. You write GraphQL operations in C# with a fluent API, compose fragments at runtime, and extract field subsets — no SDL, no codegen, no build-time schema validation. It's small enough that you can read the whole thing in an afternoon.

## What changed

The 2.0 line introduces three things that weren't possible in 1.5:

**1. Field preservation (`PreservationBuilder`).** Take an existing query and pick a subset by string path or by a typed C# expression. Useful for role-based filtering, conditional fragments, or stripping sensitive fields from a query you didn't author.

```csharp
var publicView = PreservationBuilder.Create(profile)
    .Preserve("user.name", "user.email")
    .Build();

var conditional = PreservationBuilder.Create(profile)
    .PreserveFromExpression<UserDto>(x => x.user.email != null)
    .Build();
```

**2. Multi-strategy merging (`MergingStrategy`).** When you `Include(otherBuilder)`, the new strategy enum tells the merger how to handle duplicate paths: merge them, alias them on conflict, or keep them distinct. Default behavior covers the common case; the strict and never-merge modes are there when you need them.

**3. A real performance pass.** In-place merging, lock-free reads on the field tree, span-based path parsing, and zero-allocation cache hits on path lookups. Query rendering is now reflection-free; reflection is confined to the LINQ-expression preservation path.

## What this took

The branch landed **244 files changed, +33,169 / −3,255**, squashed into 23 thematic commits. About a third of that diff is tests — the Core namespace now sits at **99.93% line coverage and 99.32% branch coverage**, with 1,716 tests across .NET 8, 9, and 10.

A few things I wasn't expecting going in:
- The "consolidate scattered tests into Theories with scenario bags" pass deleted more lines than it added, while raising coverage. Worth doing on any test suite that's grown organically.
- Removing dead defensive guards turned out to be a coverage *gain*, because the guarded branches were unreachable and were polluting the report.
- NUKE was a pleasant build system to use, but for a single-purpose library with three workflows, a 50-line Makefile reads better than 130 lines of `Build.cs`.

## Try it

```bash
dotnet add package NGql.Core
```

The [README](https://github.com/dolifer/NGql#readme) has runnable examples for every API. The output blocks are pasted verbatim from `ToString()` — if a sample renders differently from what's documented, that's a bug.

## What I'd love feedback on

- **Real-world preservation use cases.** The string-path and LINQ-expression APIs cover what I've seen, but the surface is new and there's room to add convenience overloads if there's demand.
- **Merge edge cases.** The `MergeByFieldPath` strategy auto-aliases on argument conflicts — if you hit a case where the resulting alias is awkward or the merge is surprising, please open an issue with the input fragments.
- **Documentation gaps.** The migration guide covers what I changed, but if you're upgrading from 1.5.x and something feels worse than before, I want to hear about it.

Issues, discussions, and PRs all welcome. The contributor guide ([`CLAUDE.md`](https://github.com/dolifer/NGql/blob/main/CLAUDE.md)) has the conventions if you'd like to send one.

---

🎂 *Side note: shipping this on my birthday wasn't planned but it's a nice coincidence. Cheers, and thanks for using NGql.*

— Denis
