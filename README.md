![Project Logo](https://raw.githubusercontent.com/dolifer/NGql/main/icon.png)

# NGql - GraphQL Query Builder for .NET

A **zero-dependency**, schema-less GraphQL query builder for .NET. Compose GraphQL operations from C# with a fluent API, merge fragments at runtime, and extract subsets via field-path or LINQ expression — without an SDL or codegen step.

[![NuGet](https://img.shields.io/nuget/v/NGql.Core)](https://www.nuget.org/packages/NGql.Core/)
[![Downloads](https://img.shields.io/nuget/dt/NGql.Core)](https://www.nuget.org/packages/NGql.Core/)
[![Build](https://img.shields.io/github/actions/workflow/status/dolifer/NGql/build-and-test.yml?branch=main)](https://github.com/dolifer/NGql/actions/workflows/build-and-test.yml)
[![Line coverage](https://dolifer.github.io/NGql/coverage/badge_linecoverage.svg)](https://dolifer.github.io/NGql/coverage/)
[![Branch coverage](https://dolifer.github.io/NGql/coverage/badge_branchcoverage.svg)](https://dolifer.github.io/NGql/coverage/)
![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blue)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/dolifer/NGql/blob/main/LICENSE)

---

## Why NGql?

- **Schemaless** — runs without an SDL, code-gen step, or build-time schema validation
- **Zero runtime dependencies** — single assembly, multi-targets `net8.0` / `net9.0` / `net10.0`
- **Fluent composition** — `QueryBuilder` / `FieldBuilder` keeps query construction inline with C# control flow
- **Runtime fragment merging** — `Include(otherBuilder)` joins disjoint fragments; `MergingStrategy` picks how to handle duplicates (default merge, never merge, or merge-by-field-path with auto-aliasing on conflict)
- **Field preservation** — keep a subset of an existing query by string path (`PreservationBuilder.Preserve`) or by C# expression (`PreserveFromExpression<T>(x => x.user.profile.email != null)`)
- **Variables, enums, nested arguments** — `Variable`/`EnumValue` types render to native GraphQL syntax; nested `Dictionary<string, object?>` arguments produce nested input objects
- **Hot-path optimized** — span-based path parsing, lock-free reads on `FieldChildren`, in-place merge in `Include()`. Reflection is used only by the LINQ-expression preservation path; query rendering itself is reflection-free

---

## Installation

```bash
dotnet add package NGql.Core
```

**Supported Frameworks:** .NET 8.0, 9.0, 10.0

---

## Companion Tools

NGql ships two optional companions alongside the library. Use them when they help; ignore them otherwise — the library is fully usable on its own.

### `dotnet-ngql` — command-line renderer

A .NET global tool that compiles a `QueryBuilder` snippet against `NGql.Core` and prints the GraphQL it renders to. Useful for sanity-checking a snippet, snapshotting expected query text in CI scripts, or executing a rendered operation against a live endpoint.

```bash
dotnet tool install -g dotnet-ngql --prerelease   # one-time install (preview-only on NuGet today)
dotnet tool update  -g dotnet-ngql --prerelease   # update to latest preview

ngql snippet.cs                               # render a file
echo '<snippet>' | ngql                       # or read from stdin

ngql snippet.cs --execute \
    --endpoint https://api.example.com/graphql \
    -H "Authorization: Bearer $TOKEN" \
    --var id=42                               # render and POST to a real endpoint
```

The tool's version tracks `NGql.Core` in lockstep. Mutations are refused by default; pass `--allow-mutations` to opt in. Full docs: <https://www.nuget.org/packages/dotnet-ngql>.

### `ngql` Claude Code skill

A [Claude Code](https://docs.claude.com/en/docs/claude-code) skill that teaches Claude to author NGql code from natural language ("build a query that fetches a user's last 5 orders") or from a pasted GraphQL operation / curl. Pairs with the `dotnet-ngql` tool to verify generated snippets against a live endpoint.

The skill lives at [`.claude/skills/ngql/`](https://github.com/dolifer/NGql/tree/main/.claude/skills/ngql) — running Claude Code inside a clone of this repo picks it up automatically. To use it from any project, copy the folder to `~/.claude/skills/ngql/`. See [`.claude/skills/ngql/README.md`](.claude/skills/ngql/README.md) for install and usage.

---

## Quick Start

> All sample output blocks below are pasted verbatim from `QueryBuilder.ToString()`.
> Field order follows insertion-independent canonical sorting (alphabetical, with aliased
> duplicates appended after the un-aliased one).

### 1. Build a Simple Query

```csharp
using NGql.Core;
using NGql.Core.Builders;

var query = QueryBuilder
    .CreateDefaultBuilder("GetUsers")
    .AddField("users.name")
    .AddField("users.email");

Console.WriteLine(query);
```

**Output:**
```graphql
query GetUsers{
    users{
        email
        name
    }
}
```

### 2. Add Field Arguments

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("SearchUsers")
    .AddField("users", new Dictionary<string, object?>
    {
        ["first"] = 10,
        ["search"] = "john"
    },
    subFields: new[] { "name", "email" });
```

**Output:**
```graphql
query SearchUsers{
    users(first:10, search:"john"){
        email
        name
    }
}
```

### 3. Use Variables

`Variable` lives in `NGql.Core` (not `NGql.Core.Builders`) — make sure both `using`
directives are in scope. Pass a `Variable` instance as an argument value and it is
auto-promoted to the operation signature.

```csharp
using NGql.Core;
using NGql.Core.Builders;

var userId = new Variable("$userId", "ID!");

var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user", new Dictionary<string, object?>
    {
        ["id"] = userId
    },
    subFields: new[] { "name", "email" });
```

**Output:**
```graphql
query GetUser($userId:ID!){
    user(id:$userId){
        email
        name
    }
}
```

**Note on Variables:**
- Variable names must start with `$` (the `Variable` constructor throws otherwise)
- The type string is opaque to NGql and emitted verbatim — your GraphQL server validates it
- Variables appear in the operation signature; passing values at execution time is the
  HTTP/transport layer's responsibility (NGql renders the operation text, it does not execute it)

---

## Core Features

### 1. Dot Notation for Nested Fields

The simplest way to express field hierarchies:

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("DeepQuery")
    .AddField("organization.departments.teams.members.name");
```

**Output:**
```graphql
query DeepQuery{
    organization{
        departments{
            teams{
                members{
                    name
                }
            }
        }
    }
}
```

### 2. Field Type Annotations

Specify field types as documentation metadata:

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("TypedFields")
    .AddField("String user.name")           // Scalar type
    .AddField("Int user.age")               // Integer type
    .AddField("User user.profile")          // Object type
    .AddField("[] tags")                    // Array marker
    .AddField("Post[] user.posts");         // Typed array
```

**Output:**
```graphql
query TypedFields{
    tags
    user{
        age
        name
        posts
        profile
    }
}
```

**About Type Annotations:**
- Type annotations (`String`, `Int`, `Post[]`) are stored in metadata; they do **not** appear in the rendered GraphQL output
- Use them as inline documentation when round-tripping through serializers, or to drive your own tooling that reads `FieldDefinition.Type`
- NGql does **not** validate types against a schema — they are metadata only

### 3. Field Aliases

Use `alias:name` syntax inside any segment of a dotted path to alias the corresponding
node. Subsequent additions that share the same path **merge into the same node**, so
adding more subfields under an aliased root accumulates them under that single alias.

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("AliasedQuery")
    .AddField("primaryName:user.name")
    .AddField("primaryName:user.email")
    .AddField("primaryName:user.posts.title");
```

**Output:**
```graphql
query AliasedQuery{
    primaryName:user{
        email
        name
        posts{
            title
        }
    }
}
```

**Aliasing the same field with conflicting arguments** triggers `MergeByFieldPath` to
auto-suffix duplicates as `name_1`, `name_2`, … (see *Merging Strategies* below).

**Use cases:**
- Name conflicts when the same field appears with different arguments
- Renaming a field for the response without changing the schema-side name

### 4. Nested Arguments

Build complex argument structures for filtering, sorting, and pagination. Nested
`Dictionary<string, object?>` values render as nested GraphQL input objects; anonymous
types and POCOs are also decomposed via reflection (their property names become input
keys), but using `Dictionary` keeps things explicit and avoids surprises.

```csharp
var cursor = new Variable("$cursor", "String");
var query = QueryBuilder
    .CreateDefaultBuilder("ComplexArgs")
    .AddField("searchUsers", new Dictionary<string, object?>
    {
        ["filter"] = new Dictionary<string, object?>
        {
            ["name"] = "john",
            ["age"] = new Dictionary<string, object?>
            {
                ["gte"] = 18,
                ["lte"] = 65
            }
        },
        ["pagination"] = new Dictionary<string, object?>
        {
            ["first"] = 20,
            ["after"] = cursor
        }
    })
    .AddField("searchUsers.edges.node.name");
```

**Output:**
```graphql
query ComplexArgs($cursor:String){
    searchUsers(filter:{age:{gte:18, lte:65}, name:"john"}, pagination:{after:$cursor, first:20}){
        edges{
            node{
                name
            }
        }
    }
}
```

Notes:
- Argument keys are sorted alphabetically for stable output (helps cache keys / snapshot tests)
- Variables nested inside argument objects are still hoisted to the operation signature
- Lists / arrays render as `[a, b, c]` GraphQL input lists

**When to use this:**
- **Filtering**: Pass filter objects to narrow results
- **Sorting**: Specify sort order (e.g., `orderBy: { field: "created", direction: "DESC" }`)
- **Pagination**: Pass pagination args (e.g., `first: 20, after: cursor`)

---

## Query Composition & Merging

Combine query fragments with `Include()`. Merge behavior is controlled by the
`MergingStrategy` set on the **target** builder (the one calling `Include`).

### Basic Composition

```csharp
var userFields = QueryBuilder
    .CreateDefaultBuilder("UserFields")
    .AddField("user.id")
    .AddField("user.name")
    .AddField("user.email");

var profileFields = QueryBuilder
    .CreateDefaultBuilder("ProfileFields")
    .AddField("user.profile.bio")
    .AddField("user.profile.avatar");

var combined = QueryBuilder
    .CreateDefaultBuilder("UserProfile", MergingStrategy.MergeByFieldPath)
    .Include(userFields)
    .Include(profileFields);
```

**Output:**
```graphql
query UserProfile{
    user{
        email
        id
        name
        profile{
            avatar
            bio
        }
    }
}
```

### Merging Strategies

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| `MergeByDefault` | Default — append fragments without alias collision detection | Compose disjoint fragments |
| `MergeByFieldPath` | Merge compatible same-path fields; auto-alias on argument conflict | Optimize overlapping queries |
| `NeverMerge` | Each fragment becomes its own auto-aliased copy | Force separation |

#### MergeByFieldPath (Optimizing)

Compatible fragments collapse; argument conflicts auto-alias as `name_1`, `name_2`, …

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("OptimizedQuery", MergingStrategy.MergeByFieldPath)
    .AddField("users", subFields: new[] { "id", "name" });

// Same path, no args → MERGES
query.Include(QueryBuilder.CreateDefaultBuilder("F1", MergingStrategy.MergeByFieldPath)
    .AddField("users", subFields: new[] { "email" }));

// Nested path → MERGES into the existing "users"
query.Include(QueryBuilder.CreateDefaultBuilder("F2", MergingStrategy.MergeByFieldPath)
    .AddField("users.profile", subFields: new[] { "bio" }));

// Same field, conflicting args → AUTO-ALIASED
query.Include(QueryBuilder.CreateDefaultBuilder("F3", MergingStrategy.MergeByFieldPath)
    .AddField("users", new Dictionary<string, object?> { ["status"] = "active" }, subFields: new[] { "role" }));
```

**Output:**
```graphql
query OptimizedQuery{
    users{
        email
        id
        name
        profile{
            bio
        }
    }
    users_1:users(status:"active"){
        role
    }
}
```

#### NeverMerge (Enforce Separation)

```csharp
var mainQuery = QueryBuilder
    .CreateDefaultBuilder("MainQuery", MergingStrategy.MergeByFieldPath)
    .AddField("users.name");

var separate = QueryBuilder
    .CreateDefaultBuilder("Separate", MergingStrategy.NeverMerge)
    .AddField("users.email");

mainQuery.Include(separate);
```

**Output:**
```graphql
query MainQuery{
    users{
        name
    }
    users_1:users{
        email
    }
}
```

The included `Separate` builder declares `NeverMerge`, so its fields are aliased rather
than merged into `mainQuery`'s `users`.

### Dynamic Query Building

```csharp
record UserQueryOptions(bool IncludeEmail, bool IncludeProfile, bool IncludePosts);

QueryBuilder BuildUserQuery(UserQueryOptions options)
{
    var query = QueryBuilder
        .CreateDefaultBuilder("DynamicUser", MergingStrategy.MergeByFieldPath)
        .AddField("user.id")
        .AddField("user.name");

    if (options.IncludeEmail)
        query.AddField("user.email");

    if (options.IncludeProfile)
    {
        query.AddField("user.profile.bio");
        query.AddField("user.profile.avatar");
    }

    if (options.IncludePosts)
        query.AddField("user.posts.title")
             .AddField("user.posts.publishedAt");

    return query;
}
```

---

## Query Preservation 🎯

Extract specific fields from complex queries—perfect for filtering data by user role or permission.

### Basic Preservation

The public entry point is `PreservationBuilder.Create(query)`. Add field paths via
`Preserve(...)` (string paths) or `PreserveFromExpression<T>(...)` (LINQ predicate),
then `.Build()` returns a new `QueryBuilder` containing only the preserved subtree.

```csharp
var fullQuery = QueryBuilder
    .CreateDefaultBuilder("FullProfile")
    .AddField("user.id")
    .AddField("user.name")
    .AddField("user.email")
    .AddField("user.ssn")           // sensitive
    .AddField("user.salary")        // sensitive
    .AddField("user.profile.bio");

var publicQuery = PreservationBuilder.Create(fullQuery)
    .Preserve("user.name", "user.profile.bio")
    .Build();
```

**Output:**
```graphql
query FullProfile{
    user{
        name
        profile{
            bio
        }
    }
}
```

### Role-Based Field Filtering

```csharp
enum UserRole { Public, Admin, Self }

static QueryBuilder FilterByRole(QueryBuilder source, UserRole role) => role switch
{
    UserRole.Public => PreservationBuilder.Create(source)
        .Preserve("user.name", "user.profile.bio", "user.profile.avatar")
        .Build(),
    UserRole.Admin => PreservationBuilder.Create(source)
        .Preserve("user.id", "user.name", "user.email",
                  "user.profile.bio", "user.createdAt", "user.lastLogin")
        .Build(),
    UserRole.Self => source,                                  // full access
    _ => PreservationBuilder.Create(source).Build(),          // empty subset
};

var adminQuery  = FilterByRole(fullQuery, UserRole.Admin);
var publicQuery = FilterByRole(fullQuery, UserRole.Public);
```

### Expression-based Preservation

If you have a typed model that mirrors the query shape, you can preserve fields via
a C# expression — useful when the predicate already lives in a permission rule or
validation method:

```csharp
class UserView { public Profile profile { get; set; } = null!; public string? email { get; set; } }
class Profile  { public string? bio { get; set; } public string? name { get; set; } }

var preserved = PreservationBuilder.Create(fullQuery)
    .PreserveFromExpression<UserView>(x => x.profile.bio != null && x.email != null)
    .Build();
```

`PreserveFromExpression<T>` walks the expression tree, extracts every member access
chain, and preserves the corresponding field paths. Comparisons, logical operators,
ternaries, null-coalescing, and LINQ method calls (`Any`, `Where`, `First`) are all
supported. This path uses reflection on `T` once per call.

---

## Mutations

`Mutation` follows the Classic API shape: pass variables to the constructor, then
`.Select(...)` either field names directly or a nested `Query` (which carries its own
arguments via `.Where(...)`).

```csharp
var nameVar  = new Variable("$name", "String!");
var emailVar = new Variable("$email", "String!");

var createUser = new Query("createUser")
    .Where("name", nameVar)
    .Where("email", emailVar)
    .Select("id", "createdAt");

var mutation = new Mutation("CreateUser", nameVar, emailVar)
    .Select(createUser);

Console.WriteLine(mutation);
```

**Output:**
```graphql
mutation CreateUser($email:String!, $name:String!){
    createUser(email:$email, name:$name){
        createdAt
        id
    }
}
```

**Mutation API:**
- `new Mutation(name, params Variable[])` — declare the operation and its variables
- `.Variable(name, type)` / `.Variable(Variable)` — add more variables incrementally
- `.Select(params string[])` — add plain field names
- `.Select(Query subQuery)` — embed a `Query` (with its `Where`/`Select` arguments and subfields)
- `.Select(IEnumerable<object>)` — mixed list of strings and `QueryBlock`s

---

## Best Practices

### Prefer dot notation for hierarchical paths

`AddField("user.profile.avatar.url")` produces the same field tree as four chained
`AddField` calls but reads as one line. Both forms compose with arguments and the same
field path — pick whichever is clearer in context.

```csharp
// Equivalent shapes:
query.AddField("user.profile.avatar.url");
query.AddField("user", fb => fb
    .AddField("profile", fb2 => fb2
        .AddField("avatar", fb3 => fb3
            .AddField("url"))));
```

### Build small reusable fragments

```csharp
static class QueryFragments
{
    public static QueryBuilder UserBaseFields() =>
        QueryBuilder.CreateDefaultBuilder("UserBase")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.email");

    public static QueryBuilder UserProfileFields() =>
        QueryBuilder.CreateDefaultBuilder("UserProfile")
            .AddField("user.profile.bio")
            .AddField("user.profile.avatar");
}

var combined = QueryBuilder
    .CreateDefaultBuilder("FullUser", MergingStrategy.MergeByFieldPath)
    .Include(QueryFragments.UserBaseFields())
    .Include(QueryFragments.UserProfileFields());
```

### Pick the right merging strategy

```csharp
// Most common — duplicates auto-alias on argument conflict
QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath);

// Force every fragment into its own aliased field
QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.NeverMerge);

// Default — append fragments without alias-conflict detection
QueryBuilder.CreateDefaultBuilder("Q");
// or explicitly:
QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByDefault);
```

### Reuse `Variable` instances across calls

A single `Variable` instance can be passed as an argument value in many places — NGql
detects it via reference and adds it to the operation signature exactly once.

```csharp
var userId = new Variable("$userId", "ID!");

var query = QueryBuilder.CreateDefaultBuilder("DualLookup")
    .AddField("user", new Dictionary<string, object?> { ["id"] = userId },
              subFields: new[] { "name" })
    .AddField("posts", new Dictionary<string, object?> { ["authorId"] = userId },
              subFields: new[] { "title" });
```

---

## Performance Notes

Hot-path design choices, in rough order of impact:

- **Span-based field-path parsing** — dotted paths are walked over `ReadOnlySpan<char>`, not `string.Split`, with stack-allocated buffers up to 256 chars and a pooled fallback above that
- **Lock-free reads on `FieldChildren`** — small collections use a volatile array snapshot; the lookup index activates only past 16 children
- **In-place merge in `Include()`** — `MergeFieldsInPlace` mutates the existing field tree instead of cloning, so merging N fragments into a parent is O(N) work, not O(N²)
- **Path-and-field-lookup caches** — `_pathIndex` caches `GetPathTo(rootName, nodePath)` results; both invalidate on field mutation
- **Reflection is opt-in** — `QueryBuilder`/`FieldBuilder` rendering is reflection-free. Reflection runs only when you call `PreserveFromExpression<T>(...)` or `Include<T>(...)`, where it walks the expression tree / type's properties
- **`BenchmarkRunner`** project ships with the repo if you want to measure your own scenarios

---

## Migration from NGql 1.5.x

If you're upgrading from version 1.5.x (Classic API), see the **[Migration Guide](https://github.com/dolifer/NGql/blob/main/docs/reference/MIGRATION.md)** for step-by-step examples.

**Key Differences:**

| Feature | 1.5.x (Classic) | 2.x (QueryBuilder) |
|---------|-----------------|--------------------|
| Query creation | `new Query("name")` | `QueryBuilder.CreateDefaultBuilder("name")` |
| Nested fields | `.Select(new Query("child"))` | `.AddField("parent.child")` |
| Field arguments | `.Where("key", value)` | `.AddField("field", new Dictionary<string, object?> { … })` |
| Composing fragments | manual stitching | `Include(otherBuilder)` with `MergingStrategy` |
| Field-path subset | not available | `PreservationBuilder.Create(...).Preserve(...).Build()` |
| Type-annotation metadata | not available | `AddField("String user.name")` (metadata only — does not appear in rendered GraphQL) |

The Classic API (`Query`, `Mutation`) is still fully supported in 2.x and renders independently —
it is **not** the internal representation `QueryBuilder` uses; both APIs produce GraphQL
text through separate code paths. Use whichever fits your use case (or mix them: a
`Mutation` can `Select` a hand-built `Query`, while `QueryBuilder` is the typical entry
point for composable, dynamic queries). See
[LEGACY.md](https://github.com/dolifer/NGql/blob/main/docs/reference/LEGACY.md) for
Classic-API examples.

---

## Examples & Documentation

- **[Migration Guide](https://github.com/dolifer/NGql/blob/main/docs/reference/MIGRATION.md)** — detailed upgrade path from 1.5.x to 2.x
- **[Legacy API Reference](https://github.com/dolifer/NGql/blob/main/docs/reference/LEGACY.md)** — `Query` / `Mutation` Classic-API documentation
- **[Test Suite](https://github.com/dolifer/NGql/tree/main/tests)** — runnable usage examples for every feature
- **[Contributor Guide](https://github.com/dolifer/NGql/blob/main/CLAUDE.md)** — architecture, build commands, conventions

---

## Contributing

Contributions welcome! Please:

1. Read the existing code style
2. Add tests for new features (see [CLAUDE.md](./CLAUDE.md) for the test-consolidation conventions)
3. Run `make coverage` and ensure no production-line coverage regression (Windows: run from Git Bash or WSL — see [CLAUDE.md](./CLAUDE.md))
4. Open a descriptive pull request

---

## License

MIT — see [LICENSE](https://github.com/dolifer/NGql/blob/main/LICENSE).
