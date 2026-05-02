# Skill behavioral eval

A list of prompts that exercise specific Skill rules. Run by hand (or via a sub-agent in
Claude Code) whenever SKILL.md changes meaningfully. Each entry names the rule under test
and the expected behavior category, NOT the exact response — Claude paraphrases.

These are NOT deterministic fixtures. They're behavioral checks. The deterministic
contract for what `ngql` itself renders lives in `tools/Tool/Fixtures/`.

## How to run one

In a Claude Code session that has the Skill installed:

```text
/ngql-preview:ngql <prompt-from-the-table-below>
```

Compare the response to the "expected behavior" column. If it diverges, the Skill content
needs tightening — the failure mode is usually a rule that's too vague or a counter-example
the rule didn't anticipate.

To run from outside Claude Code (for CI-ish drift checks), spawn a sub-agent with the
Skill file as context:

```
Read /Users/<…>/NGql/.claude/skills/ngql-local/SKILL.md and behave as the skill instructs.
The user has just sent: "<prompt>"
Output the response you would give the user.
```

## Eval prompts

| # | Prompt | Rule under test | Expected behavior |
|---|---|---|---|
| 1 | `build a query that fetches a user's name and email` | NL → builder, common case | Generates a `QueryBuilder.CreateDefaultBuilder("…").AddField("user", new[] { "email", "name" })`-shaped snippet. Calls out the schema assumption ("assumed root field is `user`"). |
| 2 | `port this to NGql: query GetUser($id: ID!) { user(id: $id) { name email } }` | GraphQL → builder, variables | Generates the snippet with a `Variable("$id", "ID!")` reference; preserves operation name. |
| 3 | `build a CreateUser mutation taking $name (String!) and $email (String!), returning id and createdAt` | NL → mutation | Uses `CreateMutationBuilder` (NOT the deprecated `new Mutation(…)`); declares both variables. |
| 4 | `port: query Search { users(first: 10, role: ADMIN) { id name } }` | GraphQL → builder, enum literal | Uses `new EnumValue("ADMIN")` (unquoted enum on the wire), not a bare string. |
| 5 | `take a fullProfile builder containing user.name, user.email, user.ssn, user.avatar — give me a public view with only name and avatar` | refactor / preserve | Uses `PreservationBuilder.Create(...).Preserve("user.name", "user.avatar").Build()`. Doesn't try to mutate the source builder. |
| 6 | `build GetTopRepos: first 10 repos of GitHub user 'dolifer', ordered by stargazers desc, returning name and stargazerCount` | nested input objects, args + lambda | Uses the `(field, args, lambda)` overload (no `metadata: null` boilerplate). Inner `FieldBuilder.AddField` uses args-first ordering. |
| 7 | `tag the user field with telemetry metadata { 'cached': true, 'tag': 'user-v2' }` | metadata via lambda + WithMetadata | Uses `b.WithMetadata(...)` inside a sub-field lambda — does NOT pass metadata as a positional dict. |
| 8 | `get a list of top 10 repositories on GitHub` | inline fragment generation | Generates a `QueryBuilder` snippet using `b.AddField("nodes", n => n.OnType("Repository", r => …))`. Asks one clarifying question first ("top 10 by what — stars, forks, recent activity?"), then generates clean code without refusal. The schema-narrowing happens via `OnType`, not as a feature gap. |
| 9 | `build a subscription PriceChanged that watches a price feed` | feature gap (subscriptions) | **Refuses.** Says NGql doesn't render subscriptions; doesn't offer to fake it. |
| 10 | `add @include(if: $verbose) to the user.email field` | feature gap (directives) | **Refuses or restructures.** Says NGql doesn't have first-class directives. Offers to generate the body without them. |
| 16 | `build a query that fetches both users and admins, each returning the same id+name+avatarUrl selection — use a fragment to avoid repetition` | feature gap (named fragments — issue #20) | **Refuses to generate the named-fragment form.** Offers to inline the selection set at each use site (works, just duplicates the fields). Mentions the issue tracking the gap. |
| 11 | (after producing a snippet for an unknown private API) — `does this work?` | schema honesty | Acknowledges schema was assumed; asks the user to confirm the field names against their actual server before claiming the snippet works. |
| 12 | (after producing a `--execute` command for a non-localhost endpoint) — `run that for me` | endpoint confirmation | Confirms the URL once before executing if it's not localhost or a clear sandbox. |
| 13 | (after `ngql` exits 127) — `try installing it` | install flow | Asks two questions before installing: channel (stable vs preview, default to whichever matches the active Skill — `/ngql-preview:` → preview) and scope (local vs global, default-suggest local). Doesn't pick automatically. |
| 14 | (after `ngql` returns exit 0 against `webhook.site`) — display only | exit-0 nuance | Calls out that the response body isn't a GraphQL response (HTML / echo body); doesn't claim "query worked." |
| 15 | (no command was actually run by the user) — `nothing happened` | execution-happened check | First asks whether the user actually ran the command, before pivoting to root-cause diagnosis. |

## Adding a new eval prompt

When the Skill grows a new rule, add a row here so future re-runs catch regressions. Keep
prompts terse and the "expected behavior" column free of literal text — describe the
*shape* of the right answer, not the exact wording, since Claude varies per session.
