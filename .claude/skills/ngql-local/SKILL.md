---
name: ngql-local
description: |
  **Editable in-repo copy of the NGql Skill.** Use when the user is running Claude Code inside the NGql repository and is actively editing this Skill or the library it teaches. Tracks the working tree, not a published release — the content here may be ahead of `ngql-preview` and `ngql` (the published channels). Translates the user's GraphQL intent into NGql query-builder C# code: triggered by "build a query", a pasted GraphQL operation or curl that needs porting to NGql, "filter / preserve fields", or mentions of QueryBuilder / Mutation / PreservationBuilder / NGql. NGql is a zero-dependency, schema-less .NET GraphQL query builder — the user supplies the schema knowledge (field names, types), the Skill produces the fluent code.
---

# NGql Skill

Generate NGql query-builder C# from natural language, pasted GraphQL/curl, or a refactor request. **You don't own the schema** — render the user's intent, never invent fields.

## Three modes

| Trigger | Action |
|---|---|
| Natural language ("get top 10 repos…") | Generate a builder. Well-known APIs (GitHub, Stripe, public schemas) → assume + flag in one line. Unknown APIs → ask for field/arg names first. |
| Pasted GraphQL or curl | Port verbatim — preserve operation name and variable types. Curl bodies are usually `{"query":"…","variables":{…}}` JSON; extract first. |
| Refactor / preserve | Use `PreservationBuilder.Create(src).Preserve("a","b").Build()`. **`Preserve` is inclusive** — for "drop X" requests, list everything except X. |

If the mode isn't obvious, ask before generating.

## API surface

```csharp
using NGql.Core;            // Variable, EnumValue, MergingStrategy
using NGql.Core.Builders;   // QueryBuilder, FieldBuilder, PreservationBuilder

// Operations
QueryBuilder.CreateDefaultBuilder("Op");                                       // query
QueryBuilder.CreateMutationBuilder("Op");                                      // mutation
QueryBuilder.CreateDefaultBuilder("Op", MergingStrategy.MergeByFieldPath);     // optional strategy
```

`MergingStrategy` controls `Include(other)` behavior: `MergeByDefault` (inherit), `MergeByFieldPath` (merge, auto-alias on arg conflict), `NeverMerge`.

### `AddField` — these are the only shapes to use

```csharp
.AddField("path.to.field")                                            // simple, dot-paths nest
.AddField("users", new Dictionary<string, object?> { … })             // args
.AddField("users", new[] { "id", "name" })                            // sub-fields
.AddField("users", new Dictionary<string, object?> { … },             // args + sub-fields (args FIRST)
                   new[] { "id", "name" })
.AddField("user", b => b.AddField("name").AddField("email"))          // sub-field lambda
.AddField("user", new Dictionary<string, object?> { … },              // args + lambda
                   b => b.AddField("name"))
.AddField("amount", "Money!")                                         // typed leaf (rare)
```

Inside a lambda, call the same shapes on `b`. **Args go BEFORE sub-fields**, same as outside.

### Variables, enums, fragments, metadata

```csharp
var idVar = new Variable("$id", "ID!");                               // $-prefix required
.AddField("user", new Dictionary<string, object?> { ["id"] = idVar }, // auto-promotes to op signature
                   new[] { "name" })

.AddField("users", new Dictionary<string, object?> {
    ["role"] = new EnumValue("ADMIN")                                 // unquoted on wire: role:ADMIN
})

// Inline fragment for union/interface narrowing — renders `... on Type { … }`
.AddField("nodes", n => n.OnType("Repository", r => r.AddField("name")))

// Named fragment — declared once at op level, spread (`...Name`) at each use site
QueryBuilder.CreateDefaultBuilder("GetUsers")
    .AddFragment("UserCard", "User", f => f.AddField("id").AddField("name"))
    .AddField("users", u => u.SpreadFragment("UserCard"))
    .AddField("admins", a => a.SpreadFragment("UserCard"))

// Metadata — via lambda + WithMetadata, NEVER as a positional dict
.AddField("user", new Dictionary<string, object?> { ["id"] = idVar }, b => b
    .WithMetadata(new Dictionary<string, object> { ["cached"] = true })
    .AddField("name"))
```

### `PreservationBuilder`

```csharp
PreservationBuilder.Create(src).Preserve("user.name", "user.email").Build();
PreservationBuilder.Create(src).PreserveAtPath("createdAt", "user.posts").Build();
PreservationBuilder.Create(src).PreserveFromExpression<UserDto>(x => x.user.email != null).Build();
```

`Build()` returns a fresh `QueryBuilder`; the source is never mutated. The `T` for `PreserveFromExpression<T>` must mirror the query shape — don't synthesize the DTO without seeing it or being told.

### Composition

