# Claude Instructions for NGql

## Overview

NGql is a **zero-dependency, schema-less GraphQL query builder for .NET**. It provides a fluent, type-safe API to construct GraphQL queries programmatically with automatic merging and field preservation. The codebase is performance-optimized using `Span<T>`, thread-local pooling, and minimal allocations.

**Supported Frameworks:** .NET 8.0, 9.0, 10.0

---

## Build, Test & Lint Commands

### Build & Test

A `Makefile` wraps the common operations. The same targets are used in CI:

```bash
make help       # list all targets
make build      # restore + build (Release)
make test       # build + run tests with coverage
make coverage   # test + generate HTML coverage report and badges
make pack       # produce nupkg in artifacts/packages
make ci         # clean + coverage (mirrors GitHub Actions)
make rebuild    # clean + test (force-clean local rebuild)
make clean      # remove artifacts/, bin/, obj/
```

**Windows:** the Makefile uses Bash, `find`, and `rm`, so it requires either **Git Bash** (bundled with [Git for Windows](https://git-scm.com/download/win)) or **WSL**. A `make.cmd` shim is included so `make test` works from any Windows shell — it shells out to Git Bash if available, otherwise prints an install hint.

Underlying commands (if you prefer to skip make):

```bash
dotnet restore NGql.sln
dotnet build NGql.sln --configuration Release
dotnet test NGql.sln --configuration Release --no-build
dotnet pack src/Core/Core.csproj --configuration Release --output artifacts/packages
```

### Running Tests

```bash
# Run all tests with coverage
dotnet test

# Run specific test project
dotnet test tests/Core.Tests/Core.Tests.csproj

# Run single test file
dotnet test tests/Core.Tests/Core.Tests.csproj --filter "FullyQualifiedName~QueryBuilderTests"

# Run with specific test method
dotnet test tests/Core.Tests/Core.Tests.csproj --filter "FullyQualifiedName~QueryBuilderTests.QueryBuilder_AddField_With_Type_Only_Should_Work"
```

### Code Analysis

- **Roslyn Analyzers**: SonarAnalyzer.CSharp (v10.15.0) runs automatically during build
- **Implicit warnings**: Configure in `.editorconfig` or via `Directory.Build.props`
- **No explicit linter**: Rely on analyzers and EditorConfig rules

### Coverage Reports

Coverage reports are generated during test execution and placed in:
- `artifacts/coverage-report/` — HTML coverage report
- `artifacts/test-results/` — JUnit and Coverlet results

---

## Project Structure & Architecture

### High-Level Architecture

NGql follows a **builder pattern** with layered separation:

```
┌─────────────────────────────────────────┐
│         Public API Layer                │
│  QueryBuilder, Mutation, Query, Variable│
└────────────┬────────────────────────────┘
             │
┌────────────▼─────────────────────────────┐
│      Definition & Composition Layer      │
│  QueryDefinition, FieldDefinition,       │
│  MergingStrategy, QueryMerger            │
└────────────┬─────────────────────────────┘
             │
┌────────────▼─────────────────────────────┐
│       Low-Level Rendering Layer          │
│  QueryTextBuilder, FieldBuilder,         │
│  FieldFactory, ValueFormatter            │
└────────────┬─────────────────────────────┘
             │
┌────────────▼─────────────────────────────┐
│      Support Infrastructure              │
│  Pooling/ (ThreadLocalMemoryManager,     │
│  LockFree*Pool), Caching/ (TypeCache),   │
│  Observability/, Extensions, Helpers     │
└─────────────────────────────────────────┘
```

### Directory Layout

```
src/Core/
├── Builders/              # Public API + composition builders
│   ├── QueryBuilder.cs              # Main fluent API entry point
│   ├── FieldBuilder.cs              # Field hierarchy construction
│   ├── FieldFactory.cs              # Field-creation helpers
│   ├── PreservationBuilder.cs       # Field-subset extraction
│   ├── QueryTextBuilder.cs          # GraphQL text rendering
│   ├── ExpressionPreservationProcessor.cs  # LINQ-expression preservation
│   └── NavigationPropertyExpander.cs       # Reflection-based traversal
├── Abstractions/          # Core data models
│   ├── FieldDefinition.cs           # Field metadata & hierarchy
│   ├── QueryDefinition.cs           # Query structure
│   └── FieldChildren.cs, QueryBlock.cs, …
├── Features/              # High-level features
│   ├── QueryMerger.cs               # Intelligent query merging
│   ├── PreserveExtensions.cs        # Internal preservation helpers
│   ├── QueryMap.cs                  # Path/alias indexing
│   └── FieldSignatureGenerator.cs, KeyGenerator.cs
├── Observability/         # PoolingObservability, NGqlActivity, NGqlTelemetry
├── Extensions/            # Helpers.cs (span-based parsing) and friends
├── Pooling/               # Memory pooling primitives
├── Caching/               # TypeCache
├── Exceptions/            # Custom exception types
├── Variable.cs            # GraphQL variables
├── Query.cs, Mutation.cs  # Classic-API entry points
├── EnumValue.cs           # Unquoted-enum argument wrapper
├── MergingStrategy.cs     # Merging behavior enum
└── ValueFormatter.cs      # Argument-value rendering

tests/Core.Tests/
├── Builders/              # QueryBuilder & FieldBuilder tests
├── Abstractions/          # Data-model tests
├── Extensions/            # Parsing & rendering tests
├── Models/                # Test infrastructure (TestScenarioBag, ExpressionsBag, ScenarioBag)
├── QueryBuilderTests.cs, QueryDefinitionTests.cs, MutationTests.cs
└── Issues/                # Regression tests for bug fixes (with snapshots)

tests/Core.IntegrationTests/   # Real composition scenarios
tests/BenchmarkRunner/         # BenchmarkDotNet performance harness
```

### Public API Surface

**Core entry points:**

1. **QueryBuilder** — fluent API for building queries
   - `QueryBuilder.CreateDefaultBuilder(string name)` or `(string name, MergingStrategy)` — create a builder
   - `.AddField(path, …)` — add a field (multiple overloads for arguments / sub-fields / metadata)
   - `.Include(QueryBuilder)` — compose with another builder (subject to `MergingStrategy`)
   - `.WithMetadata(...)` — attach metadata to the query
   - `.ToString()` — render to GraphQL

2. **PreservationBuilder** (created via `PreservationBuilder.Create(QueryBuilder)`)
   - `.Preserve(params string[] fieldPaths)` — keep listed paths
   - `.PreserveAtPath(string fieldPath, string nodePath)` — keep `fieldPath` only when scoped under `nodePath`
   - `.PreserveFromExpression<T>(Expression<Func<T,bool>>)` — extract paths from a LINQ predicate
   - `.Build()` — return a new `QueryBuilder` containing only the preserved subset

3. **Query / Mutation** (classic API)
   - `new Query(name, params Variable[])` / `new Mutation(name, params Variable[])`
   - `.Variable(name, type)` to add variables
   - `.Select(...)` to add fields, sub-queries, or other Query objects

4. **Variable** — `new Variable("$name", "Type!")` (the type string is opaque metadata)

5. **EnumValue** — `new EnumValue("ADMIN")` to pass an enum literal as an argument unquoted

6. **MergingStrategy** enum
   - `MergeByDefault` — inherit from parent (parent's strategy applies during `Include`)
   - `MergeByFieldPath` — merge fragments with compatible paths and arguments; auto-alias on conflict
   - `NeverMerge` — keep fragments distinct (used for debugging or when GraphQL spec requires it)

---

## Key Conventions

### 1. Namespace Organization

```csharp
// Namespaces follow folder structure
namespace NGql.Core.Builders;      // src/Core/Builders/
namespace NGql.Core.Abstractions;  // src/Core/Abstractions/
namespace NGql.Core.Features;      // src/Core/Features/
```

Test namespaces mirror production:
```csharp
namespace NGql.Core.Tests.Builders;      // tests/Core.Tests/Builders/
namespace NGql.Core.Tests.Abstractions;  // tests/Core.Tests/Abstractions/
```

### 2. Implicit Usings & File-Scoped Namespaces

- **Implicit usings enabled** in `Core.csproj` (`<ImplicitUsings>enable</ImplicitUsings>`)
- **File-scoped namespaces** required: `namespace NGql.Core.Builders;` (not braced)
- Explicit `using` directives are still used for less common namespaces and project-internal namespaces (`NGql.Core.Abstractions`, `NGql.Core.Extensions`, etc.)

### 3. Testing Patterns

**Test framework:** xUnit + FluentAssertions

#### Structure: AAA Format (Arrange, Act, Assert)

Always follow the AAA pattern and keep tests **compact and readable**:

```csharp
using FluentAssertions;
using Xunit;
using NGql.Core.Builders;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderTests
{
    // Fact test for unique behavior
    [Fact]
    public void QueryBuilder_AddField_Should_Work()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.name");

        // Assert
        query.ToString().Should().Contain("user");
    }

    // Theory test with ExpressionsBag for consolidation
    [Theory]
    [InlineData("user")]
    [InlineData("users")]
    public void QueryBuilder_Handles_FieldNames(string field)
    {
        // Arrange
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField(field);

        // Act & Assert (combined when appropriate)
        query.ToString().Should().Contain(field);
    }
}
```

#### Scenario Bags for Theory Consolidation

For tests with multiple similar scenarios, use a scenario-bag to register named scenarios local to the test. Three flavors live in `tests/Core.Tests/Models/`:

- `ScenarioBag<T, TSelf>` — abstract base with `.Register(name, value).Get(name)`
- `ExpressionsBag<TModel>` / `ExpressionsBag<TModel, TReturn>` — bag of `Expression<Func<…>>` (used for LINQ-extraction tests)
- `TestScenarioBag<T>` / `TestScenarioBag<TSetup, T>` — bag of richer test scenarios with setup + expected value

```csharp
[Theory]
[InlineData("SimpleProperty", "user.age")]
[InlineData("NestedProperty", "user.profile.name")]
[InlineData("DeepProperty", "metrics.realtime.deposits.amount")]
public void ExtractPaths_SingleField_ReturnsSinglePath(string scenario, string expectedPath)
{
    var expr = new ExpressionsBag<TestModel>()
        .Register("SimpleProperty", x => x.user.age > 18)
        .Register("NestedProperty", x => x.user.profile.name != null)
        .Register("DeepProperty", x => x.metrics.realtime.deposits.amount > 0)
        .Get(scenario);

    var result = SomeExtractor.Extract(expr);

    result.Should().ContainSingle().Which.Should().Be(expectedPath);
}
```

**Key principles:**
- **Keep bags local** to test methods (not centralized) for clarity
- **Use identical assertions** across all scenarios in a Theory (e.g., all assert `.ContainSingle()`)
- **Split into separate tests** if assertions differ by scenario (one expects 1 field, another expects 3)
- **Leave Facts as Facts** for unique or standalone scenarios

#### Naming Convention

`[PublicMethod]_[Scenario or Behavior]_[ExpectedResult]`

Examples:
- `ExtractPaths_SingleField_ReturnsSinglePath`
- `QueryBuilder_BinaryAnd_ReturnsAllFields`
- `ComplexLogic_WithNestedExpressions_ExtractsAllPaths`

### 4. Performance & Memory

**Critical optimization techniques used:**

1. **Span<T> for parsing** — Avoid string allocations in hot paths
   ```csharp
   // In Helpers.cs: IsSimpleField, IsDottedField use ReadOnlySpan<char>
   public static bool IsSimpleField(ReadOnlySpan<char> path)
   ```

2. **ThreadLocalMemoryManager pooling** — 4-slot thread-local + 64-item global stack
   - Used for temporary StringBuilder instances
   - Check `ValidateForReturn()` to prevent oversized objects from bloating pool

3. **SortedDictionary caching** — Avoid recreation in loops
   ```csharp
   // Anti-pattern: recreate every iteration
   arguments = arguments as SortedDictionary<...> ?? new SortedDictionary<...>();
   ```

4. **Lazy initialization** — TypeCache uses `_combinedArray` for O(1) lookups instead of repeated loops

### 5. Zero-Dependency Philosophy

- **No external packages** in Core library (except build-time tools like GitVersion, SonarAnalyzer)
- All string formatting, parsing, and logic implemented from scratch
- Reflection is confined to `NavigationPropertyExpander`, `ExpressionPreservationProcessor`,
  `TypeCache`, `QueryBlockObjectExtensions`, and `ObjectMetadataExtensions`. Query rendering
  (`QueryTextBuilder`, `FieldFactory`, `Helpers.SortArgumentValue`, `ValueFormatter`) is
  reflection-free.

### 6. Expression Preservation (Query Filtering)

Used for role-based field filtering:
```csharp
var fullQuery = QueryBuilder.CreateDefaultBuilder("FullProfile")
    .AddField("user.name")
    .AddField("user.email")
    .AddField("user.ssn");  // Sensitive

// Extract only safe fields
var publicQuery = PreservationBuilder
    .Create(fullQuery)
    .Preserve("user.name")
    .Build();
```

### 7. Query Merging Strategies

- **MergeByFieldPath** (default for optimization):
  - Merges fragments with identical paths and arguments
  - Separate fragments when paths differ or arguments conflict
  - Reduces network payload

- **NeverMerge**:
  - Used for debugging or when GraphQL spec requires separation
  - Forces distinct field instances

### 8. Handling Dot Notation Paths

Paths use dot notation for hierarchy: `"user.profile.settings.privacy"`

- **Parsing** happens in `Helpers.cs` (span-based, zero-allocation)
- **Field building** happens in `FieldBuilder.cs` (recursive creation)
- **Rendering** happens in `QueryTextBuilder.cs` (flat GraphQL output)

### 9. Error Handling

- Specific exception types in `Exceptions/` folder
- No generic catch blocks (specific exception handling preferred)
- Exceptions include helpful messages for debugging

### 10. Code Style

- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`)
- **PascalCase** for public members
- **_camelCase** for private fields
- **LangVersion: latest** — Use latest C# features
- **4-space indentation** (via EditorConfig)
- **Insert final newlines** in all files
- **No comments** unless code needs clarification
- **Prefer records** for immutable data models
- **Prefer sealed classes** for non-extensible types

---

## Test & Coverage Expectations

### Coverage Requirements

- **Target:** 100% line coverage for Core namespace
- **Strategy:** Test public API surfaces; implementation details covered indirectly
- **Coverage tracked:** Via Coverlet during `dotnet test`

### Test Organization

- **Unit tests** — Fast, isolated, focus on public API
- **Integration tests** — In `Core.IntegrationTests/` (real composition scenarios)
- **Regression tests** — In `Issues/` subfolder (bug fix validation)

### Running Tests Before Commit

```bash
# Full check (with coverage)
make coverage
```

---

## Git Conventions

### Commit Messages

Strict single-line format using conventional commits:

```
type: short-description

# Types: fix, feat, docs, chore, refactor, test
# Examples:
# feat: add field preservation API
# fix: correct double allocation in Helpers
# test: add parametrized tests for dot notation
# docs: update migration guide
```

- **Maximum ~70 characters** for the title
- **NO bullet points, paragraphs, or wrapping**
- **No phase/priority references** (P0, Phase 9.3, etc.)

### Branch Protection

Recommended (configure in repo settings as needed):
- Tests passing before merge
- Coverage held at or above baseline (~99.9% line / ~99.3% branch)

---

## Documentation Files

- **README.md** — User guide with API examples and best practices
- **docs/reference/MIGRATION.md** — Upgrade guide from 1.5.x to 2.x
- **docs/reference/LEGACY.md** — Deprecated API reference (still functional)

---

## Common Development Tasks

### Adding a New Feature

1. Create test(s) first (TDD approach)
2. Implement in appropriate layer (Builders, Features, Abstractions, etc.)
3. Run full test suite: `make test`
4. Update documentation if user-facing
5. Commit with `feat:` prefix

### Fixing a Bug

1. Add regression test in `tests/Core.Tests/Issues/` to prevent recurrence
2. Implement fix in source code
3. Verify all tests pass
4. Commit with `fix:` prefix

### Performance Optimization

1. Profile using BenchmarkRunner (`tests/BenchmarkRunner/`)
2. Identify bottleneck (avoid premature optimization)
3. Implement fix using span-based or pooling patterns
4. Benchmark improvement
5. Add test coverage for change
6. Commit with `perf:` prefix (or `refactor:` if structural)

### Code Review Checklist

- [ ] Tests added/updated for new logic
- [ ] No external dependencies introduced
- [ ] Memory allocations minimized (use spans/pooling where appropriate)
- [ ] Documentation updated if user-facing
- [ ] Commit message format correct
- [ ] All tests passing (`make test`)
- [ ] Coverage maintained or improved

---

## Useful Resources

- **GitHub Actions:** `.github/workflows/` for CI/CD automation
- **Benchmarking:** `tests/BenchmarkRunner/` for performance analysis
- **Memory Profiling:** `ThreadLocalMemoryManager` and `PoolingObservability` for diagnostics

---

## Quick Reference: API Examples

### Basic Query
```csharp
var query = QueryBuilder
    .CreateDefaultBuilder("GetUser")
    .AddField("user.id")
    .AddField("user.name");
```

### Query Merging
```csharp
var combined = QueryBuilder
    .CreateDefaultBuilder("Combined", MergingStrategy.MergeByFieldPath)
    .Include(fragment1)
    .Include(fragment2);
```

### Field Preservation
```csharp
var filtered = PreservationBuilder
    .Create(fullQuery)
    .Preserve("user.name", "user.email")
    .Build();
```

### Mutations
```csharp
var mutation = new Mutation("CreateUser",
    new Variable("$name", "String!")
)
.Select("createUser(name: $name)")
.Select("createUser.id");
```
