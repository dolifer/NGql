<h1 align="center">

<img src="https://github.com/dolifer/NGql/blob/main/icon.png" alt="NGql" width="200"/>
<br/>
NGql
</h1>

<div align="center">

Schemaless GraphQL client for .NET Core.

[![GitHub license](https://img.shields.io/badge/license-mit-blue.svg)](https://github.com/dolifer/NGql/blob/main/LICENSE)

</div>

## Installation

```
dotnet add package NGql.Core
```

## Core

Core library allows creation of `Query` and `Muration`'s.
Both returns a `QueryBase` that have an implicit convertion to `string`.

### Query
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

### Mutation
```csharp
var mutation = new Mutation("CreateUser")
                .Select(new Query("createUser")
                    .Where("name", "Name")
                    .Where("password", "Password")
                    .Select("id", "name"))
                .ToString();
```

## Client

Client allows to make a call agains GQL endpoint with a given `Query` or `Mutation`.
Added for demo purpose to show basic usage.

```csharp
using var client = new NGqlClient("https://swapi-graphql.netlify.app/.netlify/functions/index");
var query = new Query("PersonAndFilms")
    .Select(new Query("person")
        .Where("id", "cGVvcGxlOjE=")
        .Select("name")
        .Select(new Query("filmConnection")
            .Select(new Query("films")
                .Select("title")))
    );

var response = await client.QueryAsync<PersonAndFilmsResponse>(query);
```

The `response` variable will be a following JSON object
```json
{
    "Person": {
        "Name": "Luke Skywalker",
        "FilmConnection": {
            "Films": [
                {
                    "Title": "A New Hope"
                },
                {
                    "Title": "The Empire Strikes Back"
                },
                {
                    "Title": "Return of the Jedi"
                },
                {
                    "Title": "Revenge of the Sith"
                }
            ]
        }
    }
}
```
