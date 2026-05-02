---
name: ngql-local
description: |
  **Editable in-repo copy of the NGql Skill.** Use when the user is running Claude Code inside the NGql repository and is actively editing this Skill or the library it teaches. Tracks the working tree, not a published release — the content here may be ahead of `ngql-preview` and `ngql` (the published channels). Translates the user's GraphQL intent into NGql query-builder C# code: triggered by "build a query", a pasted GraphQL operation or curl that needs porting to NGql, "filter / preserve fields", or mentions of QueryBuilder / Mutation / PreservationBuilder / NGql. NGql is a zero-dependency, schema-less .NET GraphQL query builder — the user supplies the schema knowledge (field names, types), the Skill produces the fluent code.
---

# NGql Query Builder Skill

You are generating NGql query-builder code for a .NET project. **NGql does not own a schema.** The user knows their GraphQL API. Your job is to render their intent as fluent NGql calls — not to invent fields, types, or arguments they didn't supply.

## Three modes

Pick one based on the user's input. If you can't tell, ask before generating code.

### Mode 1 — Natural language → builder

> "Get the top 10 repositories of GitHub user `dolifer`, with stargazer counts."

1. Decide whether the target API is **well-known** (GitHub, Stripe, Shopify, public schemas in the training data) or **unknown** (private API).
   - Well-known: proceed and call out which fields you assumed in a one-line "assumed schema" note.
   - Unknown: ask the user for the field names / arg names you need before writing code. Don't guess.
2. Extract: operation name, root field, arguments (name + value + GraphQL type for variables), nested fields.
3. Generate using the builder API: `QueryBuilder.CreateDefaultBuilder(...)` for queries, `QueryBuilder.CreateMutationBuilder(...)` for mutations. Both share the same fluent surface (`AddField`, `Include`, `WithMetadata`, etc.) — the only difference is the operation prefix at render time.

### Mode 2 — GraphQL or curl → builder

> "Here's the curl I have today, port it to NGql:" (paste)

1. Extract the GraphQL operation from the input. Curl bodies are usually JSON `{"query": "...", "variables": {...}}` — pull `query` and `variables` out before parsing.
2. Map each construct to NGql:
   - `query Foo($x: ID!)` → `QueryBuilder.CreateDefaultBuilder("Foo")` plus `new Variable("$x", "ID!")` passed as an argument value (auto-promoted to the operation signature).
   - `field(arg: 5, name: "bob")` → `.AddField("field", new Dictionary<string, object?> { ["arg"] = 5, ["name"] = "bob" }, ...)`.
   - Enum literals (unquoted in GraphQL) → `new EnumValue("ADMIN")` as the argument value.
   - Nested selection sets → either dot-paths (`.AddField("user.profile.name")`) or a sub-field lambda (`.AddField("user", b => b.AddField("name"))`). Prefer dot-paths when there are no per-level arguments; use the lambda when intermediate levels carry args or you need readable nesting.
   - Fragments / multiple operations → split into separate builders and compose with `.Include(other)`.
   - `mutation` → use `QueryBuilder.CreateMutationBuilder("Name")`. The fluent surface is the same as the query path; only the operation prefix differs at render time.
3. Preserve the original operation name and variable types verbatim — they are the schema contract.

### Mode 3 — Refactor / preserve

> "Take this builder and produce a public view that drops `ssn` and `email`."

1. Use `PreservationBuilder.Create(source).Preserve("path.a", "path.b").Build()` to keep listed paths.
2. For LINQ-driven extraction (typed): `PreserveFromExpression<T>(x => x.user.profile.email != null)`. Mention the `T` model class must mirror the query shape — the Skill cannot synthesize that DTO without seeing or being told its structure.
3. `Preserve(...)` is **inclusive** ("keep these"), not exclusive ("drop these"). If the user says "drop X", flip it: list everything *except* X, or use `PreserveFromExpression` with a predicate that excludes the field.

## API quick reference (current as of NGql 2.1+)

