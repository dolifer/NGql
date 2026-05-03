---
layout: default
title: Claude Code Skill — NGql
description: The companion Claude Code skill that translates natural language and pasted GraphQL into NGql code.
---

<p><a href="{{ '/' | relative_url }}">← back to home</a></p>

# `ngql-preview` Skill

A [Claude Code](https://docs.claude.com/en/docs/claude-code) skill that teaches Claude to author NGql query-builder C# from:

- **Natural language** — *"Get the top 10 repos for user X with stargazer counts"* → a `QueryBuilder` snippet using the fluent API.
- **Pasted GraphQL or curl** — preserves operation name, variable types, enum literals, nested arguments, inline fragments via `OnType`. Auth headers from a curl get captured for the next `--execute` offer so you don't retype credentials.
- **Refactor / preserve requests** — *"Drop `ssn` and `email` from this builder"* → a `PreservationBuilder.Preserve(...)` chain that keeps everything else.

It's prescriptive: it picks one safe API idiom per situation and refuses to generate code for unsupported GraphQL constructs (named fragments, directives, subscriptions). When the request needs something NGql can't model, the skill stops, names the gap, and offers concrete workarounds — instead of producing broken code with a warning.

Pairs naturally with the [`dotnet-ngql` CLI](install/#cli-tool-dotnet-ngql) to verify what it generated. The skill proactively offers to render every snippet via `echo '<snippet>' | ngql` (asking once per session for render-only; always asking per call for `--execute` since that touches a server).

---

## Install

```text
/plugin marketplace add dolifer/claude-plugins
/plugin install ngql-preview@dolifer
```

Then invoke as `/ngql-preview:ngql` in any Claude Code session. To pull updates later:

```text
/plugin marketplace update
/plugin update ngql-preview@dolifer
/reload-plugins
```

---

## Channels

| Channel | Plugin name | Status |
|---|---|---|
| **Preview** | `ngql-preview` | Live — tracks the latest `NGql.Core` preview. Allowed to break and iterate. |
| **Stable** | `ngql` | Coming soon — first stable release gates on the skill content stabilizing in preview. |

Both can coexist in the same Claude Code session: `/ngql:ngql` invokes stable, `/ngql-preview:ngql` invokes preview. Useful for comparing behavior or A/B-ing a skill change before promoting.

---

## Full skill documentation

The skill marketplace site has the canonical docs, frontmatter, version, and changelog:

**→ [dolifer.github.io/claude-plugins/skills/ngql-preview/](https://dolifer.github.io/claude-plugins/skills/ngql-preview/)**

Source-of-truth for editing the skill content lives in this repo at [`.claude/skills/ngql-local/SKILL.md`](https://github.com/dolifer/NGql/blob/main/.claude/skills/ngql-local/SKILL.md). The catalog repo receives the published content via CI on every push to `main` that touches the skill folder.

---

## Reporting skill issues

Open in [dolifer/NGql/issues](https://github.com/dolifer/NGql/issues) with the `skill` label. The skill content is generated from this repo, so a fix lands in the marketplace catalog automatically on the next preview publish.
