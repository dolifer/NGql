# Migration Guide: NGql 1.5.x to 2.x

This guide helps you migrate from NGql 1.5.x (Classic API) to NGql 2.x (QueryBuilder API) with step-by-step examples and best practices.

---

## Quick Start

The main difference: **NGql 1.x** built queries by nesting `Query` objects, while **NGql 2.x** uses a fluent `QueryBuilder` with dot-notation paths.

| Aspect | 1.5.x (Classic) | 2.x (QueryBuilder) |
|--------|---|---|
| Query creation | `new Query("name")` | `QueryBuilder.CreateDefaultBuilder("name")` |
| Field selection | `.Select("field")` | `.AddField("field")` |
| Nested fields | `.Select(new Query(...))` | `.AddField("parent.child.field")` |
| Field arguments | `.Where("arg", value)` | `.AddField("field", new Dict { {"arg", value} })` |

---

## Migration Examples

### 1. Simple Query

**Before (1.5.x):**
```c#
var query = new Query("GetUsers")
    .Select(new Query("users")
        .Select("name")
        .Select("email"));

Console.WriteLine(query); 
// Output: query GetUsers { users { name email } }
```

**After (2.x):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUsers")
    .AddField("users.name")
    .AddField("users.email");

Console.WriteLine(query);
// Output: query GetUsers { users { name email } }
```

**Key Changes:**
- Replace `new Query()` with `QueryBuilder.CreateDefaultBuilder()`
- Use dot notation (`parent.child`) instead of nested `Select(new Query(...))`
- Each field is added with `AddField()` instead of multiple `Select()` calls

---

### 2. Fields with Arguments

**Before (1.5.x):**
```c#
var query = new Query("GetUser")
    .Select(new Query("user")
        .Where("id", "123")
        .Where("role", "admin")
        .Select("name")
        .Select("email"));
```

**After (2.x):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user", new Dictionary<string, object?>
    {
        { "id", "123" },
        { "role", "admin" }
    })
    .AddField("user.name")
    .AddField("user.email");
```

**Key Changes:**
- All `.Where()` calls become a single `Dictionary` parameter to `AddField()`
- Pass the dictionary as the second parameter to `AddField()`

---

### 3. Deeply Nested Fields

**Before (1.5.x):**
```c#
var query = new Query("ComplexQuery")
    .Select(new Query("user")
        .Select(new Query("profile")
            .Select(new Query("avatar")
                .Select("url")
                .Select("alt"))));
```

**After (2.x):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("ComplexQuery")
    .AddField("user.profile.avatar.url")
    .AddField("user.profile.avatar.alt");
```

**Benefits:**
- Much more readable and concise
- No nested object creation overhead
- Dot notation makes the path structure obvious

---

### 4. Variables

**Before (1.5.x):**
```c#
var userId = new Variable("$userId", "ID!");
var query = new Query("GetUser", variables: userId)
    .Select(new Query("user")
        .Where("id", userId)
        .Select("name"));
```

**After (2.x):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user", new Dictionary<string, object?>
    {
        { "id", new Variable("$userId", "ID!") }
    })
    .AddField("user.name");
```

**Key Changes:**
- Variables are passed as part of the arguments dictionary
- No need to pass variables separately to the builder
- Variables are automatically extracted

---

### 5. Mutations

The `Mutation` class is part of both APIs — there is no `QueryBuilder.CreateMutationBuilder`.
The 2.x improvements are around how you build the inner selection (`Query` with
`Where(...)` arguments and `Select(...)` subfields).

**Before (1.5.x):**
```c#
var mutation = new Mutation("CreateUser")
    .Select(new Query("createUser")
        .Where("name", "John")
        .Where("email", "john@example.com")
        .Select("id", "name", "email"));
```

**After (2.x):** functionally identical for plain string arguments. To use variables,
declare them on the mutation and pass the same instance into `Where(...)`:

```c#
var nameVar  = new Variable("$name", "String!");
var emailVar = new Variable("$email", "String!");

var mutation = new Mutation("CreateUser", nameVar, emailVar)
    .Select(new Query("createUser")
        .Where("name", nameVar)
        .Where("email", emailVar)
        .Select("id", "name", "email"));
```

**Output:**
```graphql
mutation CreateUser($email:String!, $name:String!){
    createUser(email:$email, name:$name){
        email
        id
        name
    }
}
```

---

### 6. Query Composition (New Feature!)

One of the major improvements in 2.x is native query composition with `.Include()`. This is **impossible** with the Classic API.

**Before (1.5.x - Manual Duplication):**
```c#
// Had to manually duplicate field selection
var userFragment = new Query("user")
    .Select("id")
    .Select("name")
    .Select("email");

var postsFragment = new Query("user")
    .Select(new Query("posts")
        .Select("title")
        .Select("publishedAt"));

// No clean way to combine these
var query = new Query("GetUserAndPosts")
    .Select(userFragment)  // But this doesn't properly nest
    .Select(postsFragment); // And this creates duplicate "user" fields
```

**After (2.x - Automatic Merging):**
```c#
var userFragment = QueryBuilder
    .CreateDefaultBuilder("UserFragment")
    .AddField("user.id")
    .AddField("user.name")
    .AddField("user.email");

var postsFragment = QueryBuilder
    .CreateDefaultBuilder("PostsFragment")
    .AddField("user.posts.title")
    .AddField("user.posts.publishedAt");

var combinedQuery = QueryBuilder
    .CreateDefaultBuilder("GetUserAndPosts", MergingStrategy.MergeByFieldPath)
    .Include(userFragment)
    .Include(postsFragment);

// Output: Automatically merged into single "user" field!
// query GetUserAndPosts {
//   user {
//     id
//     name
//     email
//     posts {
//       title
//       publishedAt
//     }
//   }
// }
```

