---
layout: default
title: Install — NGql
description: Install the NGql library, the dotnet-ngql CLI, and (optionally) the Claude Code skill.
---

<p><a href="{{ '/' | relative_url }}">← back to home</a></p>

# Install

Three independent pieces. The library is the only required one — the CLI and skill are optional companions.

---

## Library: `NGql.Core`

```bash
dotnet add package NGql.Core
```

Multi-targets `net8.0` / `net9.0` / `net10.0`. Zero runtime dependencies. Add the package, write `using NGql.Core; using NGql.Core.Builders;`, and you're building queries.

```csharp
using NGql.Core.Builders;

var query = QueryBuilder.CreateDefaultBuilder("Hello").AddField("world.name");
Console.WriteLine(query);
// query Hello{
//     world{
//         name
//     }
// }
```

For the long-form docs with every API + runnable examples, see the [README on GitHub](https://github.com/dolifer/NGql#readme).

---

## CLI tool: `dotnet-ngql`

A .NET global tool that compiles a `QueryBuilder` snippet and prints the rendered GraphQL. Optionally executes the query against a live endpoint with `--execute`. Useful for hand-verifying snippets, snapshotting expected query text in CI, or pairing with the [Claude Code skill](skill/) to run generated code without leaving your terminal.

**Install (preview only — no stable release on NuGet yet):**

```bash
dotnet tool install -g dotnet-ngql --prerelease
```

The `--prerelease` flag is required while only preview versions exist. Drop it once a stable `2.x.0` ships.

**Use:**

```bash
echo 'QueryBuilder.CreateDefaultBuilder("Hello").AddField("world.name")' | ngql
# query Hello{
#     world{
#         name
#     }
# }

# Execute against a live endpoint:
echo '<snippet>' | ngql --execute \
    --endpoint https://api.example.com/graphql \
    -H "Authorization: Bearer $TOKEN" \
    --var id=42
```

Mutations are refused by default — pass `--allow-mutations` once you're sure the side effect is intended. Full options: `ngql --help`.

**Update:** `dotnet tool update -g dotnet-ngql --prerelease`.

The tool's version tracks `NGql.Core` in lockstep — install a specific version with `--version 2.1.0-preview.X --prerelease`.

---

## Claude Code skill (optional)

The `ngql-preview` Claude Code skill teaches Claude to author NGql code from natural language, pasted GraphQL, or curl. It pairs with the `dotnet-ngql` CLI to verify what it generated.

```text
/plugin marketplace add dolifer/claude-plugins
/plugin install ngql-preview@dolifer
```

Then in any session: `/ngql-preview:ngql build me a query for…`.

Full skill docs: [dolifer.github.io/claude-plugins/skills/ngql-preview/](https://dolifer.github.io/claude-plugins/skills/ngql-preview/).

---

## Verify the install

```bash
dotnet --version       # need .NET 8 SDK or newer for the library
ngql --version         # tool version (matches the latest preview you installed)
```

Inside a Claude Code session: `/plugin` lists installed plugins; `/ngql-preview:ngql` should be among the available commands once the skill is installed.
