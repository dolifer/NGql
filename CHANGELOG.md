# Changelog

All notable changes to **NGql.Core** and its companion `dotnet-ngql` CLI are recorded here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Per-release deep-dive notes live under [`docs/<version>/RELEASE_NOTES.md`](docs/) when they exist; this file is the terse summary intended for upgrade decisions.

The companion Claude Code Skill is versioned independently — see [`.claude/skills/ngql-local/CHANGELOG.md`](.claude/skills/ngql-local/CHANGELOG.md).

## [Unreleased]

### Added
- `QueryBuilder.CreateSubscriptionBuilder(name)` and `CreateSubscriptionBuilder(name, MergingStrategy)` — completes the query/mutation/subscription trio on the fluent builder. The surface (`AddField`, `Include`, `WithMetadata`, merging) is identical to the query and mutation paths; only the leading operation keyword differs, rendering as `subscription Name(...) { … }`. New `OperationType.Subscription` backs the render.
- **Named fragment support** via `QueryBuilder.AddFragment(name, onType, build)` + `FieldBuilder.SpreadFragment(name)`. Renders as `fragment Name on TypeName { … }` after the operation block (sorted alphabetically by name), with `...Name` spreads emitted at each use site (declaration order preserved). Spreads work inside fields, inline fragments, and other named fragments. New `InlineFragmentDefinition.SpreadFragments` and `NamedFragmentDefinition.SpreadFragments` properties expose the data model. Closes [#20](https://github.com/dolifer/NGql/issues/20). Public API additions: `QueryBuilder.AddFragment`, `FieldBuilder.SpreadFragment`, `QueryDefinition.NamedFragments`, `FieldDefinition.SpreadFragments`, `Abstractions.NamedFragmentDefinition`.
- **Field directive support** via `FieldBuilder.Include(ifVariable)`, `FieldBuilder.Skip(ifVariable)`, and the generic `FieldBuilder.Directive(name, arguments)`. Directives render after a field's name and arguments and before its selection set, space-separated in insertion order: `field(args) @include(if:$x) @skip(if:$y) { … }`. `Include`/`Skip` accept the variable with or without a leading `$` (normalized to `$name`); `Directive` accepts the name with or without a leading `@` (normalized, stored without). Directive arguments reuse the same value formatter as field arguments. Directive-less fields are unaffected — the backing store stays null, so there is zero added allocation. Public API additions: `FieldBuilder.Include`, `FieldBuilder.Skip`, `FieldBuilder.Directive`, `FieldDefinition.Directives`, `FieldDefinition.HasDirectives`, `Abstractions.FieldDirective`.
- `FieldDefinition.HasMetadata` — allocation-free check for metadata presence. Reading the `Metadata` getter materializes (and permanently attaches) an empty dictionary on metadata-less fields; use `HasMetadata` as the guard when scanning large field trees.
- **`AppendTo(StringBuilder)` and `WriteTo(TextWriter)`** on `QueryBuilder`, `Query`, and `Mutation` — render a query into an existing buffer or stream without the terminal `ToString()` string allocation. Output is byte-identical to `ToString()`; content is copied chunk-wise (`StringBuilder.Append(StringBuilder)` / `GetChunks()`), so no intermediate string is materialized. Reusing a `StringBuilder` across renders cuts allocation by ~40–65% versus `ToString()` per call.
- **`WriteUtf8(IBufferWriter<byte>)`** on `QueryBuilder`, `Query`, and `Mutation` — transcode the rendered GraphQL directly to UTF-8 into a caller-supplied buffer (e.g. `PipeWriter`, `ArrayBufferWriter<byte>`, a Kestrel response body) with no intermediate `string` and no intermediate `byte[]`. Bytes are identical to `Encoding.UTF8.GetBytes(query.ToString())` — including astral codepoints/emoji, which a stateful encoder reassembles correctly across chunk boundaries. Writing into a reused buffer cuts allocation ~51–71% versus the `ToString()` + `Encoding.UTF8.GetBytes` path.

### Changed
- **Breaking (minor):** `QueryDefinition.Fields` and `QueryDefinition.Metadata` now return `IReadOnlyDictionary<,>` instead of the live mutable `Dictionary<,>`, and the `Metadata` public setter has been removed. Consumers could previously `Clear()`/`Remove()`/insert into these dictionaries and desync the internal query map. Read access (indexer-get, `ContainsKey`, `TryGetValue`, `Count`, `Values`, enumeration) is unchanged; mutate via the `QueryBuilder`/`FieldBuilder` API. Mirrors the existing `NamedFragments` encapsulation.
- `QueryBuilder.Include` now throws `NotSupportedException` when the incoming query contains any fragments (named, inline, or spreads). Previously, fragments were silently dropped — emitting GraphQL with broken or missing references. The new guard surfaces the limitation at build time. To merge fragment-bearing queries, build the merged query without fragments or apply `Include` *before* adding fragments. Tracking issue for fragment-aware `Include` is a follow-up to #20.
- Performance: classic `Query`/`QueryBlock` rendering no longer materializes an intermediate string per nested block (up to 24× faster and 67× less allocation for deeply nested queries); assorted allocation cuts across the builder, render, and preservation hot paths (LINQ-free render/insert loops, comparer-based hashing, span-based alias parsing, pooled path building, pre-sized clone collections).

### Fixed
- `PreservationBuilder.Build()` projections are now fully isolated from the source builder. Previously the preserved query shared `FieldDefinition` subtrees (and intermediate argument dictionaries) with the source, so mutating either builder after `Build()` silently leaked into the other — including new fields appearing in already-issued role-based projections. Preserved trees are deep-cloned, keep inline fragments and fragment spreads, and carry over the named-fragment definitions they reference (transitively; unreferenced definitions are not emitted).

## [2.1.0] - 2026-05-04

### Added
- **Inline fragment support** via `FieldBuilder.OnType("TypeName", b => …)`. Renders as `... on TypeName { … }` after the field's plain children, sorted alphabetically by type name. Multiple `OnType` calls for the same type on the same parent merge into one combined fragment definition. Nested fragments (fragment-inside-fragment) work recursively. Solves union/interface narrowing for queries like GitHub's `search.nodes`. New `InlineFragmentDefinition` record exposes the data model. Public API additions: `FieldBuilder.OnType`, `FieldDefinition.InlineFragments`, `FieldDefinition.HasInlineFragments`, `Abstractions.InlineFragmentDefinition`. Named fragments (`fragment X on T { … }` + `...X` spreads) remain unsupported — tracked in [issue #20](https://github.com/dolifer/NGql/issues/20).
- `QueryBuilder.CreateMutationBuilder(name)` and `CreateMutationBuilder(name, MergingStrategy)` — the fluent builder now produces both queries and mutations from the same surface, removing the need to drop down to the classic `Mutation` type for new code.
- `QueryBuilder.AddField` gains `(field, args, lambda)` and `(field, subFields, lambda)` shorthand overloads, eliminating the `metadata: null` boilerplate previously required for these shapes.
- `FieldBuilder.AddField` gains args-first variants (`(field, args, subFields, …)`) so the parameter order matches `QueryBuilder.AddField` inside sub-field lambdas.
- `dotnet-ngql` global tool — companion CLI that compiles a `QueryBuilder` snippet against `NGql.Core` and prints the rendered GraphQL. Supports rendering from a file or stdin, plus `--execute --endpoint URL` to POST the operation to a live GraphQL server. Mutations are refused unless `--allow-mutations` is passed. Versioned in lockstep with `NGql.Core` and shipped from the same release.

### Changed
- `FieldBuilder.AddField(field, dict, lambda)` — the dictionary at this position is now interpreted as **arguments** (consistent with the rest of the args-first family). In 2.0.0 the same signature was interpreted as **metadata**. Callers that relied on the 2.0 semantic must switch to the four-arg form with the named `metadata:` argument: `b.AddField("user", arguments: null, metadata: dict, action)`. See the migration note inline in `FieldBuilder.cs`.

### Fixed
- `ValueFormatter.AppendString` now escapes embedded `"`, `\`, and control characters per the GraphQL spec (§ 2.9.4). Previously, a string argument like `"hello \"world\""` rendered as `"hello "world""` — invalid GraphQL that most servers rejected.

### Deprecated
- The classic `NGql.Core.Query` and `NGql.Core.Mutation` types are soft-deprecated via XML doc remarks. They continue to work and produce identical GraphQL; new code should prefer `QueryBuilder.CreateDefaultBuilder` and `QueryBuilder.CreateMutationBuilder`. There is no removal timeline.

## [2.0.0] - 2025-12-01

Major release. See [`docs/v2.0.0/RELEASE_NOTES.md`](docs/v2.0.0/RELEASE_NOTES.md) for the full write-up.

### Added
- `PreservationBuilder` — extract a subset of an existing `QueryBuilder` by string path (`Preserve("user.name", …)`) or by typed C# expression (`PreserveFromExpression<T>(x => x.user.email != null)`). Useful for role-based filtering, conditional fragments, and stripping fields from a shared query without rebuilding it.
- `MergingStrategy` enum — `MergeByDefault`, `MergeByFieldPath`, `NeverMerge`. Controls how `Include(otherBuilder)` handles duplicate paths.
- Multi-target support: `net8.0`, `net9.0`, `net10.0`.

### Changed
- Internal rendering and merging path was overhauled for performance: in-place merge in `Include()`, lock-free reads on `FieldChildren`, span-based path parsing, `ArrayPool<T>`-backed argument storage, two-level path-index cache. Query rendering is reflection-free; reflection is confined to the LINQ-expression preservation path.
- `Preserve(...)` moved off `QueryBuilder` onto a dedicated `PreservationBuilder` (call `PreservationBuilder.Create(builder)` to start).

### Removed
- Minimum runtime raised from .NET 6/7 to **.NET 8**.
- `QueryDefinitionExtensions` is now `internal` (was effectively unused outside the assembly).
- `PreserveExtensions` is internal — use `PreservationBuilder` instead.

[Unreleased]: https://github.com/dolifer/NGql/compare/2.1.0...HEAD
[2.1.0]: https://github.com/dolifer/NGql/compare/2.0.0...2.1.0
[2.0.0]: https://github.com/dolifer/NGql/compare/1.5.0...2.0.0
