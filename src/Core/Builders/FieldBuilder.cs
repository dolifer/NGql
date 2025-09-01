using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Builders;

public sealed class FieldBuilder
{
    private FieldDefinition _fieldDefinition;

    private FieldBuilder(FieldDefinition fieldDefinition)
    {
        _fieldDefinition = fieldDefinition;
    }

    /// <summary>
    /// Adds a field definition to the builder.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to add.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldDefinition is null.</exception>
    public FieldBuilder AddField(FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);
        var arguments = fieldDefinition._arguments ?? null;

        // FAST PATH: Check if field name is simple
        var fieldName = fieldDefinition.Name;
        var isSimpleFieldName = fieldName.AsSpan().IsSimpleField();
        if (isSimpleFieldName)
        {
            GetOrAddSimpleField(_fieldDefinition.Fields, fieldName, fieldDefinition.Type, arguments, _fieldDefinition.Path, fieldDefinition.Metadata);
        }
        else
        {
            GetOrAddField(_fieldDefinition.Fields, fieldName, fieldDefinition.Type, arguments, _fieldDefinition.Path, fieldDefinition.Metadata);
        }
        return this;
    }

    /// <summary>
    /// Adds a field with optional type to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field (defaults to String).</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type = Constants.DefaultFieldType)
        => AddFieldCore(fieldName, type, null, null, null, null);

    /// <summary>
    /// Adds a field with type and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, type, null, null, metadata, null);

    /// <summary>
    /// Adds a field with subfields, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, arguments, metadata, null);

    /// <summary>
    /// Adds a field with type, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, type, null, arguments, metadata, null);

    /// <summary>
    /// Adds a field with type, subfields, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, type, subFields, null, metadata, null);

    /// <summary>
    /// Adds a field with arguments to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, arguments, null, null);

    /// <summary>
    /// Adds a field with arguments and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, arguments, metadata, null);

    /// <summary>
    /// Adds a field with type, subfields, and arguments to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, SortedDictionary<string, object?>? arguments)
        => AddFieldCore(fieldName, type, subFields, arguments, null, null);

    /// <summary>
    /// Adds a field with type, subfields, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, type, subFields, arguments, metadata, null);

    /// <summary>
    /// Adds a field with a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, null, null, action);

    /// <summary>
    /// Adds a field with metadata and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, null, metadata, action);

    /// <summary>
    /// Adds a field with type and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, null, null, action);

    /// <summary>
    /// Adds a field with type, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, null, metadata, action);

    /// <summary>
    /// Adds a field with arguments and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, arguments, null, action);

    /// <summary>
    /// Adds a field with arguments, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, arguments, metadata, action);

    /// <summary>
    /// Adds a field with type, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, arguments, null, action);

    /// <summary>
    /// Adds a field with type, arguments, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, arguments, metadata, action);

    /// <summary>
    /// Adds a field with subfields and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, null, null, action);

    /// <summary>
    /// Adds a field with subfields, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, null, metadata, action);

    /// <summary>
    /// Adds a field with type, subfields, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields, null, null, action);

    /// <summary>
    /// Adds a field with type, subfields, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields, null, metadata, action);

    /// <summary>
    /// Adds a field with subfields, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, arguments, null, action);

    /// <summary>
    /// Adds a field with subfields, arguments, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, arguments, metadata, action);

    /// <summary>
    /// Adds a field with type, subfields, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields, arguments, null, action);

    /// <summary>
    /// Adds a field with type, subfields, arguments, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields, arguments, metadata, action);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FieldBuilder AddFieldCore(string fieldName, string type, string[]? subFields, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder>? action)
    {
        var field = GetOrAddField(_fieldDefinition.Fields, fieldName, type.AsSpan(), arguments, _fieldDefinition.Path, metadata);

        // FAIL-FAST: Skip subFields processing if array is null or empty
        if (subFields?.Length > 0)
        {
            foreach (var subField in subFields)
            {
                // Use full field processing to handle dotted paths, types, aliases, etc.
                GetOrAddField(field.Fields, subField, Constants.DefaultFieldTypeSpan, null, field.Path);
            }
        }

        if (action == null)
        {
            return this;
        }

        var fieldBuilder = new FieldBuilder(field);
        action(fieldBuilder);

        _fieldDefinition.Fields[field.Name] = fieldBuilder._fieldDefinition;

        return this;
    }

    /// <summary>
    /// Creates a new FieldBuilder instance for the specified field.
    /// </summary>
    /// <param name="fieldDefinitions">The collection of existing field definitions.</param>
    /// <param name="fieldName">The name of the field to create.</param>
    /// <returns>A new FieldBuilder instance.</returns>
    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName)
    {
        return Create(fieldDefinitions, fieldName, Constants.DefaultFieldType);
    }

    /// <summary>
    /// Creates a new FieldBuilder instance for the specified field with type, arguments, and metadata.
    /// </summary>
    /// <param name="fieldDefinitions">The collection of existing field definitions.</param>
    /// <param name="fieldName">The name of the field to create.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>A new FieldBuilder instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type, SortedDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
    {
        // FAIL-FAST: Use empty arguments if null or empty
        var argumentsToUse = arguments is { Count: > 0 } ? arguments : null;

        // FAST PATH: Check if field name is simple
        FieldDefinition rootField;
        var isSimpleFieldName = fieldName.AsSpan().IsSimpleField();
        if (isSimpleFieldName)
        {
            rootField = GetOrAddSimpleField(fieldDefinitions, fieldName, type, argumentsToUse, null, metadata);
        }
        else
        {
            rootField = GetOrAddField(fieldDefinitions, fieldName, type.AsSpan(), argumentsToUse, null, metadata);
        }

        var fieldBuilder = new FieldBuilder(rootField);

        return fieldBuilder;
    }

    /// <summary>
    /// Creates a new FieldBuilder instance from an existing field definition.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to create the builder from.</param>
    /// <returns>A new FieldBuilder instance.</returns>
    public static FieldBuilder Create(FieldDefinition fieldDefinition) => new(fieldDefinition);

    /// <summary>
    /// Builds and returns the final field definition.
    /// </summary>
    /// <returns>The constructed FieldDefinition.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no field definition has been set.</exception>
    public FieldDefinition Build()
        => _fieldDefinition ?? throw new InvalidOperationException("Field definition is not set. Use AddField or CreateField methods to define fields.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition GetOrAddField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> type, SortedDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
    {
        var fieldType = type.IsEmpty ? Constants.DefaultFieldTypeSpan : type;

        // FAST PATH: Simple field name
        if (fieldPath.IsSimpleField())
        {
            return fieldDefinitions.GetOrAddSimpleField(fieldPath, fieldType.ToString(), arguments, parentPath, metadata);
        }

        // MEDIUM PATH: Dotted field
        if (fieldPath.IsDottedField())
        {
            return GetOrAddDottedField(fieldDefinitions, fieldPath, fieldType, arguments, parentPath, metadata);
        }

        // SLOW PATH: Complex field processing
        return GetOrAddComplexField(fieldDefinitions, fieldPath, fieldType, arguments, parentPath, metadata);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition GetOrAddSimpleField(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        if (fieldDefinitions.TryGetValue(fieldName, out var existingField))
        {
            // FAIL-FAST: Skip argument merging if no arguments to merge
            if (arguments?.Count > 0)
            {
                existingField = existingField.MergeFieldArguments(arguments);
                fieldDefinitions[fieldName] = existingField;
            }
            return existingField;
        }

        // Create new simple field with simple path building
        var path = string.IsNullOrEmpty(parentPath) ? fieldName : $"{parentPath}.{fieldName}";
        var field = Helpers.CreateFieldDefinition(fieldName.AsSpan(), fieldType.AsSpan(), ReadOnlySpan<char>.Empty, arguments, path.AsSpan(), metadata);
        fieldDefinitions[fieldName] = field;
        return field;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition GetOrAddDottedField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        // FAIL-FAST: Empty path check
        if (fieldPath.IsEmpty)
        {
            throw new ArgumentException("Field path cannot be empty", nameof(fieldPath));
        }

        var hasNoArguments = arguments == null;
        var hasNoMetadata = metadata == null;

        // FAST PATH: No arguments/metadata - use optimized span-based processing
        if (hasNoArguments && hasNoMetadata)
        {
            var fastCurrentFields = fieldDefinitions;
            FieldDefinition? fastResult = null;
            var pathStart = 0;

            while (pathStart < fieldPath.Length)
            {
                var dotIndex = fieldPath.Slice(pathStart).IndexOf('.');
                var isLastSegment = dotIndex == -1;
                var segmentEnd = isLastSegment ? fieldPath.Length : pathStart + dotIndex;
                var segment = fieldPath.Slice(pathStart, segmentEnd - pathStart);
                var segmentName = segment.ToString();

                if (!fastCurrentFields.TryGetValue(segmentName, out var field))
                {
                    // IMPORTANT: Intermediate segments must be object type, only last segment uses fieldType
                    var segmentType = isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;
                    var segmentPath = fieldPath.Slice(0, segmentEnd);
                    
                    field = Helpers.CreateFieldDefinition(segment, segmentType, ReadOnlySpan<char>.Empty, null, segmentPath, null);
                    fastCurrentFields[segmentName] = field;
                }
                else if (!isLastSegment && field.ShouldConvertToObjectType())
                {
                    // Convert existing field to object type if it has subfields
                    field = fastCurrentFields[segmentName] = field with { Type = Constants.ObjectFieldType };
                }

                fastResult = field;
                fastCurrentFields = field.Fields;
                pathStart = isLastSegment ? fieldPath.Length : segmentEnd + 1;
            }

            return fastResult ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
        }

        var currentFields = fieldDefinitions;
        var parentPathSpan = parentPath.AsSpan();
        Span<char> pathBuffer = stackalloc char[512]; // Stack allocation for path building
        var pathBuilder = new SpanPathBuilder(pathBuffer);

        if (!parentPathSpan.IsEmpty)
        {
            pathBuilder.Append(parentPathSpan);
        }

        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            var dotIndex = fieldPath.IndexOf('.');
            var isLastSegment = dotIndex == -1;
            var segment = isLastSegment ? fieldPath : fieldPath[..dotIndex];

            // FAIL-FAST: Skip empty segments immediately using span check
            if (segment.IsWhiteSpace())
            {
                fieldPath = isLastSegment ? ReadOnlySpan<char>.Empty : fieldPath[(dotIndex + 1)..];
                continue;
            }

            // Add segment to path builder
            pathBuilder.Append(segment);

            if (!currentFields.TryGetValue(segment, out var field))
            {
                var segmentArgs = isLastSegment ? arguments : null;
                var segmentType = isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;
                var segmentMetadata = isLastSegment ? metadata : null;
                // Optimize path building for hot path - use span directly
                field = Helpers.CreateFieldDefinition(segment, segmentType, ReadOnlySpan<char>.Empty, segmentArgs, pathBuilder.AsSpan(), segmentMetadata);
                currentFields.SetValue(segment, field);
            }
            else
            {
                // FAIL-FAST: Skip argument merging if no arguments to merge
                if (isLastSegment && arguments?.Count > 0)
                {
                    field = field.MergeFieldArguments(arguments);
                    currentFields.SetValue(segment, field);
                }

                // FAIL-FAST: Skip type conversion check if not needed
                if (!isLastSegment && field.ShouldConvertToObjectType())
                {
                    field = field with { Type = Constants.ObjectFieldType };
                    currentFields.SetValue(segment, field);
                }
            }

            result = field;
            currentFields = field.Fields;
            fieldPath = isLastSegment ? ReadOnlySpan<char>.Empty : fieldPath[(dotIndex + 1)..];
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

    private static FieldDefinition GetOrAddComplexField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
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

    private static FieldDefinition ProcessFieldSegment(SortedDictionary<string, FieldDefinition> currentFields, SpanSegment segment, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        if (!currentFields.TryGetValue(segment.Name.ToString(), out var field))
        {
            return CreateNewField(currentFields, segment, arguments, parsedFieldType, fullPath, metadata);
        }

        return UpdateExistingField(currentFields, segment, field, arguments, parsedFieldType);
    }

    private static FieldDefinition CreateNewField(SortedDictionary<string, FieldDefinition> currentFields, SpanSegment segment, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType, ReadOnlySpan<char> fullPath, Dictionary<string, object?>? metadata)
    {
        // Guard: field names should not contain spaces (type info should be consumed)
        if (segment.Name.Contains(' '))
        {
            throw new InvalidOperationException($"Field name '{segment.Name}' contains spaces. Type information should be consumed during parsing.");
        }

        var fieldArgs = segment.IsLastFragment ? arguments : null;
        var intermediateType = segment.HasParsedType && segment.ParsedType.SequenceEqual(Constants.ArrayTypeMarkerSpan) ? Constants.ArrayTypeMarker : Constants.ObjectFieldType;
        var computedFieldType = segment.IsLastFragment ? (segment.HasParsedType ? segment.ParsedType.ToString() : parsedFieldType.ToString()) : intermediateType;
        var fieldMetadata = segment.IsLastFragment ? metadata : null;

        // Optimize path building for hot path - use span directly
        var field = Helpers.CreateFieldDefinition(segment.Name, computedFieldType.AsSpan(), segment.Alias, fieldArgs, fullPath, fieldMetadata);
        currentFields[segment.Name.ToString()] = field;
        return field;
    }

    private static FieldDefinition UpdateExistingField(SortedDictionary<string, FieldDefinition> currentFields, SpanSegment segment, FieldDefinition field, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType)
    {
        if (segment.HasAlias && field.Alias == null)
        {
            field = currentFields[segment.Name.ToString()] = field with { Alias = segment.Alias.ToString() };
        }

        if (!segment.IsLastFragment && field.ShouldConvertToObjectType())
        {
            field = currentFields[segment.Name.ToString()] = field with { Type = Constants.ObjectFieldType };
        }

        if (segment.IsLastFragment)
        {
            if (arguments?.Count > 0)
            {
                field = currentFields[segment.Name.ToString()] = field.MergeFieldArguments(arguments);
            }

            if (!parsedFieldType.Equals(Constants.DefaultFieldTypeSpan, StringComparison.OrdinalIgnoreCase) &&
                !field.Type.AsSpan().Equals(parsedFieldType, StringComparison.OrdinalIgnoreCase))
            {
                field = currentFields[segment.Name.ToString()] = field with { Type = parsedFieldType.ToString() };
            }
        }

        return field;
    }

    /// <summary>
    /// Recursively creates and merges field definitions into the target field collection.
    /// This method handles field merging, argument consolidation, and nested field creation.
    /// </summary>
    /// <param name="fields">Target field collection</param>
    /// <param name="fieldDefinition">Field definition to create/merge</param>
    private static void RecursiveCreateField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        var parentField = CreateOrMergeField(fields, fieldDefinition);

        // Recursively process all child fields
        foreach (var childFieldDefinition in fieldDefinition.Fields.Values)
        {
            RecursiveCreateField(parentField.Fields, childFieldDefinition);
        }
    }

    private static FieldDefinition CreateOrMergeField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        var arguments = fieldDefinition._arguments ?? null;

        // Try to find existing field to merge with
        var existingField = Helpers.FindExistingField(fields, fieldDefinition);
        if (existingField != null)
        {
            var mergedField = existingField.MergeFieldArguments(arguments);
            fields[existingField.Name] = mergedField;
            return mergedField;
        }

        // Create new field
        var newField = Helpers.CreateFieldDefinition(
            fieldDefinition.Name.AsSpan(),
            (fieldDefinition.Type ?? Constants.DefaultFieldType),
            (fieldDefinition.Alias ?? "").AsSpan(),
            arguments,
            fieldDefinition.Path.AsSpan(),
            fieldDefinition.Metadata);
        fields[fieldDefinition.Name] = newField;
        return newField;
    }

    internal static void Include(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
        => RecursiveCreateField(fields, fieldDefinition);

    /// <summary>
    /// Sets an alias for the current field.
    /// </summary>
    /// <param name="alias">The alias to set for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder WithAlias(string alias)
    {
        _fieldDefinition = _fieldDefinition with { Alias = alias };

        return this;
    }

    /// <summary>
    /// Sets the type for the current field.
    /// </summary>
    /// <param name="type">The type to set for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder WithType(string type)
    {
        _fieldDefinition = _fieldDefinition with { Type = type };

        return this;
    }

    /// <summary>
    /// Sets or merges metadata for the current field.
    /// </summary>
    /// <param name="metadata">The metadata to set or merge with existing metadata.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder WithMetadata(Dictionary<string, object> metadata)
    {
        var mergedMetadata = Helpers.MergeMetadata(_fieldDefinition._metadata, metadata);

        _fieldDefinition.Metadata = mergedMetadata;

        return this;
    }

    /// <summary>
    /// Adds or updates an argument for the current field.
    /// </summary>
    /// <param name="key">The argument key.</param>
    /// <param name="value">The argument value.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder Where(string key, object? value)
    {
        if (_fieldDefinition._arguments != null && _fieldDefinition._arguments.TryGetValue(key, out var existingValue))
        {
            if (existingValue is IDictionary<string, object> existingDict && value is IDictionary<string, object> newDict)
            {
                // Merge nested dictionaries
                _fieldDefinition._arguments[key] = Helpers.MergeDictionaries(existingDict, newDict);
            }
            else
            {
                // Override the existing value with the new one
                _fieldDefinition._arguments[key] = value;
            }
        }
        else
        {
            // Lazy create Arguments dictionary only when needed
            if (_fieldDefinition._arguments is null)
            {
                _fieldDefinition = _fieldDefinition with { Arguments = new() { [key] = value } };
            }
            else
            {
                _fieldDefinition._arguments[key] = value;
            }
        }

        return this;
    }

}
