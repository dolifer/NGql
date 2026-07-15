using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
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
    {
        var fieldType = type.IsEmpty ? Constants.DefaultFieldTypeSpan : type;

        // FAST PATH: Simple field name
        if (fieldPath.IsSimpleField())
        {
            return fieldDefinitions.GetOrAddSimpleField(fieldPath, fieldType, arguments, parentPath, metadata);
        }

        // MEDIUM PATH: Dotted field
        if (fieldPath.IsDottedField())
        {
            return GetOrAddDottedField(fieldDefinitions, fieldPath, fieldType, arguments, parentPath, metadata);
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
    {
        var fieldType = type.IsEmpty ? Constants.DefaultFieldTypeSpan : type;
        var children = parent._children ??= new FieldChildren();

        // FAST PATH: Simple field name
        if (fieldPath.IsSimpleField())
        {
            return children.GetOrAddSimpleField(fieldPath, fieldType, arguments, parentPath, metadata);
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
    private static FieldDefinition GetOrAddDottedField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var hasNoArguments = arguments == null;
        var hasNoMetadata = metadata == null;

        // FAST PATH: No arguments/metadata - use optimized processing
        if (hasNoArguments && hasNoMetadata)
        {
            return ProcessDottedFieldFastPath(fieldDefinitions, fieldPath, fieldType);
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
    private static FieldDefinition ProcessDottedFieldFastPath(Dictionary<string, FieldDefinition> rootFields, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType)
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
                : GetOrCreateChildSegment(parentField, spanSegment, fieldPath, pathStart, fieldType);
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
            currentParent = GetOrCreateChildSegment(currentParent, spanSegment, fieldPath, pathStart, fieldType);
            pathStart = nextStart;
        }

        return currentParent;
    }

    private static FieldDefinition GetOrCreateRootSegment(Dictionary<string, FieldDefinition> rootFields, SpanSegment spanSegment, ReadOnlySpan<char> fieldPath, int pathStart, ReadOnlySpan<char> fieldType)
    {
        var segmentName = spanSegment.Name.ToString();
        if (!rootFields.TryGetValue(segmentName, out var field))
        {
            field = CreateDottedFieldSegment(spanSegment.Name, fieldPath, pathStart + spanSegment.Name.Length, spanSegment.IsLastFragment, fieldType);
            rootFields[segmentName] = field;
            return field;
        }
        PromoteToObjectIfNeeded(field, spanSegment.IsLastFragment);
        return field;
    }

    private static FieldDefinition GetOrCreateChildSegment(FieldDefinition parentField, SpanSegment spanSegment, ReadOnlySpan<char> fieldPath, int pathStart, ReadOnlySpan<char> fieldType)
    {
        var children = parentField._children ??= new FieldChildren();
        if (!children.TryGetValue(spanSegment.Name, out var field) || field is null)
        {
            field = CreateDottedFieldSegment(spanSegment.Name, fieldPath, pathStart + spanSegment.Name.Length, spanSegment.IsLastFragment, fieldType);
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
                // Nested level — use FieldChildren.
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
        => new("Field cannot be null or empty", "fieldName");

    /// <summary>
    /// Creates a field segment for dotted field processing.
    /// </summary>
    private static FieldDefinition CreateDottedFieldSegment(ReadOnlySpan<char> segment, ReadOnlySpan<char> fullPath, int segmentEnd, bool isLastSegment, ReadOnlySpan<char> fieldType)
    {
        var segmentType = isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;
        var segmentPath = fullPath.Slice(0, segmentEnd);
        
        return Helpers.CreateFieldDefinition(segment, segmentType, ReadOnlySpan<char>.Empty, null, segmentPath, null);
    }

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
        if (!children.TryGetValue(segment, out var field))
        {
            field = CreateDottedSegmentField(segment, isLastSegment, fieldType, arguments, metadata, segmentPath);
            children.Append(field);
            return field;
        }

        if (!isLastSegment) return PromoteIntermediateChildToObject(field!);
        // FieldBuilder normalizes empty argument dictionaries to null upstream, so a
        // non-null `arguments` here always has Count > 0.
        return arguments is null ? field! : MergeArgumentsIntoExistingChild(children, segment, field!, arguments);
    }

    // ProcessDottedFieldFastPath handles the args-null case before reaching here, and
    // FieldBuilder.Create normalizes empty argument dictionaries to null upstream — so by
    // the time we get here arguments is always a non-null dictionary with Count > 0.
    private static FieldDefinition MergeArgumentsIntoExistingChild(FieldChildren children, ReadOnlySpan<char> segment, FieldDefinition existing, IDictionary<string, object?> arguments)
    {
        var merged = existing.MergeFieldArguments(arguments);
        children.Set(segment, merged);
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
        Span<char> pathBuffer = stackalloc char[512];
        var pathBuilder = new SpanPathBuilder(pathBuffer);

        fieldPath = Helpers.ParseFieldTypeFromPath(fieldPath, fieldType, out var parsedFieldType);

        FieldDefinition? parentField = null;
        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            var segment = ExtractNextSegment(ref fieldPath);

            if (segment.Name.IsWhiteSpace())
                continue;

            pathBuilder.Append(segment.Name);
            var typeToUse = !segment.ParsedType.IsEmpty ? segment.ParsedType : parsedFieldType;

            if (parentField == null)
            {
                // Root level
                result = ProcessFieldSegment(fieldDefinitions, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
            }
            else
            {
                // Nested level
                var children = parentField._children ??= new FieldChildren();
                result = ProcessFieldSegment(children, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
            }

            parentField = result;
        }

        return result!;
    }

    /// <summary>
    /// Gets or adds a complex field with type parsing and alias handling — per-node variant.
    /// </summary>
    private static FieldDefinition GetOrAddComplexField(FieldDefinition rootParent, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var parentPathSpan = parentPath.AsSpan();
        Span<char> pathBuffer = stackalloc char[512];
        var pathBuilder = new SpanPathBuilder(pathBuffer);

        if (!parentPathSpan.IsEmpty) pathBuilder.Append(parentPathSpan);

        fieldPath = Helpers.ParseFieldTypeFromPath(fieldPath, fieldType, out var parsedFieldType);

        var currentParent = rootParent;
        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            var segment = ExtractNextSegment(ref fieldPath);

            if (segment.Name.IsWhiteSpace())
                continue;

            pathBuilder.Append(segment.Name);
            var typeToUse = !segment.ParsedType.IsEmpty ? segment.ParsedType : parsedFieldType;
            var children = currentParent._children ??= new FieldChildren();
            result = ProcessFieldSegment(children, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
            currentParent = result;
        }

        return result!;
    }

    /// <summary>
    /// Processes a field segment for complex field creation — root-Dict variant.
    /// </summary>
    private static FieldDefinition ProcessFieldSegment(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        if (!currentFields.TryGetValue(segment.Name.ToString(), out var field))
        {
            return CreateNewField(currentFields, segment, arguments, parsedFieldType, fullPath, metadata);
        }

        return UpdateExistingField(currentFields, segment, field, arguments, parsedFieldType);
    }

    /// <summary>
    /// Processes a field segment for complex field creation — FieldChildren variant.
    /// </summary>
    private static FieldDefinition ProcessFieldSegment(FieldChildren children, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        if (!children.TryGetValue(segment.Name, out var field))
        {
            return CreateNewField(children, segment, arguments, parsedFieldType, fullPath, metadata);
        }

        return UpdateExistingField(children, segment, field, arguments, parsedFieldType);
    }

    /// <summary>
    /// Creates a new field for complex field processing — root-Dict variant.
    /// </summary>
    private static FieldDefinition CreateNewField(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, IDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        var (fieldArgs, fieldMetadata) = ResolveSegmentArgsAndMetadata(segment, arguments, metadata);
        var fieldType = ResolveSegmentFieldType(segment, parsedFieldType);
        var field = Helpers.CreateFieldDefinition(segment.Name, fieldType, segment.Alias, fieldArgs, fullPath, fieldMetadata);
        currentFields[segment.Name.ToString()] = field;
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
            var fieldKey = segment.Name.ToString();
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
            field = field.MergeFieldArguments(arguments);
            children.Set(segment.Name, field);
        }
        ApplyParsedFieldType(field, parsedFieldType);
        return field;
    }

    // Mutates intermediate-segment metadata (alias, object-promotion). Returns true when the
    // segment is the last fragment and the caller should continue with last-fragment updates.
    private static bool ApplyIntermediateUpdates(SpanSegment segment, FieldDefinition field)
    {
        // segment.HasAlias <=> !Alias.IsEmpty by SpanSegment's invariant.
        if (segment.HasAlias && field._alias is null)
        {
            field._alias = segment.Alias.ToString();
        }
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
    /// Creates or merges a field definition into the target collection — root-Dict variant.
    /// </summary>
    internal static FieldDefinition CreateOrMergeField(Dictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        // Try to find existing field to merge with
        var existingField = Helpers.FindExistingField(fields, fieldDefinition);
        if (existingField != null)
        {
            var mergedField = existingField.MergeFieldArguments(fieldDefinition._arguments);
            fields[existingField.Name] = mergedField;
            return mergedField;
        }

        var newField = CloneFieldDefinitionForMerge(fieldDefinition);
        fields[fieldDefinition.Name] = newField;
        return newField;
    }

    /// <summary>
    /// Creates or merges a field definition into the target collection — FieldChildren variant.
    /// </summary>
    internal static FieldDefinition CreateOrMergeField(FieldChildren children, FieldDefinition fieldDefinition)
    {
        var existingField = Helpers.FindExistingField(children, fieldDefinition);
        if (existingField != null)
        {
            var mergedField = existingField.MergeFieldArguments(fieldDefinition._arguments);
            children.Set(existingField.Name, mergedField);
            return mergedField;
        }

        var newField = CloneFieldDefinitionForMerge(fieldDefinition);
        children.Append(newField);
        return newField;
    }

    // FieldDefinition._type is always set to a non-empty value by all constructors (defaulting
    // to Constants.DefaultFieldType), so the type-empty branch was a dead defensive check.
    private static FieldDefinition CloneFieldDefinitionForMerge(FieldDefinition fieldDefinition)
    {
        var fieldAlias = string.IsNullOrEmpty(fieldDefinition._alias) ? Span<char>.Empty : fieldDefinition._alias.AsSpan();
        return Helpers.CreateFieldDefinition(
            fieldDefinition.Name.AsSpan(),
            fieldDefinition._type.AsSpan(),
            fieldAlias,
            fieldDefinition._arguments,
            fieldDefinition.Path.AsSpan(),
            fieldDefinition.Metadata);
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

    private static SpanSegment ExtractNextSegment(ref ReadOnlySpan<char> fieldPath)
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
        ParseAliasAndName(cleanedPath, out var name, out var alias);
        
        // Only include parsed type if it's not the default
        var typeToInclude = parsedType.SequenceEqual(Constants.DefaultFieldTypeSpan) ? ReadOnlySpan<char>.Empty : parsedType;
        var segment = new SpanSegment(name, alias, isLastFragment, typeToInclude);
        
        fieldPath = nextDot == -1 ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];

        return segment;
    }

    /// <summary>
    /// Splits <paramref name="cleanedPath"/> on ':' into trimmed non-empty parts. Exactly two
    /// parts mean <c>alias:name</c>; any other count (no colon, empty parts only, or three or
    /// more parts) falls back to treating the whole segment as the name with no alias.
    /// </summary>
    private static void ParseAliasAndName(ReadOnlySpan<char> cleanedPath, out ReadOnlySpan<char> name, out ReadOnlySpan<char> alias)
    {
        if (cleanedPath.IndexOf(':') < 0)
        {
            name = cleanedPath.Trim();
            alias = ReadOnlySpan<char>.Empty;
            return;
        }

        ReadOnlySpan<char> first = default, second = default;
        var partCount = 0;
        var rest = cleanedPath;
        while (true)
        {
            var colonIndex = rest.IndexOf(':');
            var part = (colonIndex < 0 ? rest : rest[..colonIndex]).Trim();
            if (!part.IsEmpty)
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
        }
        else
        {
            name = cleanedPath.Trim();
            alias = ReadOnlySpan<char>.Empty;
        }
    }
}
