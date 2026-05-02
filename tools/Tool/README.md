# dotnet-ngql

Command-line tool for [NGql](https://github.com/dolifer/NGql) — compiles a C# `QueryBuilder` snippet against `NGql.Core` and prints the rendered GraphQL.

## Install

```bash
dotnet tool install -g dotnet-ngql --prerelease
```

The `--prerelease` flag is required while NuGet only has preview versions. Once a stable `2.x.0` ships, the flag becomes optional.

## Update

```bash
dotnet tool update -g dotnet-ngql --prerelease
```

The tool's version tracks `NGql.Core` in lockstep — installing a specific NGql version is `--version 2.1.0-preview.X --prerelease`.

## If `ngql` reports "command not found"

The .NET SDK installs global tools to `~/.dotnet/tools/`, but doesn't always add that path to new shells. Either persist it in your shell rc:

```bash
echo 'export PATH="$PATH:$HOME/.dotnet/tools"' >> ~/.zshrc   # or ~/.bashrc
```

…or set it for the current shell:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
```

## Use

Render from a file:

```bash
ngql my-snippet.cs
```

Render from stdin:

```bash
cat my-snippet.cs | ngql
# or
echo 'QueryBuilder.CreateDefaultBuilder("Hello").AddField("world.name")' | ngql
```

A snippet is an expression-bodied C# script. The final expression must yield the builder whose `ToString()` will be printed. The tool automatically imports:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core;
using NGql.Core.Builders;
```

so a complete snippet can be as small as:

```csharp
QueryBuilder.CreateDefaultBuilder("Hello")
    .AddField("world.name")
```

The output is plain GraphQL on stdout — composable with shell redirects:

```bash
ngql my-snippet.cs > out.graphql
```

## Exit codes

| Code | Meaning |
|---|---|
| 0 | rendered successfully |
| 1 | snippet failed to compile or threw at runtime (error on stderr) |
| 64 | invalid usage |
| 66 | input file not found |

## When to use this

- Hand verification that a `QueryBuilder` snippet renders the GraphQL you expect.
- Companion tool for the [NGql Claude Code skill](https://github.com/dolifer/NGql/tree/main/.claude/skills/ngql), which generates NGql code from natural language or pasted GraphQL.
- Quick rendering inside CI scripts or git hooks (e.g. snapshotting expected query text).

## License

MIT, same as NGql itself.