### Namespaces
```csharp
using NGql.Core;            // Variable, EnumValue, MergingStrategy
using NGql.Core.Builders;   // QueryBuilder, FieldBuilder, PreservationBuilder
```

`Query` and `Mutation` types still exist in `NGql.Core` for back-compat, but you should not generate code that uses them — see the "Legacy types" section at the bottom.

### `QueryBuilder` — the fluent API for both queries and mutations

```csharp
QueryBuilder.CreateDefaultBuilder("OperationName");                                  // query Operation { ... }
QueryBuilder.CreateDefaultBuilder("OperationName", MergingStrategy.MergeByFieldPath);

QueryBuilder.CreateMutationBuilder("OperationName");                                 // mutation Operation { ... }
QueryBuilder.CreateMutationBuilder("OperationName", MergingStrategy.MergeByFieldPath);
```

The two factories return the same `QueryBuilder` type with the same fluent methods — `CreateMutationBuilder` only changes the operation prefix at `ToString()` time. Variables, arguments, sub-fields, `Include`, `WithMetadata`, `PreservationBuilder` — all work identically across the two.

`AddField` — pick the shape that matches your intent. These are the **only** call shapes you should generate; together they cover every realistic case.

```csharp
.AddField("path.to.field")                                         // simple, including dot-paths
.AddField("users", new Dictionary<string, object?> { ... })        // with arguments
.AddField("users", new[] { "id", "name" })                         // with sub-field names
.AddField("users", new Dictionary<string, object?> { ... },
                   new[] { "id", "name" })                         // arguments + sub-fields (args FIRST)
.AddField("user", b => b.AddField("name").AddField("email"))       // sub-field lambda
.AddField("user", new Dictionary<string, object?> { ... },
                   b => b.AddField("name"))                        // arguments + sub-field lambda
.AddField("amount", "Money!")                                      // typed leaf (rare)
```

Inside a sub-field lambda you call the same set of shapes on the lambda's `b`. Args go BEFORE sub-fields, exactly the same as on the outer `QueryBuilder`.

**Metadata** (the per-field `Dictionary<string, object?>` for telemetry/tags/etc., separate from GraphQL arguments) is attached via a lambda, not as a positional dict:

```csharp
.AddField("user", new Dictionary<string, object?> { ["id"] = idVar }, b => b
    .WithMetadata(new Dictionary<string, object> { ["cached"] = true })
    .AddField("name"))
```

Never pass a metadata dict as a positional argument — always use the `WithMetadata(...)` step inside a sub-field lambda. (This rule means callers and Claude never have to disambiguate two adjacent `Dictionary<string, object?>` parameters.)

Use `Dictionary<string, object?>` (not `IDictionary<,>`) at the call site — that is what the public overloads declare.

Compose:
```csharp
var combined = QueryBuilder
    .CreateDefaultBuilder("Combined", MergingStrategy.MergeByFieldPath)
    .Include(fragmentA)
    .Include(fragmentB);
```

`MergingStrategy` values:
- `MergeByDefault` — inherit parent
- `MergeByFieldPath` — merge same-path fragments, auto-alias on argument conflict (the default for a fresh `CreateDefaultBuilder`)
- `NeverMerge` — keep every `Include` distinct

### `Variable`

```csharp
var idVar = new Variable("$id", "ID!");      // name *includes* the $ prefix
```

Pass the `Variable` instance as an argument *value* and it is auto-promoted to the operation's variable list:

```csharp
.AddField("user", new Dictionary<string, object?> { ["id"] = idVar }, new[] { "name" })
```

### `EnumValue`

```csharp
.AddField("users", new Dictionary<string, object?> {
    ["role"] = new EnumValue("ADMIN")    // renders unquoted: role:ADMIN
})
```

Without `EnumValue`, a string would render as `"ADMIN"` (quoted). Always wrap GraphQL enum literals.

### `PreservationBuilder`

