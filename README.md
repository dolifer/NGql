![Project Logo](https://raw.githubusercontent.com/dolifer/NGql/main/icon.png)

# NGql - GraphQL Query Builder for .NET

A **zero-dependency**, schema-less GraphQL query builder with a fluent, powerful API. Build type-safe GraphQL queries in C# with automatic query merging, field preservation, and intelligent composition.

[![GitHub license](https://img.shields.io/badge/license-mit-blue.svg)](https://github.com/dolifer/NGql/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/NGql.Core)](https://www.nuget.org/packages/NGql.Core/)
![.NET Version](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-blue)

---

## Why NGql?

- **Schemaless**: No GraphQL schema required—perfect for dynamic queries
- **Zero Dependencies**: Self-contained, no external packages
- **Type-Safe Syntax**: Compile-time checking for field paths and types
- **Automatic Merging**: Intelligently combine query fragments
- **Field Preservation**: Extract specific fields from complex queries
- **Performance Optimized**: Minimal allocations with `Span<T>` and zero-copy patterns
- **Easy to Read**: Fluent API that produces clean, maintainable code

---

## Installation

```bash
dotnet add package NGql.Core
```

**Supported Frameworks:** .NET 8.0, 9.0, 10.0

---

## Quick Start

### 1. Build a Simple Query

```csharp
using NGql.Core.Builders;

var query = QueryBuilder
    .CreateDefaultBuilder("GetUsers")
    .AddField("users.name")
    .AddField("users.email");

Console.WriteLine(query);
```

**Output:**
```graphql
query GetUsers {
  users {
    name
    email
  }
}
```

### 2. Add Field Arguments

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("SearchUsers")
    .AddField("users", new Dictionary<string, object?>
    {
        { "first", 10 },
        { "search", "john" }
    })
    .AddField("users.name")
    .AddField("users.email");
```

**Output:**
```graphql
query SearchUsers {
  users(first: 10, search: "john") {
    name
    email
  }
}
```

### 3. Use Variables

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user", new Dictionary<string, object?>
    {
        { "id", new Variable("$userId", "ID!") }
    })
    .AddField("user.name")
    .AddField("user.email");
```

**Output:**
```graphql
query GetUser($userId: ID!) {
  user(id: $userId) {
    name
    email
  }
}
```

**Note on Variables:**
- Variable names start with `$` (e.g., `$userId`)
- Types follow GraphQL syntax: `ID!` means required ID, `String` means optional string
- Variables are declared in the query signature and passed at runtime

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
query DeepQuery {
  organization {
    departments {
      teams {
        members {
          name
        }
      }
    }
  }
}
```

### 2. Field Type Annotations

Specify field types for better schema compliance:

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("TypedFields")
    .AddField("String user.name")           // Scalar type
    .AddField("Int user.age")               // Integer type
    .AddField("User user.profile")          // Object type
    .AddField("[] tags")                    // Array marker
    .AddField("Post[] user.posts");         // Typed array

Console.WriteLine(query);
```

**Output:**
```graphql
query TypedFields {
  user {
    name
    age
    profile
    posts
  }
  tags
}
```

**About Type Annotations:**
- Type annotations (`String`, `Int`, `Post[]`) are preserved in metadata but don't appear in the GraphQL output
- They help document field types for maintainability and clarity
- Optional: Use types as comments/documentation when working with typed APIs
- Since NGql is schemaless, type annotations are metadata-only—no runtime validation
- The field names and structure are what matters for GraphQL execution

### 3. Field Aliases

Give fields alternative names in the response. Aliases can be at any part of the dot notation:

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("AliasedQuery")
    .AddField("primaryName:user.name")              // Alias at the end
    .AddField("userAlias:user.email")               // Alias at the end
    .AddField("recent:user.posts.title")            // Alias at the end
    .AddField("author:user.profile.name")           // Alias at the end
    .AddField("userData:user.id")                   // Alias at the end
    .AddField("allPosts:user.posts");               // Alias for entire nested path
```

**Output:**
```graphql
query AliasedQuery {
  primaryName: user {
    name
  }
  userAlias: user {
    email
  }
  recent: user {
    posts {
      title
    }
  }
  author: user {
    profile {
      name
    }
  }
  userData: user {
    id
  }
  allPosts: user {
    posts
  }
}
```

**Aliases at Any Part of the Dot Notation:**

You can also place aliases at any intermediate level in the path:

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("IntermediateAliases")
    .AddField("mainUser:user.profile.settings.privacy")  // Alias on first level
    .AddField("user.prof:profile.info.email")            // Alias in the middle
    .AddField("user.profile.sec:settings.security");     // Alias near the end
```

**Output:**
```graphql
query IntermediateAliases {
  mainUser: user {
    profile {
      settings {
        privacy
      }
    }
  }
  user {
    prof: profile {
      info {
        email
      }
    }
  }
  user {
    profile {
      sec: settings {
        security
      }
    }
  }
}
```

**Use Cases:**
- **Name conflicts**: Avoid field name collisions when querying the same field with different arguments
- **Clarity**: Use readable aliases in responses (e.g., `currentUser` instead of `user`)
- **Nested navigation**: Alias intermediate levels for cleaner response structures

### 4. Nested Arguments

Build complex argument structures for filtering, sorting, and pagination:

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("ComplexArgs")
    .AddField("searchUsers", new Dictionary<string, object?>
    {
        { "filter", new Dictionary<string, object?>
        {
            { "name", "john" },
            { "age", new Dictionary<string, object?> 
            {
                { "gte", 18 },
                { "lte", 65 }
            }}
        }},
        { "pagination", new 
        {
            first = 20,
            after = new Variable("$cursor", "String")
        }}
    })
    .AddField("searchUsers.edges.node.name");
```

**Output:**
```graphql
query ComplexArgs($cursor: String) {
  searchUsers(
    filter: { name: "john", age: { gte: 18, lte: 65 } },
    pagination: { first: 20, after: $cursor }
  ) {
    edges {
      node {
        name
      }
    }
  }
}
```

**When to use this:**
- **Filtering**: Pass filter objects to narrow results (e.g., `filter: { status: "active" }`)
- **Sorting**: Specify sort order (e.g., `orderBy: { field: "created", direction: "DESC" }`)
- **Pagination**: Pass pagination args (e.g., `first: 20, after: cursor`)
- **Nested arguments**: Arguments can contain objects and arrays just like GraphQL

---

## Query Composition & Merging 🚀

The most powerful feature: combine query fragments with automatic, intelligent merging.

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

Console.WriteLine(combined);
```

**Output:**
```graphql
query UserProfile {
  user {
    id
    name
    email
    profile {
      bio
      avatar
    }
  }
}
```

All three queries merged into a single efficient query!

### Merging Strategies

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| **`MergeByDefault`** | Inherits from parent | Most flexible (default) |
| **`MergeByFieldPath`** | Merges compatible paths | Optimize similar queries |
| **`NeverMerge`** | Always separate | Enforce separation |

#### MergeByFieldPath (Optimizing)

**Use this when:** You want to combine query fragments with compatible paths to minimize output size and network traffic.

```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("OptimizedQuery", MergingStrategy.MergeByFieldPath)
    .AddField("users", ["id", "name"]);

// Same path, no args → MERGES
query.Include(QueryBuilder.CreateDefaultBuilder("F1")
    .AddField("users", ["email"]));

// Different path → MERGES as nested
query.Include(QueryBuilder.CreateDefaultBuilder("F2")
    .AddField("users.profile", ["bio"]));

// Same path, different args → DOESN'T MERGE (different filters)
query.Include(QueryBuilder.CreateDefaultBuilder("F3")
    .AddField("users", new { status = "active" }, ["role"]));
```

**Output:**
```graphql
query OptimizedQuery {
  users {
    id
    name
    email
    profile {
      bio
    }
  }
  users_1(status: "active") {
    role
  }
}
```

**Result:** Three fragments combined into one optimized query (except the one with different arguments).

#### NeverMerge (Enforce Separation)

**Use this when:** You need separate field instances (e.g., for debugging, different caching strategies, or GraphQL specifications that forbid merging).

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
query MainQuery {
  users {
    name
  }
  users_1 {
    email
  }
}
```

**Result:** Despite compatible paths, the second fragment is always separate due to `NeverMerge`.

### Advanced: Dynamic Query Building

```csharp
public QueryBuilder BuildUserQuery(UserQueryOptions options)
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

```csharp
var fullQuery = QueryBuilder
    .CreateDefaultBuilder("FullProfile")
    .AddField("user.id")
    .AddField("user.name")
    .AddField("user.email")
    .AddField("user.ssn")           // Sensitive
    .AddField("user.salary")        // Sensitive
    .AddField("user.profile.bio");

// Extract public fields only
var publicQuery = fullQuery.Preserve("user.name", "user.profile.bio");
```

**Input:**
```graphql
query FullProfile {
  user {
    id
    name
    email
    ssn
    salary
    profile {
      bio
    }
  }
}
```

**Output:**
```graphql
query FullProfile {
  user {
    name
    profile {
      bio
    }
  }
}
```

### Real-World: Role-Based Field Filtering

```csharp
public class QueryFilter
{
    public static QueryBuilder FilterByRole(QueryBuilder query, UserRole role)
    {
        return role switch
        {
            UserRole.Public => query.Preserve(
                "user.name",
                "user.profile.bio",
                "user.profile.avatar"
            ),
            UserRole.Admin => query.Preserve(
                "user.id",
                "user.name",
                "user.email",
                "user.profile.bio",
                "user.createdAt",
                "user.lastLogin"
            ),
            UserRole.Self => query,  // Full access
            _ => query.Preserve()    // Empty - no fields
        };
    }
}

// Usage
var adminQuery = QueryFilter.FilterByRole(fullQuery, UserRole.Admin);
var publicQuery = QueryFilter.FilterByRole(fullQuery, UserRole.Public);
```

---

## Mutations

Create mutations using the `Mutation` class:

```csharp
var mutation = new Mutation("CreateUser",
    new Variable("$name", "String!"),
    new Variable("$email", "String!")
)
.Select("createUser(name: $name, email: $email)")
.Select("createUser.id")
.Select("createUser.name")
.Select("createUser.email");

Console.WriteLine(mutation);
```

**Output:**
```graphql
mutation CreateUser($name: String!, $email: String!) {
  createUser(name: $name, email: $email) {
    id
    name
    email
  }
}
```

**Mutation API:**
- `new Mutation(name, params variables)` - Create a mutation with a name and optional variables
- `.Variable(name, type)` - Add a variable to the mutation
- `.Select(selects)` - Add fields to the mutation (supports dot notation like `"field.subfield"` and GraphQL syntax like `"field(arg: value)"`)
- `.Variable(variable)` - Add a pre-created Variable object

---

## Best Practices

### 1. **Use Dot Notation for Readability**

```csharp
// ✅ Good: Clear hierarchy
query.AddField("user.profile.avatar.url");

// ❌ Avoid: Multiple arguments
query.AddField("user", ["profile"]);
```

### 2. **Create Reusable Fragments**

```csharp
public static class QueryFragments
{
    public static QueryBuilder UserBaseFields()
        => QueryBuilder.CreateDefaultBuilder("UserBase")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.email");

    public static QueryBuilder UserProfileFields()
        => QueryBuilder.CreateDefaultBuilder("UserProfile")
            .AddField("user.profile.bio")
            .AddField("user.profile.avatar");
}

// Reuse
var combined = QueryBuilder
    .CreateDefaultBuilder("FullUser", MergingStrategy.MergeByFieldPath)
    .Include(QueryFragments.UserBaseFields())
    .Include(QueryFragments.UserProfileFields());
```

### 3. **Choose the Right Merging Strategy**

```csharp
// For optimization (most cases)
QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)

// For guaranteed separation
QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.NeverMerge)

// For flexibility (inherits from parent)
QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByDefault)
```

### 4. **Use Type Annotations for Documentation**

```csharp
// ✅ Good: Types document the expected structure
query.AddField("User user")
     .AddField("String user.name")
     .AddField("Post[] user.posts");

// ❌ Avoid: No type information
query.AddField("user")
     .AddField("user.name")
     .AddField("user.posts");
```

### 5. **Type Variables for Reusability**

```csharp
// ✅ Good: Variables are dynamic
.AddField("user", new { id = new Variable("$userId", "ID!") })

// ❌ Avoid: Hardcoded values
.AddField("user", new { id = "123" })
```

---

## Performance Notes

NGql is optimized for performance:

- **Zero-Copy Spans**: Uses `Span<T>` and `ReadOnlySpan<T>` to avoid allocations
- **Smart Field Caching**: Lazy initialization of field structures
- **Efficient Merging**: Linear-time query merging algorithm
- **No Reflection**: All operations are compile-time safe
- **Minimal GC Pressure**: Designed for hot-path query building

---

## Migration from NGql 1.5.x

If you're upgrading from version 1.5.x (Classic API), see the **[Migration Guide](docs/reference/MIGRATION.md)** for step-by-step examples.

**Key Differences:**

| Feature | 1.5.x | 2.x |
|---------|-------|-----|
| Query creation | `new Query()` | `QueryBuilder.CreateDefaultBuilder()` |
| Nested fields | `.Select(new Query())` | `.AddField("parent.child")` |
| Arguments | `.Where("key", value)` | `.AddField("field", new Dict { ... })` |
| Merging | Manual | Automatic |
| Field types | ❌ | ✅ |
| Preservation | ❌ | ✅ |

The Classic API is still fully functional but deprecated. [Learn more](docs/reference/LEGACY.md).

---

## Examples & Documentation

- **[Migration Guide](docs/reference/MIGRATION.md)** - Detailed upgrade from 1.5.x to 2.x
- **[Legacy API Reference](docs/reference/LEGACY.md)** - Classic API documentation for existing code
- **[Test Suite](tests/)** - Comprehensive examples in test files
- **[API Docs](src/Core/)** - Source code with inline documentation

---

## Contributing

Contributions welcome! Please:

1. Read the existing code style
2. Add tests for new features
3. Ensure all 343+ tests pass
4. Open a descriptive pull request

---

## License

MIT License - See [LICENSE](LICENSE) file for details.

---

## Roadmap & Future Work

- [ ] GraphQL subscription support
- [ ] Schema introspection integration
- [ ] Advanced caching strategies
- [ ] Performance profiling dashboards

---

**Made with ❤️ for .NET developers building GraphQL clients.**
