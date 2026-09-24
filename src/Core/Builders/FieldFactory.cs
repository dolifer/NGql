using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using NGql.Core.Features;
using NGql.Core.Pooling;

namespace NGql.Core.Builders;

/// <summary>
/// Factory class for creating and processing FieldDefinition instances.
/// Handles complex field creation logic, including dotted paths, type parsing, and field merging.
/// </summary>
internal static class FieldFactory
{
    /// <summary>
    /// Gets or adds a field to the collection, handling all field path complexities.
    /// This overload operates on the root-level <see cref="QueryDefinition.Fields"/> dictionary.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition GetOrAddField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> type, IDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
        => GetOrAddField(fieldDefinitions, fieldPath, null, type, arguments, parentPath, metadata);

    /// <summary>
    /// String overload of the root-level <c>GetOrAddField</c>: a simple field uses
    /// <paramref name="fieldPath"/> itself as its name, and a dotted leaf uses it as its path.
    /// </summary>
    internal static FieldDefinition GetOrAddField(Dictionary<string, FieldDefinition> fieldDefinitions, string fieldPath, ReadOnlySpan<char> type, IDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
        => GetOrAddField(fieldDefinitions, fieldPath.AsSpan(), fieldPath, type, arguments, parentPath, metadata);

    // fieldPathText, when present, is the string fieldPath spans; created fields reuse it.
    private static FieldDefinition GetOrAddField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, string? fieldPathText, ReadOnlySpan<char> type, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var fieldType = type.IsEmpty ? Constants.DefaultFieldTypeSpan : type;

        // FAST PATH: Simple field name
        if (fieldPath.IsSimpleField())
        {
            return fieldDefinitions.GetOrAddSimpleField(fieldPath, fieldType, arguments, parentPath, metadata, fieldPathText);
        }

        // MEDIUM PATH: Dotted field
        if (fieldPath.IsDottedField())
        {
            return GetOrAddDottedField(fieldDefinitions, fieldPath, fieldPathText, fieldType, arguments, parentPath, metadata);
        }

        // SLOW PATH: Complex field processing
        return GetOrAddComplexField(fieldDefinitions, fieldPath, fieldType, arguments, parentPath, metadata);
    }

    /// <summary>
    /// Gets or adds a field as a child of the given parent node, handling all field path complexities.
    /// This overload is for per-node child access (not root-level dictionary access).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition GetOrAddField(FieldDefinition parent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> type, IDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
        => GetOrAddField(parent, fieldPath, null, type, arguments, parentPath, metadata);

    /// <summary>
    /// String overload of the per-node <c>GetOrAddField</c>: a simple field uses
    /// <paramref name="fieldPath"/> itself as its name.
    /// </summary>
    internal static FieldDefinition GetOrAddField(FieldDefinition parent, string fieldPath, ReadOnlySpan<char> type, IDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
        => GetOrAddField(parent, fieldPath.AsSpan(), fieldPath, type, arguments, parentPath, metadata);

    private static FieldDefinition GetOrAddField(FieldDefinition parent, ReadOnlySpan<char> fieldPath, string? fieldPathText, ReadOnlySpan<char> type, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var fieldType = type.IsEmpty ? Constants.DefaultFieldTypeSpan : type;
        var children = parent._children ??= new FieldChildren();

        // parent's memoized deep fingerprint (if any) summarizes its entire subtree, including
        // whatever descendant fieldPath resolves to. Only an argument-bearing call can change the
        // merge-relevant subtree, so only those need to invalidate — but they must invalidate here,
        // at the single choke point every simple/dotted/complex descendant mutation funnels through,
        // since parent itself is never revisited by the per-segment walks below.
        if (arguments is { Count: > 0 })
        {
            parent.ClearMergeMemo();
        }

        // FAST PATH: Simple field name
        if (fieldPath.IsSimpleField())
        {
            return children.GetOrAddSimpleField(fieldPath, fieldType, arguments, parentPath, metadata, fieldPathText);
        }

        // MEDIUM PATH: Dotted field
        if (fieldPath.IsDottedField())
        {
            return GetOrAddDottedField(parent, fieldPath, fieldType, arguments, parentPath, metadata);
        }

        // SLOW PATH: Complex field processing
        return GetOrAddComplexField(parent, fieldPath, fieldType, arguments, parentPath, metadata);
    }

    /// <summary>
    /// Gets or adds a dotted field (contains dots for nested access) — root-level variant.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition GetOrAddDottedField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, string? fieldPathText, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var hasNoArguments = arguments == null;
        var hasNoMetadata = metadata == null;

        // FAST PATH: No arguments/metadata - use optimized processing
        if (hasNoArguments && hasNoMetadata)
        {
            return ProcessDottedFieldFastPath(fieldDefinitions, fieldPath, fieldPathText, fieldType);
        }

        // SLOW PATH: With arguments/metadata
        return ProcessDottedFieldWithMetadata(fieldDefinitions, fieldPath, fieldType, arguments, parentPath, metadata);
    }

    /// <summary>
    /// Gets or adds a dotted field (contains dots for nested access) — per-node variant.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition GetOrAddDottedField(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var hasNoArguments = arguments == null;
        var hasNoMetadata = metadata == null;

        if (hasNoArguments && hasNoMetadata)
        {
            return ProcessDottedFieldFastPath(rootParent, fieldPath, fieldType);
        }

