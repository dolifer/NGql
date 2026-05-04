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

Want the latest unreleased changes? Pin to a preview version (e.g. `dotnet add package NGql.Core --version 2.1.1-preview.X`) — previews ship as `<X.Y.Z>-preview.<N>` and track every push to `main`.

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

A .NET global tool that compiles a `QueryBuilder` snippet and prints the rendered GraphQL. Optionally executes the query against a live endpoint with `--execute`. Useful for hand-verifying snippets, snapshotting expected query text in CI, or pairing with the Claude Code skill ([stable](https://dolifer.github.io/claude-plugins/skills/ngql/) or [preview](https://dolifer.github.io/claude-plugins/skills/ngql-preview/)) to run generated code without leaving your terminal.

**Install (stable):**

```bash
dotnet tool install -g dotnet-ngql
```

Want the latest unreleased changes (newest features, may have rough edges)? Add `--prerelease` to opt into the preview channel — preview versions ship as `<X.Y.Z>-preview.<N>` and track every push to `main`:

```bash
dotnet tool install -g dotnet-ngql --prerelease
```

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

**Update:** `dotnet tool update -g dotnet-ngql` (add `--prerelease` to track preview).

The tool's version tracks `NGql.Core` in lockstep — install a specific version with `--version 2.1.0` (or `--version 2.1.1-preview.X --prerelease` for previews).

---

## Claude Code skill (optional)

A Claude Code skill that teaches Claude to author NGql code from natural language, pasted GraphQL, or curl. It pairs with the `dotnet-ngql` CLI to verify what it generated.

Two channels in the [`dolifer` plugin marketplace](https://dolifer.github.io/claude-plugins/) — install one or both:

| Channel | Install | Invoke | Status | Docs |
|---|---|---|---|---|
| `ngql` (stable) | `/plugin install ngql@dolifer` | `/ngql:ngql …` | Coming soon — install will fail until the first stable release ships. | [docs](https://dolifer.github.io/claude-plugins/skills/ngql/) |
| `ngql-preview` (preview) | `/plugin install ngql-preview@dolifer` | `/ngql-preview:ngql …` | Live, tracking the latest NGql preview. | [docs](https://dolifer.github.io/claude-plugins/skills/ngql-preview/) |

Both channels can coexist in the same Claude Code session — `/ngql:ngql` invokes stable, `/ngql-preview:ngql` invokes preview. Until stable ships, install preview:

```text
/plugin marketplace add dolifer/claude-plugins
/plugin install ngql-preview@dolifer
```

Then in any session: `/ngql-preview:ngql build me a query for…`.

---

## Verify the install

```bash
dotnet --version       # need .NET 8 SDK or newer for the library
ngql --version         # tool version (matches the latest preview you installed)
```

Inside a Claude Code session: `/plugin` lists installed plugins; `/ngql-preview:ngql` should be among the available commands once the skill is installed.
