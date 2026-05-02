# Changelog — NGql Claude Code Skill

All notable changes to the NGql Claude Code Skill (`ngql` / `ngql-preview` channels) are recorded here. The Skill versions independently of `NGql.Core`; see the [library CHANGELOG](https://github.com/dolifer/NGql/blob/main/CHANGELOG.md) for library and CLI changes.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this Skill follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Tagged in git as `skill-v<X.Y.Z>` (or `skill-v<X.Y.Z>-preview.N` for previews).

## [Unreleased]

### Changed
- Skill no longer attempts to invoke `ngql` (or any user binary) via Bash on the user's behalf — it produces the command line and the user runs it. Triggered by a real session where the Skill ran `which ngql` after the user reported "no result," obscuring that the tool wasn't installed.

## [1.0.0] - 2026-05-02

Baseline release. Establishes the published Skill as a versioned artifact distributed via the [`dolifer/claude-plugins`](https://github.com/dolifer/claude-plugins) marketplace.

### Added
- Skill `ngql` (stable) and `ngql-preview` (preview) — same content under different plugin names so both channels coexist in one Claude Code session.
- Three modes covered by the Skill: natural-language → `QueryBuilder` snippet, GraphQL/curl → `QueryBuilder` snippet, and refactor via `PreservationBuilder`.
- Prescriptive value-type matrix for both C# argument literals and `--var` shell strings (numbers, bools, enums, lists, nested input objects, variables).
- Verification flow via the `dotnet-ngql` CLI: render-only (`ngql snippet.cs`) and execute-mode (`ngql snippet.cs --execute --endpoint URL`) with mutation-safety opt-in.
- Independent SemVer stream tagged `skill-v*`, decoupled from `NGql.Core` versioning. Skill content can ship without library releases and vice versa.

[Unreleased]: https://github.com/dolifer/NGql/compare/skill-v1.0.0...HEAD
[1.0.0]: https://github.com/dolifer/NGql/releases/tag/skill-v1.0.0
