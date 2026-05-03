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
- New rule: snippets generated for `ngql` must end in a bare expression (no `var query = …; Console.WriteLine(query)`, no `return`). The Skill documented this in a dedicated section because three iterations were needed in a real session before the snippet ran — the first attempt added `Console.WriteLine`, the second still had a `var = ` assignment, the third tried `return` (also wrong for C# scripts).
- New rule: when an action is announced ("running `which ngql`"), perform exactly that action — don't substitute a different tool (e.g. file-search Grep). Caught when a "want me to check?" exchange ran a search instead of the announced shell command.
- Added a "feature gaps" table explicitly listing GraphQL constructs NGql does NOT support — inline fragments, named fragments, directives, subscriptions, type narrowing on unions/interfaces. Skill must surface the gap up front and either offer the closest equivalent or refuse, instead of generating code that pretends to support the construct (e.g. `.AddField("... on Repository")` which renders as a literal field name `Repository`).
- Tightened the gap rule: when an unsupported construct is needed, the Skill now must **stop before generating any C#** — no broken snippet "for reference," no "here's what NGql can do alongside the warning." The previous wording let the Skill produce both the broken snippet and the warning side-by-side, which still tempted copy-paste of code that renders to wrong GraphQL. Generate code only after the user has picked a workaround path.
- Added `EVAL.md` next to SKILL.md: a behavioral checklist of prompts that exercise specific Skill rules (NL → builder, mutation, enum, preserve, gap-refuse for unsupported constructs, install flow, exit-code nuances, etc.). Run by hand or via a sub-agent whenever SKILL.md changes meaningfully. Not shipped to the catalog — contributor doc only.

### Added
- **Inline fragment generation now works.** With NGql 2.1's `FieldBuilder.OnType("TypeName", b => …)` shipping, the Skill removes inline fragments and union/interface narrowing from the gap table and adds a worked example covering the canonical GitHub-search use case. The "feature gap" rule still applies to named fragments, directives, and subscriptions. EVAL prompt #8 (top-10 GitHub repos) flips from "refuses to generate" to "generates clean snippet using OnType." A new EVAL prompt #16 covers named fragments — those still refuse, pointing at https://github.com/dolifer/NGql/issues/20.
- **Skill now proactively offers `ngql` after every snippet, with asymmetric consent for render vs execute.** Render is side-effect-free (compile + print GraphQL): ask once per session, then auto-render subsequent snippets. Execute touches a server (with possible side effects, especially mutations): ask every time, no exceptions. Both forms default to stdin (`echo '<snippet>' | ngql …`) because it's one Bash call, one permission prompt, no file write. File-path form (`ngql snippet.cs`) reserved for snippets too long to inline-echo cleanly. The Render-only and Execute sections were also rewritten to lead with stdin.

### Changed
- **SKILL.md compacted from 549 lines to ~330 (40% smaller).** Same 50+ behavioral rules, same coverage — collapsed prose, dropped redundant tables, dropped per-rule explanations Claude infers from context. Worked examples reduced from 5 to 4 (the four that actually surface different idioms — well-known NL, mutation, curl-with-auth, inline-fragment narrowing). Curl import now also captures the endpoint URL and any `-H` headers from the paste, so a later `--execute` offer can reuse them without asking the user to retype credentials. Smoke-tested via sub-agent with five representative prompts (NL query, mutation, curl-with-auth, subscription-refusal, GitHub union narrowing) — all five behaviors land cleanly on the first try.

### Removed
- All mention of legacy `Query` / `Mutation` types and migration guidance from the classic API. The Skill teaches `QueryBuilder.CreateDefaultBuilder` and `CreateMutationBuilder` exclusively; new users shouldn't even see the deprecated names. Existing user code that still references the classic types remains supported by NGql.Core itself — but the Skill won't generate it or help convert it.

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