---

## Migration Strategy

### Phase 1: One File at a Time
Start with the least critical code:
```c#
// Old code in ServiceA.cs - Migrate FIRST (low impact)
// Still-used code in ServiceB.cs - Keep as 1.5.x
// Critical code in ServiceC.cs - Migrate LAST (high impact)
```

### Phase 2: Parallel Execution
Both APIs work together in the same application:
```c#
// Old code
var legacyQuery = new Query("GetUsers").Select("name");

// New code
var modernQuery = QueryBuilder
    .CreateDefaultBuilder("GetUsers")
    .AddField("users.name");

// Both produce the same GraphQL, both are valid
```

### Phase 3: Complete Migration
Once all code is migrated, you can remove the old `Query` and `Mutation` classes entirely.

---

## Common Patterns

### Dynamic Query Building

**Before (1.5.x):**
```c#
var query = new Query("DynamicQuery");
if (includeProfile)
{
    query = query.Select(new Query("user")
        .Select(new Query("profile")
            .Select("bio")));
}
if (includePosts)
{
    query = query.Select(new Query("user")
        .Select(new Query("posts")
            .Select("title")));
}
```

**After (2.x):**
```c#
var query = QueryBuilder.CreateDefaultBuilder("DynamicQuery");
if (includeProfile)
{
    query.AddField("user.profile.bio");
}
if (includePosts)
{
    query.AddField("user.posts.title");
}
```

### Type-annotated field paths

2.x accepts an optional type prefix in the field-path string. The type is stored as
metadata on `FieldDefinition.Type` and **does not** appear in the rendered GraphQL —
it is documentation for your own tooling, not a schema-validation feature.

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("TypedUsers")
    .AddField("[] User users")        // metadata: "[] User"
    .AddField("String users.name");   // metadata: "String"

// Output: same shape as untyped — { users { name } }
```

### Query Preservation (new feature)

Extract a subset of an existing query via `PreservationBuilder`. The entry point is
`PreservationBuilder.Create(builder)`; chain `.Preserve(...)` /
`.PreserveFromExpression<T>(...)` and call `.Build()` to materialize a new builder.

```c#
var complexQuery = QueryBuilder
    .CreateDefaultBuilder("FullProfile")
    .AddField("user.id")
    .AddField("user.name")
    .AddField("user.email")
    .AddField("user.profile.bio")
    .AddField("user.profile.avatar");

// String paths
var publicQuery = PreservationBuilder.Create(complexQuery)
    .Preserve("user.name", "user.profile.avatar")
    .Build();

// LINQ expression — preserves every field touched by the predicate
class UserView { public string? name { get; set; } public Profile profile { get; set; } = null!; }
class Profile  { public string? bio { get; set; } }

var derived = PreservationBuilder.Create(complexQuery)
    .PreserveFromExpression<UserView>(x => x.name != null && x.profile.bio != null)
    .Build();
```

This is **new in 2.x** and has no Classic-API equivalent.

---

## Troubleshooting

### Issue: "My nested query isn't merging"

**Problem:**
```c#
var baseQuery = QueryBuilder
    .CreateDefaultBuilder("Q")
    .AddField("user.name");

var more = QueryBuilder
    .CreateDefaultBuilder("M")
    .AddField("user", new Dictionary<string, object?> { ["id"] = 123 })  // different arguments
    .AddField("user.email");

baseQuery.Include(more);
// Renders two distinct "user" fields (the second is auto-aliased "user_1")
// because the argument shapes differ.
```

**Solution:** Use `NeverMerge` strategy if you want guaranteed separation, or ensure arguments are identical for merging:
```c#
var baseQuery = QueryBuilder
    .CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
    .AddField("user", new Dictionary<string, object?> { ["id"] = 123 })
    .AddField("user.name");

var more = QueryBuilder
    .CreateDefaultBuilder("M", MergingStrategy.MergeByFieldPath)
    .AddField("user", new Dictionary<string, object?> { ["id"] = 123 })  // same arguments
    .AddField("user.email");

baseQuery.Include(more);
// Both calls share one "user" field with merged subfields.
```

### Issue: "Variables aren't being detected"

**Problem:**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("Q")
    .AddField("user", new { id = "123" });
// This is just a string, not a Variable!
```

**Solution:** Wrap in `Variable` class:
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("Q")
    .AddField("user", new Dictionary<string, object?>
    {
        { "id", new Variable("$userId", "ID!") }
    });
// Now variables are properly detected
```

---

## Feature Comparison

| Feature | 1.5.x Classic | 2.x QueryBuilder | Notes |
|---------|---|---|---|
| Basic queries | ✅ | ✅ | Both work fine |
| Nested fields | ✅ | ✅✨ | 2.x is more readable |
| Arguments | ✅ | ✅ | 2.x is cleaner |
| Variables | ✅ | ✅ | 2.x auto-detects |
| Field types | ❌ | ✅ | 2.x only |
| Aliases | ❌ | ✅ | 2.x only |
| Query composition | ❌ | ✅ | 2.x only |
| Auto-merging | ❌ | ✅ | 2.x only |
| Field preservation | ❌ | ✅ | 2.x only |
| Array types | ❌ | ✅ | 2.x only |

---

## Getting Help

- **API Documentation**: See [README.md](README.md) for QueryBuilder API details
- **Legacy API Reference**: See [LEGACY.md](LEGACY.md) for Classic API examples
- **Examples**: Check the `tests/` directory for comprehensive examples
- **Issues**: Open an issue on GitHub with your migration question

---

## Timeline

- **NGql 1.5.x**: Original Classic API (legacy, supported)
- **NGql 2.0.0**: QueryBuilder API release (recommended)
- **Future**: Classic API may be moved to separate NuGet package
