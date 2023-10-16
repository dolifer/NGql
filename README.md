![Project Logo](https://raw.githubusercontent.com/dolifer/NGql/main/icon.png) 
# NGql

Schemaless GraphQL query builder for .NET Coren with fluent syntax. Zero dependencies.

[![GitHub license](https://img.shields.io/badge/license-mit-blue.svg)](https://github.com/dolifer/NGql/blob/main/LICENSE)

# Quick Start

```shell
dotnet add package NGql.Core
```

# Usage

Library allows creation of `Query` and `Mutation`'s.
Both have an implicit conversion to `string`.

## Query
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
the output will be the following
```
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
                    .Select("id", "name"))
                .ToString();
```
the output will be the following
```
mutation CreateUser{
    createUser(name:"Name", password:"Password"){
        id
        name
    }
}
```

## Variables
Variables allows to reuse existing queries and mutations instead of building them from start every time.

### Passing variables

```c#
// define a variable
var variable = new Variable("$name", "String");

// pass as constructor parameter
var ctor = new Query("name", variables: variable);

// or as method variable
var fluent = new Query("name", variables: variable).Variable(variable););
```
the output will be the following
```
query name($name:String){
    id
}
```