```csharp
QueryBuilder.CreateDefaultBuilder("Combined", MergingStrategy.MergeByFieldPath)
    .Include(fragmentA)
    .Include(fragmentB);
```

### Don't generate

- `IDictionary<string, object?>` literals — overloads take `Dictionary<string, object?>`
- `using` directives for namespaces you don't reference (and never the auto-imports — see snippet contract)
- `try/catch`, feature flags, or "future-proofing" — let bad input throw
- Code comments unless asked
- GraphQL alongside the C# unless asked — use `ngql` to render
- **`var query = …;` followed by anything** — the snippet must end in a bare expression, never a statement. Drop the `var` and let the builder be the last expression.
- **`Console.WriteLine(…)`** — `ngql` calls `.ToString()` itself. Adding `Console.WriteLine` either won't run or duplicates output. If the user asks for "a complete program" rather than an `ngql` snippet, that's a different request — confirm before generating it.
- **`return …;`** at the script root — Roslyn scripts disallow it. The bare expression *is* the return.

## Value types

### In the C# snippet (argument literals)

| GraphQL | CLR | Renders |
|---|---|---|
| `Int` | `int`/`long`/`short` (& unsigned) | `42` |
| `Float` | `float`/`double`/`decimal` | `3.14` |
| `String` / `ID` | `string` | `"alice"` (quotes/backslashes/control chars escaped) |
| `Boolean` | `bool` | `true` / `false` |
| `null` | `null` | `null` |
| **enum** | `new EnumValue("ADMIN")` | `ADMIN` (unquoted) |
| **variable** | `new Variable("$id", "ID!")` | `$id` |
| **list** | `new[] { … }` / `IList` | `[1, 2, 3]` |
| **input object** | `new Dictionary<string, object?>` | `{k:v, …}` (alphabetized keys) |
| **date** | `DateTime` / `DateTimeOffset` | ISO-8601 quoted; verify against server's custom-scalar contract |

CLR enums work but render as `.ToString()` — prefer `EnumValue("EXACT_NAME")` since GraphQL convention is `SCREAMING_SNAKE_CASE` and C# is `PascalCase`. Nest dicts/lists freely; variables can sit anywhere a value can.

### In `--var key=value` (execute mode)

The tool JSON-parses each value; bare strings fall through.

```bash
--var first=10              # number
--var active=true           # bool
--var maybe=null            # null
--var name=alice            # bare string (parse failed)
--var name="Anne Ware"      # string with spaces
--var ids='[1,2,3]'         # list — single-quote JSON to protect from shell
--var filter='{"min":10}'   # input object — same
```

`Variable("$x", "T")` in a snippet → `--var x=value` on the CLI (no `$`).

## Output rules

NGql renders with: 4-space indent, tight braces (`users{` not `users {`), fields **sorted alphabetically** within a selection set, aliases appended after the un-aliased duplicate, strings double-quoted, enums unquoted, variables as `$name:Type` in the operation signature.

## Snippet contract for `ngql`

The CLI evaluates a snippet as a Roslyn script: **the final expression yields the builder**, full stop.

- **No** `var x = ...; ...` (drop the assignment, end with the bare builder expression)
- **No** `Console.WriteLine(...)` (the tool prints `.ToString()` itself)
- **No** `return` (top-level scripts disallow it)
- **No `using` lines for the auto-imported namespaces.** `ngql` already imports `System`, `System.Collections.Generic`, `System.Linq`, `NGql.Core`, `NGql.Core.Builders`. Adding `using NGql.Core.Builders;` to a generated snippet is dead bytes — strip it. Only add `using` for namespaces you actually reference that aren't on that list (rare in practice — `System.Text.Json` if a value-formatter need it). The snippets in the worked examples below show the canonical bare shape.

Canonical shape:

```csharp
QueryBuilder.CreateDefaultBuilder("Hello").AddField("world.name")
```

## Feature gaps — refuse, don't fake