```csharp
var publicView = PreservationBuilder.Create(fullQuery)
    .Preserve("user.name", "user.email")
    .Build();

var conditional = PreservationBuilder.Create(fullQuery)
    .PreserveFromExpression<UserDto>(x => x.user.profile.email != null)
    .Build();

var scoped = PreservationBuilder.Create(fullQuery)
    .PreserveAtPath("createdAt", "user.posts")   // only keep `createdAt` under `user.posts`
    .Build();
```

`Build()` returns a fresh `QueryBuilder` — the source is never mutated.

### Legacy types — `Query` and `Mutation` (do not generate)

The `NGql.Core.Query` and `NGql.Core.Mutation` classes are the NGql 1.x classic API. They still render correctly and are not removed, but **new code should not use them** — `QueryBuilder.CreateDefaultBuilder` and `QueryBuilder.CreateMutationBuilder` cover both surfaces with a richer fluent API. If you encounter user code that uses these types, you can read it (to extract intent) but do not produce more of it. When porting, map:

| Classic | New |
|---|---|
| `new Query("Foo", vars)` | `QueryBuilder.CreateDefaultBuilder("Foo")` |
| `new Mutation("Foo", vars)` | `QueryBuilder.CreateMutationBuilder("Foo")` |
| `.Where("name", value)` (on a sub-query for arguments) | argument dictionary on `AddField(...)` |
| `.Select("a", "b")` | `.AddField("a").AddField("b")` or `new[] { "a", "b" }` |
| `.Include<T>("name")` | no direct equivalent — extract field names from the type yourself and call `AddField` |

## Value types — argument literals and `--var` strings

Two distinct contexts need correct value handling: **argument literals inside the C# snippet** (handed to NGql.Core) and **shell `--var key=value` strings** (handed to `ngql --execute`). Pick the right form for each context.

### In the C# snippet (argument literals)

When generating arguments for `AddField`, `Where`, etc., these CLR types map to GraphQL value forms:

| GraphQL value | CLR literal | Renders as |
|---|---|---|
| `Int` | `int`, `long`, `short`, `sbyte` (and unsigned counterparts) | `42` |
| `Float` | `float`, `double`, `decimal` | `3.14` |
| `String` | `string` | `"alice"` (quotes, backslashes, and control chars are escaped — just write the string naturally) |
| `Boolean` | `bool` | `true` / `false` |
| `null` | `null` | `null` |
| `ID` | `string` | `"abc123"` (GraphQL `ID` is a string on the wire) |
| **enum literal** | `new EnumValue("ADMIN")` | `ADMIN` (unquoted) |
| **variable reference** | `new Variable("$id", "ID!")` | `$id` (auto-promoted to the operation signature) |
| **List of T** | `new[] { ... }`, `new List<T> { ... }`, or any `IList` | `[1, 2, 3]` / `[ADMIN, EDITOR]` |
| **Input object** | `new Dictionary<string, object?> { ["k"] = v, ... }` | `{k:v, ...}` (keys alphabetized) |
| **DateTime / DateTimeOffset** | `new DateTime(...)` | ISO-8601 quoted string. Most GraphQL servers expect a custom scalar here — verify the wire shape matches the server's contract. |

CLR enums (`StringComparison.Ordinal`) also work — they render as their `.ToString()` name. **Prefer `new EnumValue("EXACT_NAME")` over CLR enums** unless the CLR enum's casing already matches the server's expected name; GraphQL convention is `SCREAMING_SNAKE_CASE` while C# is `PascalCase`.

Nested freely: a `Dictionary` inside a `Dictionary`, a list of `Dictionary`s, all combine. Variables can sit anywhere a CLR value can.

### In `--var key=value` (execute mode)

The tool JSON-parses each value; if parsing fails, the value is treated as a bare string. So:

| You want | Pass | Becomes (JSON variable) |
|---|---|---|
| number | `--var first=10` | `10` |
| boolean | `--var active=true` | `true` |
| null | `--var maybe=null` | `null` |
| **string** | `--var name=alice` | `"alice"` (parse fails → bare-string fallback) |
| **string with spaces** | `--var name="Anne Ware"` | `"Anne Ware"` |
| **list** | `--var ids='[1,2,3]'` | `[1, 2, 3]` |
| **list of strings** | `--var tags='["a","b"]'` | `["a", "b"]` |
| **input object** | `--var filter='{"min":10,"tags":["a"]}'` | `{"min": 10, "tags": ["a"]}` |

Single-quote the value when it contains JSON syntax — otherwise the shell will eat the brackets/quotes/braces. The tool does not validate against a schema, so a wrong type silently reaches the server (which will reject it). This matches what curl users already do.

When the user wants to execute a snippet that uses `Variable("$x", "T")` references, they must pass matching `--var x=value` flags (the `$` prefix is for the snippet, not the CLI). Example:

```csharp
// snippet.cs:
QueryBuilder.CreateDefaultBuilder("GetOrder")
    .AddField("order",
        new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID!") },
        new[] { "id", "status" })
```

```bash
ngql snippet.cs --execute --endpoint https://... --var id=42
```

## Output rules

The user's `ToString()` calls produce GraphQL with these conventions — match them when you describe expected output:

- 4-space indentation, opening brace tight to the field name (`users{` not `users {`)
- Fields **sorted alphabetically** within a selection set (insertion order is not preserved)
- Aliases appended after the un-aliased duplicate
- Strings double-quoted; enums (via `EnumValue`) unquoted; `null` literal; numbers raw
- Variables render as `$name:Type` in the operation signature

Example — given:
```csharp
var query = QueryBuilder.CreateDefaultBuilder("GetUsers")
    .AddField("users.name")
    .AddField("users.email");
```

`query.ToString()` produces:
```graphql
query GetUsers{
    users{
        email
        name
    }
}
```

Note `email` before `name` — alphabetical, not insertion order.

## When to ask before writing code

Ask, don't guess, in these cases:

1. **Private/unknown API, no schema given.** Don't invent field names. One sentence: "What field names does your `repositories` query expose?"
2. **Mutations that mutate state** (e.g. `delete`, `transferFunds`). Confirm the operation name and the input shape before generating — the cost of a wrong mutation is higher than a wrong query.
3. **Refactor where "drop" is ambiguous.** "Drop sensitive fields" — confirm which fields count as sensitive, since `Preserve` is inclusive.
4. **The pasted operation has fragments with the same name appearing in multiple places.** Confirm whether the user wants merging, separation, or aliasing before picking a `MergingStrategy`.

## What NOT to do

