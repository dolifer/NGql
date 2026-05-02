# Changelog

All notable changes to **NGql.Core** and its companion `dotnet-ngql` CLI are recorded here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Per-release deep-dive notes live under [`docs/<version>/RELEASE_NOTES.md`](docs/) when they exist; this file is the terse summary intended for upgrade decisions.

The companion Claude Code Skill is versioned independently — see [`.claude/skills/ngql-local/CHANGELOG.md`](.claude/skills/ngql-local/CHANGELOG.md).

## [Unreleased]

## [2.1.0] - 2026-05-02

### Added
- `QueryBuilder.CreateMutationBuilder(name)` and `CreateMutationBuilder(name, MergingStrategy)` — the fluent builder now produces both queries and mutations from the same surface, removing the need to drop down to the classic `Mutation` type for new code.
- `QueryBuilder.AddField` gains `(field, args, lambda)` and `(field, subFields, lambda)` shorthand overloads, eliminating the `metadata: null` boilerplate previously required for these shapes.
- `FieldBuilder.AddField` gains args-first variants (`(field, args, subFields, …)`) so the parameter order matches `QueryBuilder.AddField` inside sub-field lambdas.
- `dotnet-ngql` global tool — companion CLI that compiles a `QueryBuilder` snippet against `NGql.Core` and prints the rendered GraphQL. Supports rendering from a file or stdin, plus `--execute --endpoint URL` to POST the operation to a live GraphQL server. Mutations are refused unless `--allow-mutations` is passed. Versioned in lockstep with `NGql.Core` and shipped from the same release.

### Changed
- `FieldBuilder.AddField(field, dict, lambda)` — the dictionary at this position is now interpreted as **arguments** (consistent with the rest of the args-first family). In 2.0.0 the same signature was interpreted as **metadata**. Callers that relied on the 2.0 semantic must switch to the four-arg form with the named `metadata:` argument: `b.AddField("user", arguments: null, metadata: dict, action)`. See the migration note inline in `FieldBuilder.cs`.

### Fixed
- `ValueFormatter.AppendString` now escapes embedded `"`, `\`, and control characters per the GraphQL spec (§ 2.9.4). Previously, a string argument like `"hello \"world\""` rendered as `"hello "world""` — invalid GraphQL that most servers rejected.

### Deprecated
- The classic `NGql.Core.Query` and `NGql.Core.Mutation` types are soft-deprecated via XML doc remarks. They continue to work and produce identical GraphQL; new code should prefer `QueryBuilder.CreateDefaultBuilder` and `QueryBuilder.CreateMutationBuilder`. There is no removal timeline.

## [2.0.0] - 2025-12-01

Major release. See [`docs/v2.0.0/RELEASE_NOTES.md`](docs/v2.0.0/RELEASE_NOTES.md) for the full write-up.

### Added
- `PreservationBuilder` — extract a subset of an existing `QueryBuilder` by string path (`Preserve("user.name", …)`) or by typed C# expression (`PreserveFromExpression<T>(x => x.user.email != null)`). Useful for role-based filtering, conditional fragments, and stripping fields from a shared query without rebuilding it.
- `MergingStrategy` enum — `MergeByDefault`, `MergeByFieldPath`, `NeverMerge`. Controls how `Include(otherBuilder)` handles duplicate paths.
- Multi-target support: `net8.0`, `net9.0`, `net10.0`.

### Changed
- Internal rendering and merging path was overhauled for performance: in-place merge in `Include()`, lock-free reads on `FieldChildren`, span-based path parsing, `ArrayPool<T>`-backed argument storage, two-level path-index cache. Query rendering is reflection-free; reflection is confined to the LINQ-expression preservation path.
- `Preserve(...)` moved off `QueryBuilder` onto a dedicated `PreservationBuilder` (call `PreservationBuilder.Create(builder)` to start).

### Removed
- Minimum runtime raised from .NET 6/7 to **.NET 8**.
- `QueryDefinitionExtensions` is now `internal` (was effectively unused outside the assembly).
- `PreserveExtensions` is internal — use `PreservationBuilder` instead.

[Unreleased]: https://github.com/dolifer/NGql/compare/2.1.0...HEAD
[2.1.0]: https://github.com/dolifer/NGql/compare/2.0.0...2.1.0
[2.0.0]: https://github.com/dolifer/NGql/compare/1.5.0...2.0.0