| Construct | NGql |
|---|---|
| Inline fragments / union narrowing | ✅ `FieldBuilder.OnType("TypeName", b => …)` |
| Named fragments (`fragment X on T`, `...X`) | ✅ `QueryBuilder.AddFragment(name, onType, build)` + `FieldBuilder.SpreadFragment(name)` |
| `@include` / `@skip` directives | ❌ — [#23](https://github.com/dolifer/NGql/issues/23). Restructure conditional branches at the C# level. |
| Custom directives | ❌ — file-separately if needed; uncommon in real APIs. |
| Subscriptions | ❌ — out of scope (transport-layer concern). |
| `Include` + any fragments | ❌ — throws `NotSupportedException`. Build the merged query without fragments, or apply `Include` *before* adding fragments. |

When the user needs a ❌ construct: **stop before any C#**, name the gap in one sentence, offer concrete paths (inline equivalent, partial + hand-splice, different field). Wait for the user's pick. **Never** generate broken code "for reference" — code that looks right but renders to invalid GraphQL is the worst failure mode.

## When to ask before writing code

- Private/unknown API → ask for field names
- Mutations that change state (`delete`, `transferFunds`) → confirm name + input shape
- Ambiguous "drop X" → confirm which fields count
- Pasted operation has same-name fragments in multiple places → confirm merging vs separation vs aliasing

## `ngql` runtime — when and how to invoke

`dotnet-ngql` compiles a snippet against `NGql.Core` and prints rendered GraphQL. Optionally executes against a live endpoint with `--execute --endpoint URL`.

### Consent rules

- **You may run `ngql`** when the user explicitly asks ("send this", "run that", "execute it"). Other binaries (`which`, `dotnet tool list`, `curl`, `cat`, etc.) require **ask-first** consent — surface the intent, wait for "yes", then run. Never silently shell out for diagnostics.
- **Render** (no `--execute`) is side-effect-free. Ask **once per session**: *"Want me to render this with `ngql`? (After yes, I'll auto-render later snippets — they're side-effect-free.)"* Then auto-run subsequent renders. Revoke on user request.
- **Execute** (`--execute`) is **always opt-in per call** — network + possible side effects. Never auto-run.

### Proactive offer after every snippet

One short sentence after the snippet block:

- *"Want me to render this — `echo '<snippet>' | ngql`?"* (or after first render-yes: just *"Rendering…"* and run)
- If the user gave an endpoint, also propose execute separately: *"Or run against `<URL>` — `echo '<snippet>' | ngql --execute --endpoint <URL>`?"*

**Default to stdin** (`echo '…' | ngql`) — one Bash call, one permission prompt, no file write. Use file path (`ngql snippet.cs`) only for long snippets (~30+ lines, intricate quoting). Mention briefly when you do: *"Long enough to write to `snippet.cs` first."*

### Endpoint + mutation safety

Before the **first** execute call in a session, confirm:
1. The endpoint URL — read it aloud (`localhost`, `staging`, `prod`, third-party); pause for go-ahead unless localhost or a clear sandbox.
2. For mutations, that `--allow-mutations` is intentional. Frame the side-effect risk:
   > Heads up — this is a mutation, it'll actually change data. If the endpoint is a sandbox / you have a backup, pass `--allow-mutations`. Otherwise drop `--execute` to dry-run.

Never pre-add `--allow-mutations` unless the user has explicitly confirmed.

### Reading exit codes

| Exit | Meaning | Next step |
|---|---|---|
| 0 | OK | If response body looks like HTML / echo dump / not a JSON `data` object — **call it out**. Don't claim success. |
| 1 | Snippet failed to compile | Offer to fix the snippet. Common causes: `IDictionary<,>` instead of `Dictionary<,>`, `EnumValue(MyEnum.X)` casing mismatch, an extra `var x = …; …` that left the file ending in a statement instead of a bare expression. |
| 2 | Server returned `errors` array | Interpret the GraphQL errors. |
| 3 | HTTP failure | Surface status code + stderr. |
| 4 | Mutation blocked | Offer `--allow-mutations` form (with safety re-check). |
| 127 | "command not found" | Either tool not installed OR `~/.dotnet/tools/` not on `$PATH` — check both. See install flow below. |

Always report the actual exit code + stderr verbatim. **One run, one report — no auto-retry loops.** When you announce an action ("running `which ngql`"), perform exactly that — never substitute a different tool.

If the user says "no response" / "didn't work" / "nothing happened" *without* asking you to do anything: ask whether they actually ran the command first. Don't assume execution and pivot to diagnostics.

### Install flow (when `ngql` is missing)

Ask **two** questions before suggesting a command — channel and scope.

**Channel**: default to whichever matches *this Skill's own plugin name* — `/ngql-preview:ngql` → preview, `/ngql:ngql` → stable. Frame as confirmation: *"You're using the preview Skill, so I'd install the preview tool to match — sound right?"*

| Channel | Install | Note |
|---|---|---|
| stable | `dotnet tool install -g dotnet-ngql` | No stable on NuGet yet — install fails until it ships. |
| preview | `dotnet tool install -g dotnet-ngql --prerelease` | The `--prerelease` flag is required. |

**Scope**: default-suggest **local** (per-project, version-pinned, reproducible):

- **Local**: `dotnet new tool-manifest` then `dotnet tool install dotnet-ngql [--prerelease]`. Invoke as `dotnet ngql ...`.
- **Global**: `dotnet tool install -g dotnet-ngql [--prerelease]`. Invoke as `ngql ...`.

Ask: *"Local (recommended — pins the version) or global?"* Wait for answer.

**Version conflict** (`requested version is lower than existing version`): ask user — keep their higher local OR `dotnet tool uninstall [-g] dotnet-ngql && dotnet tool install [-g] dotnet-ngql [--prerelease]` (uninstall + reinstall, only `dotnet-ngql` touched).

**`command not found` after install**: PATH issue. Global → `export PATH="$PATH:$HOME/.dotnet/tools"` (current shell) or persist in `~/.zshrc` / `~/.bashrc`. Local → invoke as `dotnet ngql …`, not bare `ngql`.

## Worked examples

### NL → builder, well-known API

> "Get the top 10 repositories of GitHub user `dolifer` with name and stargazer count."

```csharp
QueryBuilder.CreateDefaultBuilder("TopRepos")
    .AddField("user",
        new Dictionary<string, object?> { ["login"] = "dolifer" },
        b => b.AddField("repositories",
            new Dictionary<string, object?>
            {
                ["first"] = 10,
                ["orderBy"] = new Dictionary<string, object?>
                {
                    ["field"]     = new EnumValue("STARGAZERS"),
                    ["direction"] = new EnumValue("DESC"),
                },
            },
            new[] { "name", "stargazerCount" }))
```

> Assumed schema (GitHub v4): `user(login).repositories(first, orderBy) { name, stargazerCount }`. Swap if your schema differs.

### Mutation with variables

> "`CreateUser` taking `$name`/`$email`, returning `id` and `createdAt`."

```csharp
var nameVar  = new Variable("$name",  "String!");
var emailVar = new Variable("$email", "String!");

QueryBuilder.CreateMutationBuilder("CreateUser")
    .AddField("createUser",
        new Dictionary<string, object?> { ["name"] = nameVar, ["email"] = emailVar },
        new[] { "createdAt", "id" })
```

### curl → builder (with auth pass-through)

When the user pastes a curl, extract three things and remember them for the rest of the session:

1. **The GraphQL operation** (curl body's `query` field) → the snippet.
2. **The endpoint** (curl URL) → the `--endpoint` for any later `--execute` offer.
3. **Auth + custom headers** (`-H 'authorization: …'`, `-H 'x-api-key: …'`, etc.) → `-H` flags ready to attach to `--execute`. Don't echo secret values back; refer to them as "the header you pasted."

Example input:

```bash
curl https://api.example.com/graphql \
  -H 'authorization: Bearer XXX' \
  -d '{"query":"query GetOrder($id: ID!) { order(id: $id) { id status total } }","variables":{"id":"42"}}'
```

Generated snippet:

```csharp
var idVar = new Variable("$id", "ID!");

QueryBuilder.CreateDefaultBuilder("GetOrder")
    .AddField("order",
        new Dictionary<string, object?> { ["id"] = idVar },
        new[] { "id", "status", "total" })
```

When you offer to execute, propose with the captured pieces:

> *"Want me to run it against `https://api.example.com/graphql` with the `authorization` header you pasted — `echo '<snippet>' | ngql --execute --endpoint https://api.example.com/graphql -H 'authorization: Bearer XXX' --var id=42`?"*

Standard execute consent rules apply (always ask; confirm endpoint isn't surprising; flag mutations).

### Inline fragment (union narrowing)

> "Search GitHub for top 10 starred repos, return name + stargazerCount + url."

```csharp
QueryBuilder.CreateDefaultBuilder("TopRepos")
    .AddField("search",
        new Dictionary<string, object?>
        {
            ["query"] = "stars:>1",
            ["type"]  = new EnumValue("REPOSITORY"),
            ["first"] = 10,
        },
        b => b.AddField("nodes", n => n
            .OnType("Repository", r => r
                .AddField("name").AddField("stargazerCount").AddField("url"))))
```

The `"Repository"` is a schema type name, not user-defined. Multiple `OnType` calls on the same parent merge; fragments can nest (`OnType("X", x => x.OnType("Y", …))`).

### Named fragment (DRY across multiple use sites)

> "Build a query that fetches both users and admins, each returning the same id+name+avatarUrl selection."

```csharp
QueryBuilder.CreateDefaultBuilder("GetUsersAndAdmins")
    .AddFragment("UserCard", "User", f => f
        .AddField("id").AddField("name").AddField("avatarUrl"))
    .AddField("users", u => u.SpreadFragment("UserCard"))
    .AddField("admins", a => a.SpreadFragment("UserCard"))
```

Renders the fragment once after the operation block, spread at each use site. Fragment names are case-sensitive; `AddFragment` with a duplicate name + different `onType` throws. NGql doesn't validate that every spread points at a declared fragment — undeclared spreads render verbatim and the server rejects them (NGql is schemaless).
