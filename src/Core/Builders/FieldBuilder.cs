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
            GetOrAddSimpleField(_fieldDefinition.Fields, fieldName, fieldDefinition._type, arguments, _fieldDefinition.Path, fieldDefinition.Metadata);
        }
        else
        {
            GetOrAddField(_fieldDefinition.Fields, fieldName, fieldDefinition._type, arguments, _fieldDefinition.Path, fieldDefinition.Metadata);
        }
        return this;
    }

    /// <summary>
    /// Applies a field configuration to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="configuration">The field configuration to apply.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal FieldBuilder ApplyFieldConfiguration(string fieldName, FieldConfiguration configuration)
    {
        var field = GetOrAddField(_fieldDefinition.Fields, fieldName, (configuration.Type ?? Constants.DefaultFieldType).AsSpan(), configuration.Arguments, _fieldDefinition.Path, configuration.Metadata);

        // FAIL-FAST: Skip subFields processing if array is null or empty
        if (configuration.SubFields?.Length > 0)
        {
            foreach (var subField in configuration.SubFields)
            {
                // Use full field processing to handle dotted paths, types, aliases, etc.
                GetOrAddField(field.Fields, subField, Constants.DefaultFieldTypeSpan, null, field.Path);
            }
        }

        if (configuration.Action == null)
        {
            return this;
        }

        var fieldBuilder = new FieldBuilder(field);
        configuration.Action(fieldBuilder);

        _fieldDefinition.Fields[field.Name] = fieldBuilder._fieldDefinition;

        return this;
    }

    /// <summary>
    /// Adds a field with type and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, metadata: metadata));

    /// <summary>
    /// Adds a field with subfields, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.ObjectFieldType, subFields: subFields, arguments: arguments, metadata: metadata));

    /// <summary>
    /// Adds a field with type, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata = null)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, arguments: arguments, metadata: metadata));

    /// <summary>
    /// Adds a field with type, subfields, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? metadata = null)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, metadata: metadata));

    /// <summary>
    /// Adds a field with arguments to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.DefaultFieldType, arguments: arguments));

    /// <summary>
    /// Adds a field with arguments and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.DefaultFieldType, arguments: arguments, metadata: metadata));

    /// <summary>
    /// Adds a field with type, subfields, and arguments to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, SortedDictionary<string, object?>? arguments)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, arguments: arguments));

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
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, arguments: arguments, metadata: metadata));

    /// <summary>
    /// Adds a field with a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.DefaultFieldType, action: action));

    /// <summary>
    /// Adds a field with metadata and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.DefaultFieldType, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with type and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, action: action));

    /// <summary>
    /// Adds a field with type, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with arguments and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.DefaultFieldType, arguments: arguments, action: action));

    /// <summary>
    /// Adds a field with arguments, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.DefaultFieldType, arguments: arguments, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with type, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, arguments: arguments, action: action));

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
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, arguments: arguments, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with subfields and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.ObjectFieldType, subFields: subFields, action: action));

    /// <summary>
    /// Adds a field with subfields, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.ObjectFieldType, subFields: subFields, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with type, subfields, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, action: action));

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
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with subfields, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.ObjectFieldType, subFields: subFields, arguments: arguments, action: action));

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
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: Constants.ObjectFieldType, subFields: subFields, arguments: arguments, metadata: metadata, action: action));

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
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, arguments: arguments, action: action));

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
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type, subFields: subFields, arguments: arguments, metadata: metadata, action: action));

    /// <summary>
    /// Adds a field with optional type to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field (defaults to String).</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type = Constants.DefaultFieldType)
        => ApplyFieldConfiguration(fieldName, FieldConfiguration.From(type: type));

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
    private static FieldDefinition GetOrAddSimpleField(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string? fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
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

    private static FieldDefinition ProcessDottedFieldFastPath(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType)
    {
        var currentFields = fieldDefinitions;
        FieldDefinition? result = null;
        var pathStart = 0;

        while (pathStart < fieldPath.Length)
        {
            ExtractDottedSegment(fieldPath, pathStart, out var segment, out var isLastSegment, out var nextStart);
            var segmentName = segment.ToString();

            if (!currentFields.TryGetValue(segmentName, out var field))
            {
                field = CreateDottedFieldSegment(segment, fieldPath, pathStart + segment.Length, isLastSegment, fieldType);
                currentFields[segmentName] = field;
            }
            else if (!isLastSegment && field.ShouldConvertToObjectType())
            {
                field = currentFields[segmentName] = field with { Type = Constants.ObjectFieldType };
            }

            result = field;
            currentFields = field.Fields;
            pathStart = nextStart;
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

    private static FieldDefinition ProcessDottedFieldWithMetadata(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        var currentFields = fieldDefinitions;
        var parentPathSpan = parentPath.AsSpan();
        Span<char> pathBuffer = stackalloc char[512];
        var pathBuilder = new SpanPathBuilder(pathBuffer);

        if (!parentPathSpan.IsEmpty)
        {
            pathBuilder.Append(parentPathSpan);
        }

        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            ExtractDottedSegmentWithPath(fieldPath, out var segment, out var isLastSegment, out var remainingPath);
            
            if (segment.IsWhiteSpace())
            {
                fieldPath = remainingPath;
                continue;
            }

            pathBuilder.Append(segment);
            result = ProcessDottedSegment(currentFields, segment, isLastSegment, fieldType, arguments, metadata, pathBuilder.AsSpan());
            currentFields = result.Fields;
            fieldPath = remainingPath;
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

    private static void ExtractDottedSegment(ReadOnlySpan<char> fieldPath, int pathStart, out ReadOnlySpan<char> segment, out bool isLastSegment, out int nextStart)
    {
        var dotIndex = fieldPath.Slice(pathStart).IndexOf('.');
        isLastSegment = dotIndex == -1;
        var segmentEnd = isLastSegment ? fieldPath.Length : pathStart + dotIndex;
        segment = fieldPath.Slice(pathStart, segmentEnd - pathStart);
        nextStart = isLastSegment ? fieldPath.Length : segmentEnd + 1;
    }

    private static void ExtractDottedSegmentWithPath(ReadOnlySpan<char> fieldPath, out ReadOnlySpan<char> segment, out bool isLastSegment, out ReadOnlySpan<char> remainingPath)
    {
        var dotIndex = fieldPath.IndexOf('.');
        isLastSegment = dotIndex == -1;
        segment = isLastSegment ? fieldPath : fieldPath[..dotIndex];
        remainingPath = isLastSegment ? ReadOnlySpan<char>.Empty : fieldPath[(dotIndex + 1)..];
    }

    private static FieldDefinition CreateDottedFieldSegment(ReadOnlySpan<char> segment, ReadOnlySpan<char> fullPath, int segmentEnd, bool isLastSegment, ReadOnlySpan<char> fieldType)
    {
        var segmentType = isLastSegment ? fieldType : Constants.ObjectFieldTypeSpan;
        var segmentPath = fullPath.Slice(0, segmentEnd);
        
        return Helpers.CreateFieldDefinition(segment, segmentType, ReadOnlySpan<char>.Empty, null, segmentPath, null);
    }

    private static FieldDefinition ProcessDottedSegment(SortedDictionary<string, FieldDefinition> currentFields, ReadOnlySpan<char> segment, bool isLastSegment, ReadOnlySpan<char> fieldType, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, ReadOnlySpan<char> segmentPath)
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

    private static FieldDefinition UpdateExistingField(SortedDictionary<string, FieldDefinition> currentFields, SpanSegment segment, FieldDefinition field, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> parsedFieldType)
    {
        if (segment.HasAlias && field._alias == null)
        {
            field._alias = segment.Alias.IsEmpty ? null : segment.Alias.ToString();
        }

        if (!segment.IsLastFragment && field.ShouldConvertToObjectType())
        {
            field._type = Constants.ObjectFieldType;
        }

        if (segment.IsLastFragment)
        {
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
        // Try to find existing field to merge with
        var existingField = Helpers.FindExistingField(fields, fieldDefinition);
        if (existingField != null)
        {
            var mergedField = existingField.MergeFieldArguments(fieldDefinition._arguments);
            fields[existingField.Name] = mergedField;
            return mergedField;
        }

        // Create new field
        var newField = Helpers.CreateFieldDefinition(
            fieldDefinition.Name.AsSpan(),
            (fieldDefinition._type ?? Constants.DefaultFieldType),
            (fieldDefinition._alias ?? "").AsSpan(),
            fieldDefinition._arguments,
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
