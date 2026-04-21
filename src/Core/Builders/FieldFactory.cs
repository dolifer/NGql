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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition GetOrAddField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> type, SortedDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
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
    /// Gets or adds a dotted field (contains dots for nested access).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition GetOrAddDottedField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        if (fieldPath.IsEmpty)
        {
            throw new ArgumentException("Field path cannot be empty", nameof(fieldPath));
        }

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
    /// Processes dotted fields without arguments or metadata for optimal performance.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldFastPath(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType)
    {
        var currentFields = fieldDefinitions;
        FieldDefinition? result = null;
        var pathStart = 0;

        while (pathStart < fieldPath.Length)
        {
            ExtractDottedSegment(fieldPath, pathStart, out var spanSegment, out var nextStart);
            var segmentName = spanSegment.Name.ToString();

            if (!currentFields.TryGetValue(segmentName, out var field))
            {
                field = CreateDottedFieldSegment(spanSegment.Name, fieldPath, pathStart + spanSegment.Name.Length, spanSegment.IsLastFragment, fieldType);
                currentFields[segmentName] = field;
            }
            else if (!spanSegment.IsLastFragment && field.ShouldConvertToObjectType())
            {
                field = currentFields[segmentName] = field with { Type = Constants.ObjectFieldType };
            }

            result = field;
            currentFields = field.Fields;
            pathStart = nextStart;
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

    /// <summary>
    /// Processes dotted fields with arguments and metadata.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldWithMetadata(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var currentFields = fieldDefinitions;
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

            return ProcessDottedFieldSegments(currentFields, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
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

            return ProcessDottedFieldSegments(currentFields, fieldPath, fieldType, arguments, metadata, ref pathBuilder);
        }
    }

    /// <summary>
    /// Processes individual segments of a dotted field path.
    /// </summary>
    private static FieldDefinition ProcessDottedFieldSegments(Dictionary<string, FieldDefinition> currentFields, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ref SpanPathBuilder pathBuilder)
    {
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
            result = ProcessDottedSegment(currentFields, spanSegment.Name, spanSegment.IsLastFragment, fieldType, arguments, metadata, pathBuilder.AsSpan());
            currentFields = result.Fields;
            fieldPath = remainingPath;
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

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
    /// Processes a single dotted segment with arguments and metadata.
    /// </summary>
    private static FieldDefinition ProcessDottedSegment(Dictionary<string, FieldDefinition> currentFields, ReadOnlySpan<char> segment, bool isLastSegment, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ReadOnlySpan<char> segmentPath)
    {
        if (!currentFields.TryGetValue(segment, out var field))
        {
            var segmentArgs = isLastSegment ? arguments : null;
            var segmentType = isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;
            var segmentMetadata = isLastSegment ? metadata : null;
            
            field = Helpers.CreateFieldDefinition(segment, segmentType, ReadOnlySpan<char>.Empty, segmentArgs, segmentPath, segmentMetadata);
            currentFields.SetValue(segment, field);
            return field;
        }
        
        if (isLastSegment && arguments?.Count > 0 && field != null)
        {
            field = field.MergeFieldArguments(arguments);
            currentFields.SetValue(segment, field);
        }

        if (!isLastSegment && field?.ShouldConvertToObjectType() == true)
        {
            field = field with { Type = Constants.ObjectFieldType };
            currentFields.SetValue(segment, field);
        }

        return field ?? throw new InvalidOperationException("Field cannot be null");
    }

    /// <summary>
    /// Gets or adds a complex field with type parsing and alias handling.
    /// </summary>
    private static FieldDefinition GetOrAddComplexField(Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        // FAIL-FAST: Empty path check
        if (fieldPath.IsEmpty)
        {
            throw new ArgumentException("Field path cannot be empty", nameof(fieldPath));
        }

        var currentFields = fieldDefinitions;
        var parentPathSpan = parentPath.AsSpan();
        Span<char> pathBuffer = stackalloc char[512];
        var pathBuilder = new SpanPathBuilder(pathBuffer);

        if (!parentPathSpan.IsEmpty)
        {
            pathBuilder.Append(parentPathSpan);
        }

        fieldPath = Helpers.ParseFieldTypeFromPath(fieldPath, fieldType, out var parsedFieldType);

        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            var segment = ExtractNextSegment(ref fieldPath);

            // FAIL-FAST: Skip empty segment names immediately using span check
            if (segment.Name.IsWhiteSpace())
            {
                continue;
            }

            // Add segment to path builder
            pathBuilder.Append(segment.Name);

            var typeToUse = !segment.ParsedType.IsEmpty ? segment.ParsedType : parsedFieldType;

            result = ProcessFieldSegment(currentFields, segment, arguments, typeToUse, pathBuilder.AsSpan(), metadata);
            currentFields = result.Fields;
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

    /// <summary>
    /// Processes a field segment for complex field creation.
    /// </summary>
    private static FieldDefinition ProcessFieldSegment(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        if (!currentFields.TryGetValue(segment.Name.ToString(), out var field))
        {
            return CreateNewField(currentFields, segment, arguments, parsedFieldType, fullPath, metadata);
        }

        return UpdateExistingField(currentFields, segment, field, arguments, parsedFieldType);
    }

    /// <summary>
    /// Creates a new field for complex field processing.
    /// </summary>
    private static FieldDefinition CreateNewField(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        // Guard: field names should not contain spaces (type info should be consumed)
        if (segment.Name.Contains(' '))
        {
            throw new InvalidOperationException($"Field name '{segment.Name}' contains spaces. Type information should be consumed during parsing.");
        }

        var fieldArgs = segment.IsLastFragment ? arguments : null;
        var fieldMetadata = segment.IsLastFragment ? metadata : null;

        // Optimize path building for hot path - use span directly
        ReadOnlySpan<char> computedFieldTypeSpan;
        if (segment.IsLastFragment)
        {
            computedFieldTypeSpan = segment.HasParsedType ? segment.ParsedType : parsedFieldType;
        }
        else
        {
            computedFieldTypeSpan = segment.HasParsedType && segment.ParsedType.SequenceEqual(Constants.ArrayTypeMarkerSpan) ? Constants.ArrayTypeMarkerSpan : Constants.ObjectFieldTypeSpan;
        }

        var field = Helpers.CreateFieldDefinition(segment.Name, computedFieldTypeSpan, segment.Alias, fieldArgs, fullPath, fieldMetadata);
        currentFields[segment.Name.ToString()] = field;
        return field;
    }

    /// <summary>
    /// Updates an existing field during complex field processing.
    /// </summary>
    private static FieldDefinition UpdateExistingField(Dictionary<string, FieldDefinition> currentFields, SpanSegment segment, FieldDefinition field, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType)
    {
        if (segment.HasAlias && field._alias == null)
        {
            field._alias = segment.Alias.IsEmpty ? null : segment.Alias.ToString();
        }

        if (!segment.IsLastFragment && field.ShouldConvertToObjectType())
        {
            field._type = Constants.ObjectFieldType;
        }

        if (!segment.IsLastFragment)
        {
            return field;
        }

        if (arguments?.Count > 0)
        {
            var fieldKey = segment.Name.ToString();
            field = currentFields[fieldKey] = field.MergeFieldArguments(arguments);
        }

        if (!parsedFieldType.Equals(Constants.DefaultFieldTypeSpan, StringComparison.OrdinalIgnoreCase) &&
            !field._type.AsSpan().Equals(parsedFieldType, StringComparison.OrdinalIgnoreCase))
        {
            field._type = parsedFieldType.ToString();
        }

        return field;
    }

    /// <summary>
    /// Creates or merges a field definition into the target collection.
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

        var fieldType = string.IsNullOrWhiteSpace(fieldDefinition._type) ? Span<char>.Empty : fieldDefinition._type.AsSpan(); 
        var fieldAlias = string.IsNullOrEmpty(fieldDefinition._alias) ? Span<char>.Empty : fieldDefinition._alias.AsSpan();
        
        // Create new field
        var newField = Helpers.CreateFieldDefinition(
            fieldDefinition.Name.AsSpan(),
            fieldType,
            fieldAlias,
            fieldDefinition._arguments,
            fieldDefinition.Path.AsSpan(),
            fieldDefinition.Metadata);
        fields[fieldDefinition.Name] = newField;
        return newField;
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
        
        // Parse field name and alias from cleanedPath
        // Match original behavior: split on ':' and only handle exactly 2 non-empty parts
        var parts = cleanedPath.ToString().Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        ReadOnlySpan<char> name, alias;
        if (parts.Length == 2)
        {
            alias = parts[0].AsSpan();
            name = parts[1].AsSpan();
        }
        else
        {
            name = cleanedPath.Trim();
            alias = ReadOnlySpan<char>.Empty;
        }
        
        // Only include parsed type if it's not the default
        var typeToInclude = parsedType.SequenceEqual(Constants.DefaultFieldTypeSpan) ? ReadOnlySpan<char>.Empty : parsedType;
        var segment = new SpanSegment(name, alias, isLastFragment, typeToInclude);
        
        fieldPath = nextDot == -1 ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];

        return segment;
    }
}
