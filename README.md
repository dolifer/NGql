![Project Logo](https://raw.githubusercontent.com/dolifer/NGql/main/icon.png) 
# NGql

Schemaless GraphQL query builder for .NET with fluent syntax. Zero dependencies.

[![GitHub license](https://img.shields.io/badge/license-mit-blue.svg)](https://github.com/dolifer/NGql/blob/main/LICENSE)

## Quick Start

```shell
dotnet add package NGql.Core
```

## Overview

NGql provides two powerful approaches for building GraphQL queries:

1. **Classic API** - Direct query construction with nested objects
2. **QueryBuilder API** - Modern fluent interface with advanced features

Both approaches support .NET 6.0, 7.0, 8.0, and 9.0.

---

# Classic API

The original NGql API for direct query construction.

## Basic Query

```c#
var query = new Query("PersonAndFilms")
    .Select(new Query("person")
        .Where("id", "cGVvcGxlOjE=")
        .Select("name")
        .Select(new Query("filmConnection")
            .Select(new Query("films")
                .Select("title")))
    );
```

**Output:**
```graphql
query PersonAndFilms{
    person(id:"cGVvcGxlOjE="){
        name
        filmConnection{
            films{
                title
            }
        }
    }
}
```

## Mutation

```c#
var mutation = new Mutation("CreateUser")
    .Select(new Query("createUser")
        .Where("name", "Name")
        .Where("password", "Password")
        .Select("id", "name"));
```

**Output:**
```graphql
mutation CreateUser{
    createUser(name:"Name", password:"Password"){
        id
        name
    }
}
```

## Variables

```c#
var variable = new Variable("$name", "String");
var query = new Query("GetUser", variables: variable)
    .Select(new Query("user")
        .Where("name", variable)
        .Select("id", "name"));
```

**Output:**
```graphql
query GetUser($name:String){
    user(name:$name){
        id
        name
    }
}
```

---

# QueryBuilder API ✨

The modern fluent API with advanced features for complex query construction.

## Basic Usage

### Simple Fields

```c#
// New QueryBuilder API
var query = QueryBuilder
    .CreateDefaultBuilder("GetUsers")
    .AddField("users.name")
    .AddField("users.email");
```

**Output:**
```graphql
query GetUsers{
    users{
        name
        email
    }
}
```

### Multiple Fields with Dot Notation

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("ComplexQuery")
    .AddField("user.profile.name")
    .AddField("user.profile.avatar.url")
    .AddField("user.posts.title")
    .AddField("user.posts.comments.author");
```

**Output:**
```graphql
query ComplexQuery{
    user{
        profile{
            name
            avatar{
                url
            }
        }
        posts{
            title
            comments{
                author
            }
        }
    }
}
```

## Advanced Features

### Field Types with Dot Notation

Specify field types directly in the field path:

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("TypedQuery")
    .AddField("User user.profile")
    .AddField("String user.name")
    .AddField("Int user.age")
    .AddField("[] user.tags")           // Array marker
    .AddField("Post[] user.posts");     // Typed array
```

### Field Arguments

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("UsersWithArgs")
    .AddField("users", new Dictionary<string, object?>
    {
        { "first", 10 },
        { "after", "cursor123" },
        { "orderBy", new EnumValue("CREATED_AT") }
    })
    .AddField("users.name")
    .AddField("users.email");
```

**Output:**
```graphql
query UsersWithArgs{
    users(first:10, after:"cursor123", orderBy:CREATED_AT){
        name
        email
    }
}
```

### Variables

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUserById")
    .AddField("user", new Dictionary<string, object?>
    {
        { "id", new Variable("$userId", "ID!") }
    })
    .AddField("user.name")
    .AddField("user.email");
```

**Output:**
```graphql
query GetUserById($userId:ID!){
    user(id:$userId){
        name
        email
    }
}
```

### Field Aliases

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("AliasedQuery")
    .AddField("userName:user.name")
    .AddField("userEmail:user.email")
    .AddField("postTitles:user.posts.title");
```

**Output:**
```graphql
query AliasedQuery{
    userName:user{
        name
    }
    userEmail:user{
        email
    }
    postTitles:user{
        posts{
            title
        }
    }
}
```

### SubFields Syntax

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("SubFieldsQuery")
    .AddField("user", subFields: ["name", "email"])
    .AddField("user.posts", subFields: ["title", "content", "publishedAt"]);
```

**Output:**
```graphql
query SubFieldsQuery{
    user{
        name
        email
        posts{
            title
            content
            publishedAt
        }
    }
}
```

### Query Composition and Merging

```c#
// Create reusable query fragments
var userFragment = QueryBuilder
    .CreateDefaultBuilder("UserFragment")
    .AddField("user.name")
    .AddField("user.email")
    .AddField("user.profile.avatar");

var postsFragment = QueryBuilder
    .CreateDefaultBuilder("PostsFragment")
    .AddField("user.posts.title")
    .AddField("user.posts.publishedAt");

// Combine fragments
var combinedQuery = QueryBuilder
    .CreateDefaultBuilder("CombinedQuery")
    .Include(userFragment)
    .Include(postsFragment);
```

### Complex Arguments with Nested Objects

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("ComplexArgs")
    .AddField("searchUsers", new Dictionary<string, object?>
    {
        { "filter", new Dictionary<string, object?>
            {
                { "name", new Variable("$name", "String") },
                { "age", new Dictionary<string, object?>
                    {
                        { "gte", 18 },
                        { "lte", 65 }
                    }
                },
                { "status", new EnumValue("ACTIVE") }
            }
        },
        { "pagination", new
            {
                first = 20,
                after = new Variable("$cursor", "String")
            }
        }
    })
    .AddField("searchUsers.edges.node.name")
    .AddField("searchUsers.pageInfo.hasNextPage");
```

### Field Builder Actions

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("FieldBuilderQuery")
    .AddField("user", fieldBuilder =>
    {
        fieldBuilder.AddField("name")
                   .AddField("email")
                   .AddField("profile", profileBuilder =>
                   {
                       profileBuilder.AddField("bio")
                                   .AddField("avatar");
                   });
    });
```

### Metadata Support

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("MetadataQuery")
    .AddField("user.name", metadata: new Dictionary<string, object?>
    {
        { "description", "User's display name" },
        { "required", true }
    })
    .WithMetadata(new Dictionary<string, object>
    {
        { "version", "1.0" },
        { "author", "API Team" }
    });
```

## Query Merging Strategies 🔄

One of the most powerful features of the QueryBuilder API is intelligent query merging. When combining multiple query fragments using `.Include()`, NGql can automatically merge compatible queries to optimize the final GraphQL output.

### Available Strategies

| Strategy | Description | Use Case |
|----------|-------------|----------|
| `MergeByDefault` | Inherits merging behavior from parent (default) | Most flexible, adapts to context |
| `MergeByFieldPath` | Merges queries with compatible field paths and arguments | Optimizing similar queries |
| `NeverMerge` | Always keeps queries separate | When you need guaranteed separation |

### 1. MergeByFieldPath Strategy

Automatically merges queries that have compatible field paths and arguments:

```c#
// Create root query with MergeByFieldPath strategy
var rootQuery = QueryBuilder
    .CreateDefaultBuilder("OptimizedQuery", MergingStrategy.MergeByFieldPath)
    .AddField("users", ["id", "name"]);

// Fragment 1: Same path, no arguments - WILL MERGE
var emailFragment = QueryBuilder
    .CreateDefaultBuilder("EmailFragment")
    .AddField("users", ["email"]);

// Fragment 2: Different path - WILL MERGE (compatible)
var profileFragment = QueryBuilder
    .CreateDefaultBuilder("ProfileFragment")
    .AddField("users.profile", ["bio", "avatar"]);

// Fragment 3: Same path but with arguments - WON'T MERGE
var filteredFragment = QueryBuilder
    .CreateDefaultBuilder("FilteredFragment")
    .AddField("users", new Dictionary<string, object?> { {"status", "active"} }, ["role"]);

var finalQuery = rootQuery
    .Include(emailFragment)      // Merges into main "users" field
    .Include(profileFragment)    // Merges as nested field
    .Include(filteredFragment);  // Creates separate field path
```

**Output:**
```graphql
query OptimizedQuery{
    users{
        id
        name
        email
        profile{
            bio
            avatar
        }
    }
    users_1(status:"active"){
        role
    }
}
```

### 2. NeverMerge Strategy

Forces queries to remain separate, even if they could be merged:

```c#
var rootQuery = QueryBuilder
    .CreateDefaultBuilder("SeparateQueries", MergingStrategy.MergeByFieldPath)
    .AddField("users", ["id"]);

// This fragment will NEVER merge due to NeverMerge strategy
var separateFragment = QueryBuilder
    .CreateDefaultBuilder("AlwaysSeparate", MergingStrategy.NeverMerge)
    .AddField("users", ["name", "email"]);  // Same path but won't merge

var finalQuery = rootQuery.Include(separateFragment);
```

**Output:**
```graphql
query SeparateQueries{
    users{
        id
    }
    users_1{
        name
        email
    }
}
```

### 3. MergeByDefault Strategy

Inherits the merging behavior from the parent query:

```c#
// Root uses MergeByFieldPath
var rootQuery = QueryBuilder
    .CreateDefaultBuilder("InheritedBehavior", MergingStrategy.MergeByFieldPath)
    .AddField("users", ["id"]);

// Child uses MergeByDefault - will inherit MergeByFieldPath behavior
var childFragment = QueryBuilder
    .CreateDefaultBuilder("ChildFragment", MergingStrategy.MergeByDefault)
    .AddField("users", ["name"]);  // Will merge because parent allows it

var finalQuery = rootQuery.Include(childFragment);
```

**Output:**
```graphql
query InheritedBehavior{
    users{
        id
        name
    }
}
```

### Dynamic Strategy Assignment

You can change merging strategies at runtime:

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("DynamicQuery")
    .WithMergingStrategy(MergingStrategy.MergeByFieldPath)
    .AddField("users.profile", ["name"]);

// Later change strategy
query.WithMergingStrategy(MergingStrategy.NeverMerge);
```

### Complex Merging Example

```c#
// Root query optimizes by field path
var mainQuery = QueryBuilder
    .CreateDefaultBuilder("ComplexMerging", MergingStrategy.MergeByFieldPath)
    .AddField("organization.departments", ["id", "name"]);

// Fragment 1: Compatible path - WILL MERGE
var departmentDetails = QueryBuilder
    .CreateDefaultBuilder("DeptDetails")
    .AddField("organization.departments", ["budget", "headCount"]);

// Fragment 2: Nested compatible path - WILL MERGE
var teamInfo = QueryBuilder
    .CreateDefaultBuilder("TeamInfo")
    .AddField("organization.departments.teams", ["name", "lead"]);

// Fragment 3: Same path with arguments - WON'T MERGE
var activeDepartments = QueryBuilder
    .CreateDefaultBuilder("ActiveDepts")
    .AddField("organization.departments", 
        new Dictionary<string, object?> { {"status", "active"} }, 
        ["description"]);

// Fragment 4: Force separation - WON'T MERGE
var separateQuery = QueryBuilder
    .CreateDefaultBuilder("Separate", MergingStrategy.NeverMerge)
    .AddField("organization.departments", ["location"]);

var result = mainQuery
    .Include(departmentDetails)  // Merges
    .Include(teamInfo)          // Merges as nested
    .Include(activeDepartments) // Separate due to arguments
    .Include(separateQuery);    // Separate due to NeverMerge
```

**Output:**
```graphql
query ComplexMerging{
    organization{
        departments{
            id
            name
            budget
            headCount
            teams{
                name
                lead
            }
        }
        departments_1(status:"active"){
            description
        }
        departments_2{
            location
        }
    }
}
```

### Merging Rules

1. **Field Path Compatibility**: Queries merge if their field paths are compatible (same root or nested)
2. **Argument Matching**: Queries with different arguments create separate field instances
3. **Strategy Hierarchy**: Child strategies are overridden by parent `NeverMerge` strategies
4. **Type Safety**: Conflicting field types prevent merging and throw exceptions

## Comparison: Classic vs QueryBuilder

| Feature | Classic API | QueryBuilder API |
|---------|-------------|------------------|
| **Simple Query** | `new Query("users").Select("name")` | `QueryBuilder.CreateDefaultBuilder("GetUsers").AddField("users.name")` |
| **Nested Fields** | Multiple nested `Query` objects | Dot notation: `"user.profile.name"` |
| **Field Types** | Not supported | `"String user.name"` or inline type specification |
| **Arguments** | `.Where("id", value)` | `.AddField("user", new Dictionary<string, object?> { {"id", value} })` |
| **Aliases** | Not directly supported | `"alias:field"` syntax |
| **Query Merging** | Manual composition | Automatic with `.Include()` and strategies |
| **Variables** | Constructor or `.Variable()` | Automatic detection from arguments |
| **Array Types** | Not supported | `"[]"` and `"Type[]"` markers |
| **Reusability** | Limited | High with fragments and merging |

## Migration Guide

### Simple Query Migration

**Before (Classic):**
```c#
var query = new Query("GetUsers")
    .Select(new Query("users")
        .Select("name")
        .Select("email"));
```

**After (QueryBuilder):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUsers")
    .AddField("users.name")
    .AddField("users.email");
```

### Arguments Migration

**Before (Classic):**
```c#
var query = new Query("GetUser")
    .Select(new Query("user")
        .Where("id", "123")
        .Select("name"));
```

**After (QueryBuilder):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user", new Dictionary<string, object?> { {"id", "123"} })
    .AddField("user.name");
```

### Variables Migration

**Before (Classic):**
```c#
var variable = new Variable("$id", "ID!");
var query = new Query("GetUser", variables: variable)
    .Select(new Query("user")
        .Where("id", variable)
        .Select("name"));
```

**After (QueryBuilder):**
```c#
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user", new Dictionary<string, object?> 
    { 
        {"id", new Variable("$id", "ID!")} 
    })
    .AddField("user.name");
```

## Advanced Scenarios

### Dynamic Query Building

```c#
var queryBuilder = QueryBuilder.CreateDefaultBuilder("DynamicQuery");

// Conditionally add fields
if (includeProfile)
{
    queryBuilder.AddField("user.profile.name")
                .AddField("user.profile.bio");
}

if (includePosts)
{
    queryBuilder.AddField("user.posts.title")
                .AddField("user.posts.publishedAt");
}

var query = queryBuilder.ToString();
```

### Path Resolution

```c#
var query = QueryBuilder
    .CreateDefaultBuilder("PathQuery")
    .AddField("user.posts.comments.author");

// Get path to a specific query part
string[] pathToComments = query.GetPathTo("user", "posts.comments");
// Returns: ["user", "posts", "comments"]
```

### Performance Optimizations

The QueryBuilder API includes several performance optimizations:

- **Memory Efficient**: Uses `Span<T>` and `ReadOnlySpan<T>` for reduced allocations
- **Field Caching**: Intelligent caching of field definitions by path
- **Optimized Parameters**: Uses `in` parameters for large data structures
- **Smart Merging**: Efficient query merging with configurable strategies

---

## Best Practices

1. **Use QueryBuilder for New Projects**: The QueryBuilder API provides better maintainability and features
2. **Leverage Dot Notation**: Use `"parent.child.field"` syntax for cleaner code
3. **Create Reusable Fragments**: Build common query patterns as reusable components with `.Include()`
4. **Choose Appropriate Merging Strategies**: Use `MergeByFieldPath` for optimization, `NeverMerge` for guaranteed separation
5. **Type Your Fields**: Use type annotations for better GraphQL schema compliance
6. **Use Variables**: Prefer variables over hardcoded values for reusability
7. **Organize Complex Queries**: Break large queries into smaller, composable fragments

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
