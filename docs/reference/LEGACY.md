# Legacy: Classic API (NGql 1.x)

> **⚠️ Deprecated**: This API is no longer recommended. New projects should use the [QueryBuilder API](README.md) introduced in NGql 2.0.
> 
> If you're migrating from NGql 1.x, see the [Migration Guide](MIGRATION.md) for step-by-step instructions.

The Classic API was the original approach for building GraphQL queries in NGql. While still functional, it has been superseded by the more powerful and flexible QueryBuilder API.

---

## Overview

The Classic API uses direct query construction with nested `Query` and `Mutation` objects, similar to building a tree of GraphQL operations.

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

## Why This API Was Deprecated

The Classic API had several limitations:

1. **Verbose Nesting**: Building nested queries required multiple `new Query()` statements
2. **Limited Composability**: Reusing and combining queries was difficult
3. **No Automatic Merging**: Duplicate query paths couldn't be intelligently merged
4. **Type Safety Issues**: Field types couldn't be specified in the query itself
5. **Poor Field Aliasing**: Aliases weren't well supported
6. **No Array Type Support**: Array fields weren't explicitly supported

The QueryBuilder API solves all of these problems with a cleaner, more powerful design.

---

## Why You Might Still Need This

If you have a large codebase using the Classic API, you can:
- Continue using it (it's still fully functional)
- Migrate gradually using the [Migration Guide](MIGRATION.md)
- Use both APIs in the same application (they're compatible)

For new features and optimizations, however, the QueryBuilder API is strongly recommended.