- Do not generate the classic `Query` or `Mutation` API (`new Query(...)`, `new Mutation(...)`, `.Where(...)`, `.Include<T>(...)`). They are soft-deprecated in 2.1 — `QueryBuilder.CreateDefaultBuilder` and `QueryBuilder.CreateMutationBuilder` cover both surfaces with a richer fluent API. The classic types still render correctly for back-compat but are no longer the recommended path.
- Do not invent a schema. If the user wants `repos.totalStars` and you don't know the real field name (`stargazerCount` on GitHub), ask — don't generate code that won't compile against their server.
- Do not output GraphQL alongside the C# unless asked. The user wants NGql code. They can run `.ToString()` themselves to see the GraphQL.
- Do not add `using` directives for namespaces you don't reference.
- Do not generate `IDictionary<string, object?>` literals — the public overloads take `Dictionary<string, object?>`. Use a concrete dictionary at the call site.
- Do not pass a `metadata` dictionary as a positional argument to `AddField`. Attach metadata via a sub-field lambda using `b.WithMetadata(...)`. This keeps `arguments` as the only positional `Dictionary<string, object?>` slot and removes the only place where two adjacent dicts could be confused.
- Do not use feature flags, `try`/`catch`, or "future-proofing" abstractions in generated code. NGql calls are pure builder construction; let them throw on bad input rather than wrapping them.
- Do not add code comments to the generated builder unless the user asks. The fluent calls are self-describing.
- **You may run `ngql` via Bash when the user explicitly asks** ("send this", "run that", "execute it", "try it against the endpoint"). For any other binary — `which`, `dotnet tool list`, `curl`, `cat`, `gh`, `git`, etc. — **ask first** and get explicit consent before running. Pattern: surface the intent ("want me to check whether `ngql` is installed by running `which ngql`?"), wait for "yes" / "go" / similar, then run. Never silently shell out for diagnostics.
- **Before running `ngql` for the first time in a session, confirm two things in your message: (1) the endpoint isn't going to surprise the user — read the URL aloud (`localhost`, `staging`, `prod`, third-party) and pause for go-ahead if it's anything other than localhost or a clear sandbox; and (2) for mutations, that `--allow-mutations` is intentional.** For pure queries against localhost or services the user clearly owns, you can run without asking. For everything else, single-line confirm.
- **If `ngql` exits non-zero, report the actual exit code and stderr verbatim, then offer one fix.** Common cases: exit 1 = the snippet didn't compile (offer to fix the snippet); exit 2 = the server returned a GraphQL `errors` array (interpret the errors); exit 3 = HTTP failure (surface the status code); exit 4 = mutation blocked (offer the `--allow-mutations` form, with the safety re-check from the previous bullet); exit 127 or "command not found" = `ngql` isn't installed — give the install command (`dotnet tool install -g dotnet-ngql`) and offer to verify the install (with permission) by running `which ngql`.
- **Do not loop or auto-retry on failure.** One run, one report. The user decides whether to retry.
- **If the user reports a problem without asking you to do anything** ("no response," "didn't work," "nothing happened"), first confirm whether they ran the command at all — don't assume execution and pivot to diagnostics. Pattern: "did you run it? if yes, paste the stdout/stderr." Then, if needed, *ask* before running diagnostics.

## Worked examples

### NL → builder (well-known API)

> "Get the top 10 repositories of GitHub user `dolifer`, with name and stargazer count."

```csharp
using NGql.Core;
using NGql.Core.Builders;

var query = QueryBuilder.CreateDefaultBuilder("TopRepos")
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
            new[] { "name", "stargazerCount" }));
```

> Assumed schema (GitHub v4): `user(login: String!) { repositories(first, orderBy) { name, stargazerCount } }`. If your schema differs, swap the field names.

### NL → builder (mutation)

> "Build a `CreateUser` mutation taking `$name` and `$email`, returning `id` and `createdAt`."

```csharp
using NGql.Core;
using NGql.Core.Builders;

var nameVar  = new Variable("$name",  "String!");
var emailVar = new Variable("$email", "String!");

var mutation = QueryBuilder.CreateMutationBuilder("CreateUser")
    .AddField("createUser",
        new Dictionary<string, object?>
        {
            ["name"]  = nameVar,
            ["email"] = emailVar,
        },
        new[] { "createdAt", "id" });
```

### curl → builder

Input:
```bash
curl https://api.example.com/graphql \
  -H 'authorization: Bearer XXX' \
  -d '{"query":"query GetOrder($id: ID!) { order(id: $id) { id status total } }","variables":{"id":"42"}}'
```

Output:
```csharp
using NGql.Core;
using NGql.Core.Builders;

var idVar = new Variable("$id", "ID!");

var query = QueryBuilder.CreateDefaultBuilder("GetOrder")
    .AddField("order",
        new Dictionary<string, object?> { ["id"] = idVar },
        new[] { "id", "status", "total" });

// Submit with variables: {"id": "42"} — NGql renders the operation only,
// you bind variable values at the transport layer.
```

### Refactor / preserve

> "Take `fullProfile` and produce a public view with only name and avatar."

```csharp
using NGql.Core.Builders;

var publicView = PreservationBuilder.Create(fullProfile)
    .Preserve("user.name", "user.avatar")
    .Build();
```

## Verifying the generated code with `ngql`

