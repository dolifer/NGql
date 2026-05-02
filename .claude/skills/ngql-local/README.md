# `ngql-local` — editable in-repo copy of the NGql Claude Code Skill

A [Claude Code](https://docs.claude.com/en/docs/claude-code) Skill that teaches Claude to author [NGql](https://github.com/dolifer/NGql) query-builder C# from natural language, pasted GraphQL operations, or curl commands.

## Channel naming

This Skill ships under three distinct plugin names — they all coexist in one Claude Code session and you can install any combination:

| Name | Where it lives | Who uses it | Updated when |
|---|---|---|---|
| **`ngql-local`** | `.claude/skills/ngql-local/` in this repo | Contributors editing the Skill or the library it teaches | Every commit on this branch |
| **`ngql-preview`** | `plugins/ngql-preview/` in `dolifer/claude-plugins` | Users testing upcoming changes | Every preview release tag on NGql |
| **`ngql`** | `plugins/ngql/` in `dolifer/claude-plugins` | End users on a stable NGql release | Every stable release tag on NGql |

The three are **the same content at different snapshots**. `ngql-local` is the source of truth; release CI on this repo copies it into the catalog repo and rewrites the frontmatter `name` for each channel.

Inside Claude Code, invoke them as `/ngql-local:ngql`, `/ngql-preview:ngql`, or `/ngql:ngql`. The plugin name in the prefix tells you which copy you're using.

## What it does

When you invoke `/ngql` (or describe a query-building task in a project that has this Skill installed), Claude:

1. **Translates intent into NGql code.** "Get the top 10 repos of `dolifer` with stargazer counts" produces a `QueryBuilder` snippet using the fluent API.
2. **Ports existing GraphQL.** Paste a `curl` or a raw operation, and Claude rewrites it as NGql calls — preserving operation name, variable types, enum literals, and nested arguments.
3. **Refactors via `PreservationBuilder`.** "Strip `ssn` and `email` from this builder" yields a `Preserve(...)` chain that keeps everything else.

It will **ask** before guessing schema details for private APIs, before generating mutations that change state, and when "drop these fields" is ambiguous (since `Preserve` is inclusive, not exclusive).

## What it won't do

- Invent field names. NGql is schema-less by design — *the consumer* supplies the schema. The Skill mirrors that: no SDL is consumed, but if Claude doesn't know the API (private server, proprietary schema), it asks rather than hallucinating.
- Generate the classic `Query` API (`new Query(...)`, `.Where(...)`, `.Include<T>(...)`). That surface was deprecated in NGql 2.0. The Skill emits `QueryBuilder` for queries and `Mutation` for write operations.
- Send your code anywhere — Skills run entirely inside the Claude Code session.

## Install

### `ngql-local` — automatic when working inside this repo

When you run Claude Code inside the NGql repo, `ngql-local` is auto-discovered from `.claude/skills/ngql-local/SKILL.md`. Invoke as `/ngql-local:ngql`. No install step.

### `ngql-preview` and `ngql` — via the plugin marketplace

Once published, both shared channels live in the [`dolifer/claude-plugins`](https://github.com/dolifer/claude-plugins) catalog. Inside any Claude Code session:

```
/plugin marketplace add dolifer/claude-plugins
/plugin install ngql@dolifer            # stable
/plugin install ngql-preview@dolifer    # preview
```

Both can be installed simultaneously alongside `ngql-local`. Pull updates with `/plugin marketplace update`.

## Usage

Inside Claude Code, type `/ngql` to invoke the Skill explicitly, or just describe what you want — the Skill auto-triggers when your prompt mentions `QueryBuilder`, `Mutation`, NGql, or pastes a GraphQL operation / curl.

Examples:

```
/ngql build a query that fetches a user's email and the names of their last 5 orders
```

```
port this to NGql:
curl https://api.example.com/graphql -d '{"query":"query GetOrder($id: ID!) { order(id: $id) { id status } }"}'
```

```
take the `fullProfile` builder and produce a public view with only name and avatar
```

## Verifying generated code with `ngql`

The Skill is paired with a companion .NET global tool, `dotnet-ngql`, which compiles a generated snippet and prints the GraphQL it renders to.

```bash
dotnet tool install -g dotnet-ngql --prerelease   # one-time (preview-only on NuGet today)
ngql snippet.cs                                   # render to stdout
echo '<snippet>' | ngql                           # or read from stdin
```

To run the rendered operation against a live endpoint and see the response:

```bash
ngql snippet.cs --execute \
    --endpoint https://api.example.com/graphql \
    -H "Authorization: Bearer $TOKEN" \
    --var id=42
```

Mutations are refused by default — pass `--allow-mutations` once you're sure the side effect is intended.

The tool's version tracks `NGql.Core` in lockstep, so installing a specific NGql version is `dotnet tool install -g dotnet-ngql --version 2.1.0-preview.X --prerelease`. Update with `dotnet tool update -g dotnet-ngql --prerelease`. The `--prerelease` flag stays required until a stable `2.x.0` ships.

Full tool docs: <https://www.nuget.org/packages/dotnet-ngql>.

## Reporting issues

The Skill content lives in `SKILL.md` in this folder. If Claude generates something incorrect — wrong overload, hallucinated method, broken output — open an issue at <https://github.com/dolifer/NGql/issues> with:

- the prompt you gave
- the snippet Claude produced
- what you expected

Skill bugs are typically a one-line fix in `SKILL.md`.
