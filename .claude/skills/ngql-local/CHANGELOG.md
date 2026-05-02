# Changelog — NGql Claude Code Skill

All notable changes to the NGql Claude Code Skill (`ngql` / `ngql-preview` channels) are recorded here. The Skill versions independently of `NGql.Core`; see the [library CHANGELOG](https://github.com/dolifer/NGql/blob/main/CHANGELOG.md) for library and CLI changes.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this Skill follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Tagged in git as `skill-v<X.Y.Z>` (or `skill-v<X.Y.Z>-preview.N` for previews).

## [Unreleased]

### Changed
- Skill may now invoke the `ngql` CLI on the user's behalf when explicitly asked ("send this", "run that", "execute it"). Other binaries — `which`, `dotnet tool list`, `curl`, etc. — require the Skill to surface its intent and ask first before running. Earlier preview rules (`.7`–`.14`) were too absolute and forced the user to copy-paste commands even for "run that for me" requests; this pivot keeps the safety (no silent diagnostics) while restoring the natural "you ask, I run" UX.
- Skill confirms non-localhost endpoint URLs and `--allow-mutations` once per session before its first run, since the cost of an unintended POST is high and the cost of one extra confirmation is low.
- Skill reports `ngql` exit codes and stderr verbatim on failure, mapping each to a concrete next step (compile fix for exit 1, error interpretation for exit 2, install command for exit 127, etc.). One run per ask, no auto-retry loops.
- Skill no longer treats exit 0 as automatic success. If the response body looks like HTML, an echo dump, or anything other than a JSON object with a `data` field, the Skill calls this out instead of claiming the query worked — caught when the user pointed `--execute` at webhook.site and the tool correctly printed the HTML body verbatim, but the Skill's interpretation needed nuance.
- When `ngql` is missing (exit 127), Skill asks two questions before suggesting an install command — channel (stable vs preview) and scope (local vs global). The Skill defaults the channel to match its own plugin name (preview Skill → preview tool, stable Skill → stable tool) and defaults scope to local (per-project tool manifest, version-pinned, reproducible). Also flags the `~/.dotnet/tools/` PATH issue as an alternative cause of "command not found."
- On version conflicts (`requested version is lower than existing version`), Skill asks the user to choose: keep the higher local version (no-op) or downgrade by running `uninstall + install` for `dotnet-ngql` only. No automatic resolution; no other tools are touched.

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