NGql ships a companion .NET global tool, `dotnet-ngql`, that compiles a snippet against the bundled `NGql.Core` and prints the rendered GraphQL. It's the cleanest way for the user to confirm what the code actually produces.

> **You may run `ngql` for the user when explicitly asked.** "Send this," "run that," "execute," "try it" — go ahead and run `ngql ...`. For first-time runs against non-localhost endpoints, single-line confirm the URL is intended. For mutations, confirm `--allow-mutations` is intended. For probing the environment (`which ngql`, `dotnet tool list`), **ask before running** — surface the intent in your message, wait for go-ahead. Never silently shell out for diagnostics.

**Install (one-time):**

```bash
dotnet tool install -g dotnet-ngql
```

**Update to the latest:**

```bash
dotnet tool update -g dotnet-ngql
```

The tool's version tracks `NGql.Core` in lockstep — if the user is on a specific `NGql.Core` version, install the matching tool version (`--version 2.1.0` etc.).

### Render-only

If the user just wants to see the GraphQL produced by a snippet:

```bash
ngql snippet.cs                                    # from a file
echo '<snippet body>' | ngql                       # from stdin
```

The tool prints the GraphQL to stdout. Exit code 0 on success, 1 on compile/runtime error. Composable with shell redirects:

```bash
ngql snippet.cs > expected.graphql
```

### Execute against a live endpoint

When the user signals they have a real GraphQL endpoint to test against — e.g. mentions "verify against...", "run this against...", "test it on the staging API", or pastes a curl that includes a server URL — suggest the `--execute` flow. **Do not assume an endpoint; if the user hasn't provided one, ask.** The Skill should never invent endpoint URLs.

```bash
ngql snippet.cs --execute \
    --endpoint https://api.example.com/graphql \
    -H "Authorization: Bearer $TOKEN" \
    --var id=42 \
    --var login=octocat
```

- `-H "Name: value"` is repeatable; one per header.
- `--var key=value` is repeatable. Values are JSON-parsed when possible — numbers, booleans, arrays, and objects all work without quoting tricks. Bare strings (`--var login=octocat`) are passed as-is.
- The response JSON is pretty-printed (with syntax highlighting in interactive terminals).
- Exit codes: `0` success, `2` GraphQL `errors` array in response, `3` HTTP failure, `4` mutation blocked, `1` snippet failed to render, `64` invalid usage.

### Mutations require explicit opt-in

`ngql --execute` **refuses to send mutations by default.** When the rendered operation begins with `mutation`, the tool prints a refusal message and exits with code `4` unless the user passes `--allow-mutations`.

If the user asks to execute a mutation and you suggest the command line, also flag the side-effect risk in the same message:

> Heads up — this is a mutation, so it'll actually change data on the server. If you're sure (the endpoint is a sandbox, you have a backup, etc.), pass `--allow-mutations`. Otherwise dry-run by dropping `--execute` to just see the rendered GraphQL first.

Don't pre-add `--allow-mutations` to suggested command lines unless the user has explicitly confirmed the side effect is intended.

### When to offer execution vs stay render-only

- **Offer execution** when the user mentions an endpoint, asks to "test"/"verify"/"run against" something, pastes a curl with a server URL, or asks "does this work" / "did I get this right" about a real API.
- **Stay render-only** for "build me a query" / "port this snippet" / "add a field" — the user may not have an endpoint, and offering to hit one without context is noise.

### If the snippet doesn't compile

When `ngql` reports a compile error, the most common causes are:
- Missing `using NGql.Core.Builders;` (for `QueryBuilder` / `PreservationBuilder` / `FieldBuilder`). The tool auto-imports this for stdin/file snippets, but if the user is integrating the snippet into their own project, they need it.
- Passing `IDictionary<string, object?>` instead of `Dictionary<string, object?>` to `AddField`.
- Using `new EnumValue(MyEnum.Admin)` — that overload exists, but for safety prefer `new EnumValue("ADMIN")` with the literal GraphQL enum name; CLR enum names may not match GraphQL enum names.
