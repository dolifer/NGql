# Legacy: Classic API (NGql 1.x)

> **⚠️ Deprecated**: This API is no longer recommended. New projects should use the [QueryBuilder API](https://github.com/dolifer/NGql/blob/main/README.md) introduced in NGql 2.0.
> 
> If you're migrating from NGql 1.x, see the [Migration Guide](MIGRATION.md) for step-by-step instructions.

The Classic API was the original approach for building GraphQL queries in NGql. While still functional, it has been superseded by the more powerful and flexible QueryBuilder API.

---

## Overview

The Classic API uses direct query construction with nested `Query` and `Mutation` objects, similar to building a tree of GraphQL operations.

## Basic Query

<table>
<tr><th>C#</th><th>GraphQL</th></tr>
<tr><td>

```csharp
var query = new Query("PersonAndFilms")
    .Select(new Query("person")
        .Where("id", "cGVvcGxlOjE=")
        .Select("name")
        .Select(new Query("filmConnection")
            .Select(new Query("films")
                .Select("title")))
    );
```

</td><td>

```graphql
query PersonAndFilms{
    person(id:"cGVvcGxlOjE="){
        filmConnection{
            films{
                title
            }
        }
        name
    }
}
```

</td></tr>
</table>

## Mutation

<table>
<tr><th>C#</th><th>GraphQL</th></tr>
<tr><td>

```csharp
var mutation = new Mutation("CreateUser")
    .Select(new Query("createUser")
        .Where("name", "Name")
        .Where("password", "Password")
        .Select("id", "name"));
```

</td><td>

```graphql
mutation CreateUser{
    createUser(name:"Name", password:"Password"){
        id
        name
    }
}
```

</td></tr>
</table>

## Variables

<table>
<tr><th>C#</th><th>GraphQL</th></tr>
<tr><td>

```csharp
var variable = new Variable("$name", "String");
var query = new Query("GetUser", variables: variable)
    .Select(new Query("user")
        .Where("name", variable)
        .Select("id", "name"));
```

</td><td>

```graphql
query GetUser($name:String){
    user(name:$name){
        id
        name
    }
}
```

</td></tr>
</table>

---

## Mutation with Variables

<table>
<tr><th>C#</th><th>GraphQL</th></tr>
<tr><td>

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

</td><td>

```graphql
mutation CreateUser($email:String!, $name:String!){
    createUser(email:$email, name:$name){
        createdAt
        id
    }
}
```

</td></tr>
</table>

**Mutation API:**
- `new Mutation(name, params Variable[])` — declare the operation and its variables
- `.Variable(name, type)` / `.Variable(Variable)` — add more variables incrementally
- `.Select(params string[])` — add plain field names
- `.Select(Query subQuery)` — embed a `Query` (with its `Where`/`Select` arguments and subfields)
- `.Select(IEnumerable<object>)` — mixed list of strings and `QueryBlock`s

---

## Classic API and QueryBuilder Side by Side

| Feature | 1.5.x (Classic) | 2.x (QueryBuilder) |
|---------|-----------------|--------------------|
| Query creation | `new Query("name")` | `QueryBuilder.CreateDefaultBuilder("name")` |
| Nested fields | `.Select(new Query("child"))` | `.AddField("parent.child")` |
| Field arguments | `.Where("key", value)` | `.AddField("field", new Dictionary<string, object?> { … })` |
| Composing fragments | manual stitching | `Include(otherBuilder)` with `MergingStrategy` |
| Field-path subset | not available | `PreservationBuilder.Create(...).Preserve(...).Build()` |
| Type-annotation metadata | not available | `AddField("String user.name")` (metadata only — does not appear in rendered GraphQL) |

The Classic API (`Query`, `Mutation`) is still fully supported in 2.x and renders independently — it is **not** the internal representation `QueryBuilder` uses; both APIs produce GraphQL text through separate code paths. Use whichever fits your use case (or mix them: a `Mutation` can `Select` a hand-built `Query`, while `QueryBuilder` is the typical entry point for composable, dynamic queries).

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
