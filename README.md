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

## Comparison: Classic vs QueryBuilder

| Feature | Classic API | QueryBuilder API |
|---------|-------------|------------------|
| **Simple Query** | `new Query("users").Select("name")` | `QueryBuilder.CreateDefaultBuilder("GetUsers").AddField("users.name")` |
| **Nested Fields** | Multiple nested `Query` objects | Dot notation: `"user.profile.name"` |
| **Field Types** | Not supported | `"String user.name"` or inline type specification |
| **Arguments** | `.Where("id", value)` | `.AddField("user", new Dictionary<string, object?> { {"id", value} })` |
| **Aliases** | Not directly supported | `"alias:field"` syntax |
| **Query Merging** | Manual composition | Automatic with `.Include()` |
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
4. **Type Your Fields**: Use type annotations for better GraphQL schema compliance
5. **Use Variables**: Prefer variables over hardcoded values for reusability
6. **Organize Complex Queries**: Break large queries into smaller, composable fragments

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
