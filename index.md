---
layout: default
title: NGql
description: A zero-dependency, schema-less GraphQL query builder for .NET.
---

A **zero-dependency, schema-less** GraphQL query builder for .NET. Compose queries and mutations from C# with a fluent API, merge fragments at runtime, and extract subsets via field-path or LINQ expression — without an SDL or codegen step.

[![NuGet](https://img.shields.io/nuget/v/NGql.Core)](https://www.nuget.org/packages/NGql.Core/)
[![Downloads](https://img.shields.io/nuget/dt/NGql.Core)](https://www.nuget.org/packages/NGql.Core/)
[![Build](https://img.shields.io/github/actions/workflow/status/dolifer/NGql/build-and-test.yml?branch=main)](https://github.com/dolifer/NGql/actions/workflows/build-and-test.yml)
[![Line coverage](https://dolifer.github.io/NGql/coverage/badge_linecoverage.svg)](coverage/)
[![Branch coverage](https://dolifer.github.io/NGql/coverage/badge_branchcoverage.svg)](coverage/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/dolifer/NGql/blob/main/LICENSE)

```bash
dotnet add package NGql.Core
```

---

## What you get

- **Schemaless.** No SDL, no codegen, no build-time schema validation. The consumer supplies the schema knowledge; NGql just composes.
- **Zero runtime dependencies.** Single assembly, multi-targets `net8.0` / `net9.0` / `net10.0`.
- **Fluent composition.** `QueryBuilder` / `FieldBuilder` keeps query construction inline with C# control flow — no template strings, no string concat.
- **Runtime fragment merging.** `Include(otherBuilder)` joins disjoint fragments; `MergingStrategy` picks how duplicates resolve (merge, never merge, or merge-by-field-path with auto-aliasing on conflict).
- **Field preservation.** Extract a subset of a built query by string path or by typed C# expression — useful for role-based filtering or producing a public view of a private query.
- **Inline fragments + unions.** `OnType("Repository", b => …)` renders `... on Repository { … }` for narrowing union/interface return types.
- **Hot-path optimized.** Span-based path parsing, lock-free reads on `FieldChildren`, in-place merge in `Include()`. Reflection is confined to the LINQ-expression preservation path; query rendering itself is reflection-free.

---

## Quick taste

```csharp
using NGql.Core;
using NGql.Core.Builders;

var idVar = new Variable("$id", "ID!");

var query = QueryBuilder.CreateDefaultBuilder("GetUser")
    .AddField("user",
        new Dictionary<string, object?> { ["id"] = idVar },
        new[] { "id", "name", "email" });

Console.WriteLine(query);
```

```graphql
query GetUser($id:ID!){
    user(id:$id){
        email
        id
        name
    }
}
```

Mutations use `CreateMutationBuilder("Op")` with the same fluent surface. Inline fragments use `b.OnType("Repository", r => …)` for union narrowing. Field preservation uses `PreservationBuilder.Create(src).Preserve("user.name").Build()`.

---

## Where to next

| | |
|---|---|
| **[Install](install/)** | NuGet package, optional CLI tool, optional Claude Code skill — pick what you need. |
| **Skill** | The companion Claude Code skill that translates "build me a query for X" → working NGql code. Two channels in the `dolifer` plugin marketplace: [stable (`ngql`)](https://dolifer.github.io/claude-plugins/skills/ngql/) (coming soon) · [preview (`ngql-preview`)](https://dolifer.github.io/claude-plugins/skills/ngql-preview/) (live). |
| **[Coverage](coverage/)** | Full coverage report for the Core namespace. Currently ~99.9% line / 99% branch. |
| **[CHANGELOG](https://github.com/dolifer/NGql/blob/main/CHANGELOG.md)** | What's in 2.1, what changed from 2.0. |
| **[README on GitHub](https://github.com/dolifer/NGql#readme)** | Long-form docs with runnable examples for every API. |
| **[Source](https://github.com/dolifer/NGql)** | Issues, PRs, the codebase itself. |

---

## Status

| | |
|---|---|
| **Latest stable** | See the NuGet badge above. The library uses GitVersion-driven SemVer; previews ship as `<X.Y.Z>-preview.N`. |
| **Companion tool** | [`dotnet-ngql`](install/#cli-tool-dotnet-ngql) — compiles a snippet, renders the GraphQL, optionally executes against a live endpoint. |
| **Companion skill** | [`ngql`](https://dolifer.github.io/claude-plugins/skills/ngql/) (stable, coming soon) and [`ngql-preview`](https://dolifer.github.io/claude-plugins/skills/ngql-preview/) (preview, live) — Claude Code skills in the [`dolifer` plugin marketplace](https://dolifer.github.io/claude-plugins/). Both can coexist in one session. |
| **Min runtime** | .NET 8.0 |

License: MIT.