        return ProcessDottedFieldWithMetadata(rootParent, fieldPath, fieldType, arguments, parentPath, metadata);
    }

    /// <summary>
    /// Processes dotted fields without arguments or metadata for optimal performance — root-level variant.
    /// The first segment uses the root dictionary; subsequent segments use <see cref="FieldDefinition._children"/>.
    /// </summary>
    // Callers reach here only via IsDottedField() which guarantees fieldPath contains '.',
    // so the loop runs at least twice and parentField is non-null on exit.
    private static FieldDefinition ProcessDottedFieldFastPath(Dictionary<string, FieldDefinition> rootFields, ReadOnlySpan<char> fieldPath, string? fieldPathText, ReadOnlySpan<char> fieldType)
    {
        FieldDefinition? parentField = null;
        var pathStart = 0;

        while (pathStart < fieldPath.Length)
        {
            ExtractDottedSegment(fieldPath, pathStart, out var spanSegment, out var nextStart);
            if (spanSegment.Name.IsWhiteSpace())
            {
                pathStart = nextStart;
                continue;
            }
            parentField = parentField is null
                ? GetOrCreateRootSegment(rootFields, spanSegment, fieldPath, pathStart, fieldType)
                : GetOrCreateChildSegment(parentField, spanSegment, fieldPath, fieldPathText, pathStart, fieldType);
            pathStart = nextStart;
        }

        // A dotted path made up entirely of empty/whitespace segments (e.g. "." or "..") yields
        // no field. Reject it rather than dereferencing a null result.
        return parentField ?? throw EmptyDottedPath();
    }

    /// <summary>
    /// Processes dotted fields without arguments or metadata — per-node variant.
    /// All segments use <see cref="FieldDefinition._children"/>.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldFastPath(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType)
    {
        var currentParent = rootParent;
        var pathStart = 0;

        while (pathStart < fieldPath.Length)
        {
            ExtractDottedSegment(fieldPath, pathStart, out var spanSegment, out var nextStart);
            if (spanSegment.Name.IsWhiteSpace())
            {
                pathStart = nextStart;
                continue;
            }
            currentParent = GetOrCreateChildSegment(currentParent, spanSegment, fieldPath, null, pathStart, fieldType);
            pathStart = nextStart;
        }

        return currentParent;
    }

    private static FieldDefinition GetOrCreateRootSegment(Dictionary<string, FieldDefinition> rootFields, SpanSegment spanSegment, ReadOnlySpan<char> fieldPath, int pathStart, ReadOnlySpan<char> fieldType)
    {
        if (!TryGetRootFieldBySpan(rootFields, spanSegment.Name, out var field, out var segmentName))
        {
            // The key string doubles as the name and, unless empty segments lead the path, the path.
            var segmentPath = pathStart == 0 ? segmentName : fieldPath[..(pathStart + segmentName.Length)].ToString();
            field = Helpers.CreateFieldDefinition(segmentName, SegmentType(spanSegment.IsLastFragment, fieldType), null, null, segmentPath);
            rootFields[segmentName] = field;
            return field;
        }
        PromoteToObjectIfNeeded(field, spanSegment.IsLastFragment);
        return field;
    }

    /// <summary>
    /// Root counterpart of <see cref="FieldChildren.FindSegmentTarget"/>. Root fields are keyed by
    /// name; a second field with the same name but a different response key is keyed by its alias.
    /// Returns the field the segment addresses, or null with <paramref name="newKey"/> set to the key
    /// a new field belongs under (<paramref name="nameText"/>, when given, is reused as that key).
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Rare fallback scan returning the first match; a plain loop allocates no enumerator.")]
    internal static FieldDefinition? FindRootSegmentTarget(Dictionary<string, FieldDefinition> fields, ReadOnlySpan<char> name, ReadOnlySpan<char> alias, bool isLastSegment, string? nameText, out string newKey)
    {
        newKey = null!;
        if (!alias.IsEmpty)
        {
            // Keys compare case-insensitively, so the alias key can hit an unaliased field whose name
            // merely matches the alias; only a field that carries the alias is this segment's target.
            if (TryGetRootFieldBySpan(fields, alias, out var byAlias, out var aliasKey)
                && alias.Equals(byAlias._alias.AsSpan(), StringComparison.OrdinalIgnoreCase)
                && name.Equals(byAlias.Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return byAlias;
            }

            if (!TryGetRootFieldBySpan(fields, name, out var byName, out var nameKey))
            {
                newKey = nameText ?? nameKey;
                return null;
            }

            if (alias.Equals(byName._alias.AsSpan(), StringComparison.OrdinalIgnoreCase)) return byName;
            newKey = byAlias is null ? aliasKey : NextFreeKey(fields, alias.ToString());
            return null;
        }

        if (!TryGetRootField(fields, name, ref nameText, out var field))
        {
            newKey = nameText!;
            return null;
        }

        var isSameName = name.Equals(field.Name.AsSpan(), StringComparison.OrdinalIgnoreCase);
        if (!isLastSegment || (isSameName && string.IsNullOrEmpty(field._alias))) return field;

        // The last segment asks for the unaliased field, but its name key holds another field: an
        // aliased one of the same name moves to its alias key (Dictionary reuses the freed entry, so
        // enumeration order is unchanged); a different field aliased to this name keeps its key.
        if (!isSameName)
        {
            // The name key belongs to a field aliased to this name, so a plain field with this name
            // lives under a numbered key if it exists at all.
            foreach (var candidate in fields.Values)
            {
                if (string.IsNullOrEmpty(candidate._alias) && name.Equals(candidate.Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    return candidate;
            }

            newKey = NextFreeKey(fields, nameText ?? name.ToString());
            return null;
        }

        newKey = nameText ?? name.ToString();
        fields.Remove(newKey);
        fields[fields.ContainsKey(field._alias!) ? NextFreeKey(fields, field._alias!) : field._alias!] = field;
        return null;
    }

    // With the caller's string in hand, probe with it: a span probe materializes a key on a miss.
    // On a miss nameText holds the key to store a new field under.
    private static bool TryGetRootField(Dictionary<string, FieldDefinition> fields, ReadOnlySpan<char> name, ref string? nameText,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FieldDefinition? field)
    {
        if (nameText is not null) return fields.TryGetValue(nameText, out field);

        var found = TryGetRootFieldBySpan(fields, name, out field, out var key);
        nameText = key;
        return found;
    }

    /// <summary>
    /// The key <paramref name="field"/> is stored under: normally its name; for a second field with
    /// the same name, found by reference (its alias key, or a numbered one).
    /// </summary>
    internal static string RootKeyOf(Dictionary<string, FieldDefinition> fields, FieldDefinition field)
        => ReferenceEquals(fields.GetValueOrDefault(field.Name), field) ? field.Name : KeyByReference(fields, field);

    // Separate so the lambda's closure is allocated only on this rare path, not on every call.
    private static string KeyByReference(Dictionary<string, FieldDefinition> fields, FieldDefinition field)
        => fields.First(pair => ReferenceEquals(pair.Value, field)).Key;

    /// <summary>
    /// Probes <paramref name="rootFields"/> (keyed <see cref="StringComparer.OrdinalIgnoreCase"/>) by
    /// span without allocating on .NET 9+, via
    /// <c>Dictionary&lt;TKey,TValue&gt;.GetAlternateLookup&lt;ReadOnlySpan&lt;char&gt;&gt;()</c>. On a
    /// miss (or on net8.0, where the alternate-lookup API does not exist), materializes
    /// <paramref name="name"/> exactly once and hands it back via <paramref name="key"/> so the
    /// caller's subsequent write on the not-found path reuses it instead of calling
    /// <see cref="ReadOnlySpan{T}.ToString"/> again.
    /// </summary>
    private static bool TryGetRootFieldBySpan(Dictionary<string, FieldDefinition> rootFields, ReadOnlySpan<char> name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FieldDefinition? field, out string key)
    {
#if NET9_0_OR_GREATER
        if (rootFields.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out field))
        {
            key = null!;
            return true;
        }
        key = name.ToString();
        return false;
#else
        key = name.ToString();
        return rootFields.TryGetValue(key, out field);
#endif
    }

    private static FieldDefinition GetOrCreateChildSegment(FieldDefinition parentField, SpanSegment spanSegment, ReadOnlySpan<char> fieldPath, string? fieldPathText, int pathStart, ReadOnlySpan<char> fieldType)
    {
        var children = parentField._children ??= new FieldChildren();
        var field = children.FindSegmentTarget(spanSegment.Name, ReadOnlySpan<char>.Empty, spanSegment.IsLastFragment);
        if (field is null)
        {
            field = CreateDottedFieldSegment(spanSegment.Name, fieldPath, fieldPathText, pathStart + spanSegment.Name.Length, spanSegment.IsLastFragment, fieldType);
            children.Append(field);
            return field;
        }
        PromoteToObjectIfNeeded(field, spanSegment.IsLastFragment);
        return field;
    }

    private static void PromoteToObjectIfNeeded(FieldDefinition field, bool isLastFragment)
    {
        if (!isLastFragment && field.ShouldConvertToObjectType())
        {
            field._type = Constants.ObjectFieldType;
        }
    }

    /// <summary>
    /// Processes dotted fields with arguments and metadata — root-level variant.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldWithMetadata(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var parentPathSpan = parentPath.AsSpan();
        
        // Use stack allocation for small paths, pooled resources for larger ones
        var estimatedPathLength = parentPathSpan.Length + fieldPath.Length + 10; // Extra space for dots
        
        if (estimatedPathLength <= 512)
        {
            Span<char> pathBuffer = stackalloc char[512];
            var pathBuilder = new SpanPathBuilder(pathBuffer);

            if (!parentPathSpan.IsEmpty)
            {
                pathBuilder.Append(parentPathSpan);
            }

            return ProcessDottedFieldSegments(fieldDefinitions, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
        else
        {
            // Use pooled resources for very long paths
            using var pooledArray = CharArrayPool.GetPooled(estimatedPathLength);
            var pathBuilder = new SpanPathBuilder(pooledArray.AsSpan());

            if (!parentPathSpan.IsEmpty)
            {
                pathBuilder.Append(parentPathSpan);
            }

            return ProcessDottedFieldSegments(fieldDefinitions, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
    }

    /// <summary>
    /// Processes dotted fields with arguments and metadata — per-node variant.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldWithMetadata(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var parentPathSpan = parentPath.AsSpan();
        var estimatedPathLength = parentPathSpan.Length + fieldPath.Length + 10;

        if (estimatedPathLength <= 512)
        {
            Span<char> pathBuffer = stackalloc char[512];
            var pathBuilder = new SpanPathBuilder(pathBuffer);
            if (!parentPathSpan.IsEmpty) pathBuilder.Append(parentPathSpan);
            return ProcessDottedFieldSegments(rootParent, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
        else
        {
            using var pooledArray = CharArrayPool.GetPooled(estimatedPathLength);
            var pathBuilder = new SpanPathBuilder(pooledArray.AsSpan());
            if (!parentPathSpan.IsEmpty) pathBuilder.Append(parentPathSpan);
            return ProcessDottedFieldSegments(rootParent, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
    }

    /// <summary>
    /// Processes individual segments of a dotted field path — root-level variant.
    /// </summary>
    // Callers reach here only via IsDottedField() which guarantees fieldPath contains '.', so the
    // loop runs at least once and result is non-null on exit.
    private static FieldDefinition ProcessDottedFieldSegments(Dictionary<string, FieldDefinition> rootFields, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ref SpanPathBuilder pathBuilder)
    {
        FieldDefinition? parentField = null;
        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            ExtractDottedSegmentWithPath(fieldPath, out var spanSegment, out var remainingPath);

            if (spanSegment.Name.IsWhiteSpace())
            {
                fieldPath = remainingPath;
                continue;
            }

            pathBuilder.Append(spanSegment.Name);

            if (parentField == null)
            {
                // Root level — use the root Dictionary.
                result = ProcessDottedSegment(rootFields, spanSegment.Name, spanSegment.IsLastFragment, fieldType, arguments, metadata, pathBuilder.AsSpan());
            }
            else
            {
                // Nested level — use FieldChildren. parentField is an ancestor of whatever this
                // segment (or a later one) mutates, so its memoized fingerprint must not survive
                // an argument-bearing walk past it.
                if (arguments is { Count: > 0 })
                {
                    parentField.ClearMergeMemo();
                }
                var children = parentField._children ??= new FieldChildren();
                result = ProcessDottedSegment(children, spanSegment.Name, spanSegment.IsLastFragment, fieldType, arguments, metadata, pathBuilder.AsSpan());
            }

            parentField = result;
            fieldPath = remainingPath;
        }

        return result ?? throw EmptyDottedPath();
    }

    /// <summary>
    /// Processes individual segments of a dotted field path — per-node variant.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldSegments(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ref SpanPathBuilder pathBuilder)
    {
        var currentParent = rootParent;
        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            ExtractDottedSegmentWithPath(fieldPath, out var spanSegment, out var remainingPath);

            if (spanSegment.Name.IsWhiteSpace())
            {
                fieldPath = remainingPath;
                continue;
            }

            pathBuilder.Append(spanSegment.Name);
            // currentParent is an ancestor of whatever this segment (or a later one) mutates —
            // see the root-level variant above for the full invalidation rationale.
            if (arguments is { Count: > 0 })
            {
                currentParent.ClearMergeMemo();
            }
            var children = currentParent._children ??= new FieldChildren();
            result = ProcessDottedSegment(children, spanSegment.Name, spanSegment.IsLastFragment, fieldType, arguments, metadata, pathBuilder.AsSpan());
            currentParent = result;
            fieldPath = remainingPath;
        }

        // Matches the per-node fast-path variant: an all-empty dotted path leaves the parent
        // unchanged rather than throwing.
        return result ?? rootParent;
    }

    /// <summary>
    /// Creates an exception for a dotted path that contains no non-empty segments.
    /// Mirrors the null/empty guard used at the public AddField boundary.
    /// </summary>
    private static ArgumentException EmptyDottedPath()
        => new("Field cannot be null or empty");

    /// <summary>
    /// Creates a field segment for dotted field processing.
    /// </summary>
    private static FieldDefinition CreateDottedFieldSegment(ReadOnlySpan<char> segment, ReadOnlySpan<char> fullPath, string? fullPathText, int segmentEnd, bool isLastSegment, ReadOnlySpan<char> fieldType)
    {
        // A segment that runs to the end of the caller's string has that string as its path.
        var segmentPath = fullPathText is not null && segmentEnd == fullPathText.Length
            ? fullPathText
            : fullPath[..segmentEnd].ToString();

        return Helpers.CreateFieldDefinition(segment.ToString(), SegmentType(isLastSegment, fieldType), null, null, segmentPath);
    }

    private static ReadOnlySpan<char> SegmentType(bool isLastSegment, ReadOnlySpan<char> fieldType)
        => isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;

    /// <summary>
    /// Processes a single dotted segment with arguments and metadata — root-Dict variant.
    /// </summary>
    private static FieldDefinition ProcessDottedSegment(Dictionary<string, FieldDefinition> currentFields, ReadOnlySpan<char> segment, bool isLastSegment, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ReadOnlySpan<char> segmentPath)
    {
        if (!currentFields.TryGetValue(segment, out var field))
        {
            field = CreateDottedSegmentField(segment, isLastSegment, fieldType, arguments, metadata, segmentPath);
            currentFields.SetValue(segment, field);
            return field;
        }

        // This Dictionary variant only processes the FIRST segment of dotted paths, so isLastSegment
        // is necessarily false (single-segment paths route through GetOrAddSimpleField instead).
        var existing = field!;
        if (existing.ShouldConvertToObjectType())
        {
            existing = existing with { Type = Constants.ObjectFieldType };
            currentFields.SetValue(segment, existing);
        }
        return existing;
    }

    /// <summary>
    /// Processes a single dotted segment with arguments and metadata — FieldChildren variant.
    /// </summary>
    private static FieldDefinition ProcessDottedSegment(FieldChildren children, ReadOnlySpan<char> segment, bool isLastSegment, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ReadOnlySpan<char> segmentPath)
    {
        var field = children.FindSegmentTarget(segment, ReadOnlySpan<char>.Empty, isLastSegment);
        if (field is null)
        {
            field = CreateDottedSegmentField(segment, isLastSegment, fieldType, arguments, metadata, segmentPath);
            children.Append(field);
            return field;
        }

        if (!isLastSegment) return PromoteIntermediateChildToObject(field);
        // FieldBuilder normalizes empty argument dictionaries to null upstream, so a
        // non-null `arguments` here always has Count > 0.
        return arguments is null ? field : MergeArgumentsIntoExistingChild(children, field, arguments);
    }

    // ProcessDottedFieldFastPath handles the args-null case before reaching here, and
    // FieldBuilder.Create normalizes empty argument dictionaries to null upstream — so by
    // the time we get here arguments is always a non-null dictionary with Count > 0.
    private static FieldDefinition MergeArgumentsIntoExistingChild(FieldChildren children, FieldDefinition existing, IDictionary<string, object?> arguments)
    {
        var merged = existing.MergeFieldArguments(arguments);
        // By reference: existing was found by response key and may share its name with a sibling.
        children.ReplaceReference(existing, merged);
        return merged;
    }

    private static FieldDefinition PromoteIntermediateChildToObject(FieldDefinition existing)
    {
        if (existing.ShouldConvertToObjectType())
        {
            existing._type = Constants.ObjectFieldType;
        }
        return existing;
    }

    private static FieldDefinition CreateDottedSegmentField(ReadOnlySpan<char> segment, bool isLastSegment, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ReadOnlySpan<char> segmentPath)
    {
        var segmentArgs = isLastSegment ? arguments : null;
        var segmentType = isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;
        var segmentMetadata = isLastSegment ? metadata : null;
        return Helpers.CreateFieldDefinition(segment, segmentType, ReadOnlySpan<char>.Empty, segmentArgs, segmentPath, segmentMetadata);
    }

    /// <summary>
    /// Gets or adds a complex field with type parsing and alias handling — root-level variant.
    /// The Dictionary variant is the entry point from QueryBuilder.AddField; callers never pass a
    /// non-empty parentPath (that's used only by the per-node FieldChildren overload below). The
    /// parameter exists to share a signature with the FieldChildren variant via the public dispatch.
    /// </summary>
    // AddFieldCore rejects null/whitespace fieldPath at the public-API boundary, so by the time
    // we reach here at least one non-whitespace segment exists and result is non-null on exit.
#pragma warning disable S1172 // parentPath unused — kept for signature symmetry with the FieldChildren variant.
    private static FieldDefinition GetOrAddComplexField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
#pragma warning restore S1172
    {
        // Type/alias parsing only ever shrinks the accumulated path, so fieldPath length plus slack
        // for separators is a safe upper bound. Mirror the dotted path: stackalloc for the common
        // short case, pooled fallback for long paths so we never overflow SpanPathBuilder.
        var estimatedPathLength = fieldPath.Length + 10;

        if (estimatedPathLength <= 512)
        {
            Span<char> pathBuffer = stackalloc char[512];
            var pathBuilder = new SpanPathBuilder(pathBuffer);
            return ProcessComplexFieldSegments(fieldDefinitions, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
        else
        {
            using var pooledArray = CharArrayPool.GetPooled(estimatedPathLength);
            var pathBuilder = new SpanPathBuilder(pooledArray.AsSpan());
            return ProcessComplexFieldSegments(fieldDefinitions, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
    }

    /// <summary>
    /// Gets or adds a complex field with type parsing and alias handling — per-node variant.
    /// </summary>
    private static FieldDefinition GetOrAddComplexField(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var parentPathSpan = parentPath.AsSpan();

        // Type/alias parsing only shrinks the segment path, so parent + fieldPath + slack is a safe
        // upper bound. Mirror the dotted path: stackalloc for short paths, pooled fallback for long
        // ones so we never overflow SpanPathBuilder.
        var estimatedPathLength = parentPathSpan.Length + fieldPath.Length + 10;

        if (estimatedPathLength <= 512)
        {
            Span<char> pathBuffer = stackalloc char[512];
            var pathBuilder = new SpanPathBuilder(pathBuffer);
            if (!parentPathSpan.IsEmpty) pathBuilder.Append(parentPathSpan);
            return ProcessComplexFieldSegments(rootParent, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
        else
        {
            using var pooledArray = CharArrayPool.GetPooled(estimatedPathLength);
            var pathBuilder = new SpanPathBuilder(pooledArray.AsSpan());
            if (!parentPathSpan.IsEmpty) pathBuilder.Append(parentPathSpan);
            return ProcessComplexFieldSegments(rootParent, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
    }

    private static FieldDefinition ProcessComplexFieldSegments(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ref SpanPathBuilder pathBuilder)
    {
        fieldPath = Helpers.ParseFieldTypeFromPath(fieldPath, fieldType, out var parsedFieldType);

        FieldDefinition? parentField = null;
        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            ExtractNextSegment(fieldPath, out var segment, out var remaining);

            if (!segment.Name.IsWhiteSpace())
            {
                pathBuilder.Append(segment.Name);
                var typeToUse = !segment.ParsedType.IsEmpty ? segment.ParsedType : parsedFieldType;

                if (parentField == null)
                {
                    // Root level
                    result = ProcessFieldSegment(fieldDefinitions, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
                }
                else
                {
                    // Nested level. parentField is an ancestor of whatever this segment (or a
                    // later one) mutates — only the last fragment ever carries arguments, but
                    // every field visited on the way there must lose its memoized fingerprint.
                    if (arguments is { Count: > 0 })
                    {
                        parentField.ClearMergeMemo();
                    }
                    var children = parentField._children ??= new FieldChildren();
                    result = ProcessFieldSegment(children, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
                }

                parentField = result;
            }

            fieldPath = remaining;
        }

        return result!;
    }

    private static FieldDefinition ProcessComplexFieldSegments(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ref SpanPathBuilder pathBuilder)
    {
        fieldPath = Helpers.ParseFieldTypeFromPath(fieldPath, fieldType, out var parsedFieldType);

        var currentParent = rootParent;
        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            ExtractNextSegment(fieldPath, out var segment, out var remaining);

            if (!segment.Name.IsWhiteSpace())
            {
                pathBuilder.Append(segment.Name);
                var typeToUse = !segment.ParsedType.IsEmpty ? segment.ParsedType : parsedFieldType;
                // currentParent is an ancestor of whatever this segment (or a later one) mutates —
                // see the root-Dict variant above for the full invalidation rationale.
                if (arguments is { Count: > 0 })
                {
                    currentParent.ClearMergeMemo();
                }
                var children = currentParent._children ??= new FieldChildren();
                result = ProcessFieldSegment(children, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
                currentParent = result;
            }

            fieldPath = remaining;
        }

        return result!;
    }

    /// <summary>
    /// Processes a field segment for complex field creation — root-Dict variant.
    /// </summary>
    private static FieldDefinition ProcessFieldSegment(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        var field = FindRootSegmentTarget(currentFields, segment.Name, segment.Alias, segment.IsLastFragment, null, out var key);
        return field is null
            ? CreateNewField(currentFields, key, segment, arguments, parsedFieldType, fullPath, metadata)
            : UpdateExistingField(currentFields, segment, field, arguments, parsedFieldType);
    }

    /// <summary>
    /// Processes a field segment for complex field creation — FieldChildren variant.
    /// </summary>
    private static FieldDefinition ProcessFieldSegment(FieldChildren children, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        var field = FindExistingSegmentChild(children, segment);
        if (field is null)
        {
            return CreateNewField(children, segment, arguments, parsedFieldType, fullPath, metadata);
        }

        return UpdateExistingField(children, segment, field, arguments, parsedFieldType);
    }

    /// <summary>
    /// Finds a child matching <paramref name="segment"/>. For the LAST fragment of a complex path,
    /// identity is (Name, alias) — not Name alone — because two children may legitimately share a
    /// Name while differing by alias (distinct response keys), e.g. adding <c>"aliasA:id"</c> then
    /// <c>"aliasB:id"</c> under the same parent must produce two siblings, not collapse the second
    /// into the first. Intermediate (non-last) segments route purely by Name, exactly like dotted
    /// paths: a path addresses an intermediate node structurally by name, and an alias set on it
    /// (e.g. <c>"alias:profile.displayName:name"</c>) is rendering metadata, not routing identity —
    /// a LATER reference to the same node via its bare name (e.g. <c>"profile.userEmail:email"</c>)
    /// must still resolve to that one node, never fork a duplicate.
    /// </summary>
    private static FieldDefinition? FindExistingSegmentChild(FieldChildren children, SpanSegment segment)
        => children.FindSegmentTarget(segment.Name, segment.Alias, segment.IsLastFragment);

    /// <summary>
    /// Creates a new field for complex field processing — root-Dict variant. <paramref name="segmentKey"/>
    /// is the already-materialized dictionary key for <c>segment.Name</c> (computed once by the
    /// caller's failed probe via <see cref="TryGetRootFieldBySpan"/>), reused here for the insert
    /// instead of calling <see cref="ReadOnlySpan{T}.ToString"/> a second time.
    /// </summary>
    private static FieldDefinition CreateNewField(Dictionary<string, FieldDefinition> currentFields, string segmentKey, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        var (fieldArgs, fieldMetadata) = ResolveSegmentArgsAndMetadata(segment, arguments, metadata);
        var fieldType = ResolveSegmentFieldType(segment, parsedFieldType);
        var field = Helpers.CreateFieldDefinition(segment.Name, fieldType, segment.Alias, fieldArgs, fullPath, fieldMetadata);
        currentFields[segmentKey] = field;
        return field;
    }

    /// <summary>
    /// Creates a new field for complex field processing — FieldChildren variant.
    /// </summary>
    private static FieldDefinition CreateNewField(FieldChildren children, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        var (fieldArgs, fieldMetadata) = ResolveSegmentArgsAndMetadata(segment, arguments, metadata);
        var fieldType = ResolveSegmentFieldType(segment, parsedFieldType);
        var field = Helpers.CreateFieldDefinition(segment.Name, fieldType, segment.Alias, fieldArgs, fullPath, fieldMetadata);
        children.Append(field);
        return field;
    }

    private static (IDictionary<string, object?>? args, Dictionary<string, object?>? meta) ResolveSegmentArgsAndMetadata(SpanSegment segment, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => segment.IsLastFragment ? (arguments, metadata) : (null, null);

    private static ReadOnlySpan<char> ResolveSegmentFieldType(SpanSegment segment, ReadOnlySpan<char> parsedFieldType)
    {
        if (segment.IsLastFragment) return segment.HasParsedType ? segment.ParsedType : parsedFieldType;
        return segment.HasParsedType && segment.ParsedType.SequenceEqual(Constants.ArrayTypeMarkerSpan)
            ? Constants.ArrayTypeMarkerSpan
            : Constants.ObjectFieldTypeSpan;
    }

    /// <summary>
    /// Updates an existing field during complex field processing — root-Dict variant.
    /// </summary>
    private static FieldDefinition UpdateExistingField(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, FieldDefinition field, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType)
    {
        if (!ApplyIntermediateUpdates(segment, field)) return field;

        if (arguments?.Count > 0)
        {
            // field was found via a case-insensitive lookup keyed by this exact string (set at
            // insertion time in CreateNewField), so reusing it here — rather than re-materializing
            // segment.Name — both avoids a second allocation and targets the correct existing slot.
            var fieldKey = RootKeyOf(currentFields, field);
            field = currentFields[fieldKey] = field.MergeFieldArguments(arguments);
        }
        ApplyParsedFieldType(field, parsedFieldType);
        return field;
    }

    private static FieldDefinition UpdateExistingField(FieldChildren children, SpanSegment segment, FieldDefinition field, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType)
    {
        if (!ApplyIntermediateUpdates(segment, field)) return field;

        if (arguments?.Count > 0)
        {
            var mergedField = field.MergeFieldArguments(arguments);
            // Replace by REFERENCE: field was matched by (Name, alias) in FindExistingSegmentChild,
            // and a name-keyed Set could instead overwrite a different same-named/different-alias sibling.
            children.ReplaceReference(field, mergedField);
            field = mergedField;
        }
        ApplyParsedFieldType(field, parsedFieldType);
        return field;
    }

    // Mutates intermediate-segment metadata (alias, object-promotion). Returns true when the
    // segment is the last fragment and the caller should continue with last-fragment updates.
    private static bool ApplyIntermediateUpdates(SpanSegment segment, FieldDefinition field)
    {
        if (!segment.IsLastFragment && field.ShouldConvertToObjectType())
        {
            field._type = Constants.ObjectFieldType;
        }
        return segment.IsLastFragment;
    }

    private static void ApplyParsedFieldType(FieldDefinition field, ReadOnlySpan<char> parsedFieldType)
    {
        if (!parsedFieldType.Equals(Constants.DefaultFieldTypeSpan, StringComparison.OrdinalIgnoreCase)
            && !field._type.AsSpan().Equals(parsedFieldType, StringComparison.OrdinalIgnoreCase))
        {
            field._type = parsedFieldType.ToString();
        }
    }

    /// <summary>
    /// Merges <paramref name="fieldDefinition"/> into the root fields (the default merging strategy).
    /// It merges into an existing field when the two can merge
    /// (<see cref="FieldDefinitionExtensions.CanMergeByDefault"/>), preferring one with the same
    /// alias; at the root a differently aliased field of the same name also qualifies, because
    /// fragments tag their root with their query name and <c>GetPathTo</c> maps the merge. Otherwise a copy is added under its name key, or its alias key, or an auto-aliased key
    /// (<c>users_1</c>) when both are taken, so no fragment receives another fragment's argument
    /// values. <paramref name="key"/> is where the field ended up.
    /// </summary>
    internal static FieldDefinition CreateOrMergeField(Dictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition, out string key)
    {
        var target = FindMergeTarget(fields, fieldDefinition, sameAliasOnly: true, out key)
            ?? FindMergeTarget(fields, fieldDefinition, sameAliasOnly: false, out key);
        if (target is not null)
        {
            var merged = target.MergeFieldArguments(fieldDefinition._arguments);
            if (!ReferenceEquals(merged, target)) fields[key] = merged;
            return merged;
        }

        var newField = CloneFieldDefinitionForMerge(fieldDefinition);
        key = newField.Name;
        if (fields.ContainsKey(key))
        {
            key = !string.IsNullOrEmpty(newField._alias) && !IsResponseKeyTaken(fields, newField._alias)
                ? newField._alias
                : NextFreeKey(fields, newField._effectiveName);
            newField._alias = key;
        }

        fields[key] = newField;
        return newField;
    }

    /// <summary>
    /// Nested counterpart of <see cref="CreateOrMergeField(Dictionary{string, FieldDefinition}, FieldDefinition, out string)"/>.
    /// Below the root, siblings with the same name and different aliases are distinct response
    /// keys, so only a field with the same alias is a merge target.
    /// </summary>
    internal static FieldDefinition CreateOrMergeField(FieldChildren children, FieldDefinition fieldDefinition)
    {
        var target = FindMergeTarget(children.AsSpan(), fieldDefinition);
        if (target is not null)
        {
            var merged = target.MergeFieldArguments(fieldDefinition._arguments);
            // By reference: a same-named sibling with another alias must stay untouched.
            if (!ReferenceEquals(merged, target)) children.ReplaceReference(target, merged);
            return merged;
        }

        var newField = CloneFieldDefinitionForMerge(fieldDefinition);
        if (children.FindByResponseKey(newField._effectiveName) is not null)
        {
            newField._alias = KeyGenerator.GenerateUniqueKey(newField._effectiveName, children.AsSpan());
        }

        children.Append(newField);
        return newField;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Returns the first match with its key; a plain loop allocates no enumerator.")]
    private static FieldDefinition? FindMergeTarget(Dictionary<string, FieldDefinition> fields, FieldDefinition incoming, bool sameAliasOnly, out string key)
    {
        foreach (var (existingKey, existing) in fields)
        {
            if ((!sameAliasOnly || HasSameAlias(existing, incoming)) && FieldDefinitionExtensions.CanMergeByDefault(existing, incoming))
            {
                key = existingKey;
                return existing;
            }
        }

        key = null!;
        return null;
    }

    private static FieldDefinition? FindMergeTarget(ReadOnlySpan<FieldDefinition> children, FieldDefinition incoming)
    {
        foreach (var existing in children)
        {
            if (HasSameAlias(existing, incoming) && FieldDefinitionExtensions.CanMergeByDefault(existing, incoming))
                return existing;
        }
        return null;
    }

    private static bool HasSameAlias(FieldDefinition existing, FieldDefinition incoming)
        => string.Equals(existing._alias ?? string.Empty, incoming._alias ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    // A key or response key no root field uses yet: baseKey_1, baseKey_2, …
    private static string NextFreeKey(Dictionary<string, FieldDefinition> fields, string baseKey)
    {
        var suffix = 1;
        while (IsResponseKeyTaken(fields, $"{baseKey}_{suffix}")) suffix++;
        return $"{baseKey}_{suffix}";
    }

    // Root aliased fields are keyed by name, so a free key does not prove the response key is free.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Runs only when a merge needs a new key; a plain loop allocates no enumerator.")]
    private static bool IsResponseKeyTaken(Dictionary<string, FieldDefinition> fields, string responseKey)
    {
        if (fields.ContainsKey(responseKey)) return true;
        foreach (var field in fields.Values)
        {
            if (string.Equals(field._effectiveName, responseKey, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static FieldDefinition CloneFieldDefinitionForMerge(FieldDefinition fieldDefinition)
    {
        var clone = Helpers.CreateFieldDefinition(
            fieldDefinition.Name,
            fieldDefinition._type.AsSpan(),
            fieldDefinition._alias,
            fieldDefinition._arguments,
            fieldDefinition.Path,
            fieldDefinition._metadata);
        return clone;
    }

    private static void ExtractDottedSegment(ReadOnlySpan<char> fieldPath, int pathStart, out SpanSegment segment, out int nextStart)
    {
        var dotIndex = fieldPath.Slice(pathStart).IndexOf('.');
        var isLastSegment = dotIndex == -1;
        var segmentEnd = isLastSegment ? fieldPath.Length : pathStart + dotIndex;
        var segmentSpan = fieldPath.Slice(pathStart, segmentEnd - pathStart);
        nextStart = isLastSegment ? fieldPath.Length : segmentEnd + 1;
        
        segment = new SpanSegment(segmentSpan, ReadOnlySpan<char>.Empty, isLastSegment, ReadOnlySpan<char>.Empty);
    }

    private static void ExtractDottedSegmentWithPath(ReadOnlySpan<char> fieldPath, out SpanSegment segment, out ReadOnlySpan<char> remainingPath)
    {
        var dotIndex = fieldPath.IndexOf('.');
        var isLastSegment = dotIndex == -1;
        var segmentSpan = isLastSegment ? fieldPath : fieldPath[..dotIndex];
        remainingPath = isLastSegment ? ReadOnlySpan<char>.Empty : fieldPath[(dotIndex + 1)..];
        
        segment = new SpanSegment(segmentSpan, ReadOnlySpan<char>.Empty, isLastSegment, ReadOnlySpan<char>.Empty);
    }

    private static void ExtractNextSegment(ReadOnlySpan<char> fieldPath, out SpanSegment segment, out ReadOnlySpan<char> remaining)
    {
        var nextDot = fieldPath.IndexOf('.');
        var isLastFragment = nextDot == -1;
        var currentPart = isLastFragment ? fieldPath : fieldPath[..nextDot];
        var trimmedPart = currentPart.Trim();

        // Parse and remove type information from the segment
        var cleanedPath = Helpers.ParseFieldTypeFromPath(trimmedPart, Constants.DefaultFieldType, out var parsedType);

        // Parse field name and alias from cleanedPath: only exactly 2 non-empty trimmed
        // colon-separated parts mean alias:name. Span-based equivalent of
        // Split(':', TrimEntries | RemoveEmptyEntries) without the string/array allocations.
        ParseAliasAndName(cleanedPath, out var name, out var alias, out var malformed);

        // Only include parsed type if it's not the default
        var typeToInclude = parsedType.SequenceEqual(Constants.DefaultFieldTypeSpan) ? ReadOnlySpan<char>.Empty : parsedType;

        // Reject a colon that produced an empty alias/name part (e.g. "a : b" → ": b", or
        // "alias:"): folding the stray colon back into the field name emits invalid GraphQL such
        // as a field literally named ": b". Throw a clear error instead.
        if (malformed)
        {
            throw MalformedComplexSegment(trimmedPart);
        }

        segment = new SpanSegment(name, alias, isLastFragment, typeToInclude);
        remaining = isLastFragment ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];
    }

    private static ArgumentException MalformedComplexSegment(ReadOnlySpan<char> segment)
        => new($"Malformed field segment '{segment.ToString()}'. Use 'alias:name' (no spaces around ':') and put any type before a single space, e.g. 'Type alias:name'.");

    /// <summary>
    /// Splits <paramref name="cleanedPath"/> on ':' into trimmed non-empty parts. Exactly two
    /// parts mean <c>alias:name</c>; a colon-free segment is the name with no alias. A colon that
    /// yields an empty alias or name part (e.g. <c>": name"</c> or <c>"alias:"</c>) sets
    /// <paramref name="malformed"/> so the caller can reject it rather than folding the stray
    /// colon back into a field name and emitting invalid GraphQL. Three or more parts still fall
    /// back to treating the whole segment as the name (unchanged from prior behavior).
    /// </summary>
    private static void ParseAliasAndName(ReadOnlySpan<char> cleanedPath, out ReadOnlySpan<char> name, out ReadOnlySpan<char> alias, out bool malformed)
    {
        malformed = false;

        if (cleanedPath.IndexOf(':') < 0)
        {
            name = cleanedPath.Trim();
            alias = ReadOnlySpan<char>.Empty;
            return;
        }

        ReadOnlySpan<char> first = default, second = default;
        var partCount = 0;
        var emptyPartSeen = false;
        var rest = cleanedPath;
        while (true)
        {
            var colonIndex = rest.IndexOf(':');
            var part = (colonIndex < 0 ? rest : rest[..colonIndex]).Trim();
            if (part.IsEmpty)
            {
                emptyPartSeen = true;
            }
            else
            {
                if (partCount == 0) first = part;
                else if (partCount == 1) second = part;
                partCount++;
                if (partCount > 2) break;
            }
            if (colonIndex < 0) break;
            rest = rest[(colonIndex + 1)..];
        }

        if (partCount == 2)
        {
            alias = first;
            name = second;
            return;
        }

        // A colon that produced an empty alias/name part (e.g. ": name") is malformed: surface it
        // instead of folding the stray colon back into the name. Other shapes (three or more
        // parts) keep the historical whole-segment-as-name fallback.
        name = cleanedPath.Trim();
        alias = ReadOnlySpan<char>.Empty;
        malformed = emptyPartSeen && partCount < 2;
    }
}
