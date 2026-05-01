# NGql 2.0.0

NGql 2.0 reshapes how you build, compose, and filter GraphQL queries from .NET. The release introduces a new field-preservation API, a multi-strategy query merger, and a hot-path overhaul that removes most allocations from rendering — while keeping the library zero-dependency and small enough to read end-to-end.

## Highlights

### `PreservationBuilder` — extract a subset of an existing query

Pick fields by string path or by a typed C# expression. No SDL, no codegen.

```csharp
var profile = QueryBuilder.CreateDefaultBuilder("Profile")
    .AddField("user.name")
    .AddField("user.email")
    .AddField("user.ssn");

// String paths
var publicView = PreservationBuilder.Create(profile)
    .Preserve("user.name", "user.email")
    .Build();

// Typed LINQ expression — paths are extracted from the predicate
var conditional = PreservationBuilder.Create(profile)
    .PreserveFromExpression<UserDto>(x => x.user.email != null)
    .Build();
```

Useful for role-based filtering, conditional fragments, and stripping fields from a shared query without rebuilding it.

### `MergingStrategy` — control fragment composition

`Include(otherBuilder)` joins two query trees. The strategy decides how duplicate paths are handled:

- **`MergeByDefault`** — inherit the parent's strategy
- **`MergeByFieldPath`** — merge fragments with compatible paths and arguments; auto-alias when arguments conflict
- **`NeverMerge`** — keep fragments distinct (every `Include` produces a separate sub-tree)

```csharp
var combined = QueryBuilder
    .CreateDefaultBuilder("Combined", MergingStrategy.MergeByFieldPath)
    .Include(fragmentA)
    .Include(fragmentB);
```

### Hot-path performance pass

The internal rendering and merging code was rewritten to push most allocations out of the steady state:

- **In-place merge in `Include()`** — the second fragment's tree is folded into the first instead of being copied
- **Lock-free reads on `FieldChildren`** — uncontended dictionary lookups skip the lock entirely
- **Zero-allocation `GetPathTo` cache hits** via a two-level path index
- **Span-based path parsing** in `Helpers.cs`
- **`ArrayPool<T>`-backed argument storage** and `Dictionary<>` fields with lazy initialization

Query rendering itself is reflection-free; reflection is confined to the LINQ-expression preservation path.

### Multi-target: .NET 8, 9, and 10

The package now ships TFMs for `net8.0`, `net9.0`, and `net10.0`. The minimum supported runtime is .NET 8.

## Quality

- **1725 tests** (1634 unit + 91 integration), executed on all three target frameworks
- **99.93% line coverage / 99.66% branch coverage / 100% method coverage** on the Core namespace
- `TreatWarningsAsErrors` enforced across `src/`; SonarAnalyzer.CSharp 10.15 runs on every build
- HTML coverage report and badges published from CI to GitHub Pages on every push to `main`

## Installation

```bash
dotnet add package NGql.Core
```

```xml
<PackageReference Include="NGql.Core" Version="2.0.0" />
```

## Migration from 1.5.x

NGql 2.0 is a major version bump because the internal definition layer was reshaped to support the preservation and merging features. Source compatibility is preserved for the **classic API** (`Query`, `Mutation`, `.Where`, `.Select`); call sites using only those types should upgrade without changes.

If you build queries with the new `QueryBuilder` fluent API, see [`docs/reference/MIGRATION.md`](https://github.com/dolifer/NGql/blob/main/docs/reference/MIGRATION.md) for the full upgrade guide. The most common adjustments:

- `Preserve(...)` moved off `QueryBuilder` and onto a dedicated `PreservationBuilder` (call `PreservationBuilder.Create(builder)` to start).
- `MergingStrategy` replaces ad-hoc merge flags. Pass it to `CreateDefaultBuilder(name, strategy)`.
- Argument values that need to render unquoted should use the new `EnumValue` wrapper (`new EnumValue("ADMIN")`).

## Breaking changes

- Minimum runtime raised from .NET 6/7 to **.NET 8**
- Internal `QueryDefinitionExtensions` is now `internal` (was effectively unused outside the assembly)
- `PreserveExtensions` is internal — use `PreservationBuilder` instead
- A handful of defensive guards on internal types were removed because their preconditions are enforced by stronger invariants (no behavioral change for valid input)

## Tooling

The repository also got a refresh that affects contributors but not consumers:

- **NUKE removed** in favor of plain `dotnet` CLI invoked through a `Makefile`. Targets: `make build`, `make test`, `make coverage`, `make pack`, `make ci`, `make rebuild`.
- **Cross-platform**: a `make.cmd` shim makes `make` work from any Windows shell when Git Bash is installed.
- **Coverage merging** across test projects via coverlet's `MergeWith`, so reports reflect both unit and integration coverage.

## Acknowledgements

Thanks to everyone who reported edge cases against the 1.5.x line — the type-drift bug and the preservation/alias regressions surfaced by real usage drove most of the work behind this release.

---

**Full changelog:** https://github.com/dolifer/NGql/compare/1.5.0...2.0.0
**Documentation:** [README](https://github.com/dolifer/NGql#readme) · [Migration guide](https://github.com/dolifer/NGql/blob/main/docs/reference/MIGRATION.md) · [Coverage report](https://dolifer.github.io/NGql/)
