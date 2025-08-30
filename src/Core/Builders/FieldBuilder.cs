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
        var arguments = fieldDefinition.Arguments ?? Constants.EmptyArguments;
        GetOrAddField(_fieldDefinition.Fields, fieldDefinition.Name, fieldDefinition.Type, in arguments, _fieldDefinition.Path, fieldDefinition.Metadata);
        return this;
    }

    /// <summary>
    /// Adds a field with optional type to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field (defaults to String).</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type = Constants.DefaultFieldType)
        => AddFieldCore(fieldName, type, null, in Constants.EmptyArguments, in Constants.EmptyMetadata, null);

    /// <summary>
    /// Adds a field with type and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, type, null, in Constants.EmptyArguments, in metadata, null);

    /// <summary>
    /// Adds a field with subfields, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, in arguments, in metadata, null);

    /// <summary>
    /// Adds a field with type, arguments, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, type, null, in arguments, in metadata, null);

    /// <summary>
    /// Adds a field with type, subfields, and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, type, subFields, in Constants.EmptyArguments, in metadata, null);

    /// <summary>
    /// Adds a field with arguments to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, in arguments, in Constants.EmptyMetadata, null);

    /// <summary>
    /// Adds a field with arguments and metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, in arguments, in metadata, null);

    /// <summary>
    /// Adds a field with type, subfields, and arguments to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, SortedDictionary<string, object?>? arguments)
        => AddFieldCore(fieldName, type, subFields, in arguments, in Constants.EmptyMetadata, null);

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
        => AddFieldCore(fieldName, type, subFields, in arguments, in metadata, null);

    /// <summary>
    /// Adds a field with a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, in Constants.EmptyArguments, in Constants.EmptyMetadata, action);

    /// <summary>
    /// Adds a field with metadata and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, in Constants.EmptyArguments, in metadata, action);

    /// <summary>
    /// Adds a field with type and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, in Constants.EmptyArguments, in Constants.EmptyMetadata, action);

    /// <summary>
    /// Adds a field with type, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, in Constants.EmptyArguments, in metadata, action);

    /// <summary>
    /// Adds a field with arguments and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, in arguments, in Constants.EmptyMetadata, action);

    /// <summary>
    /// Adds a field with arguments, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.DefaultFieldType, null, in arguments, in metadata, action);

    /// <summary>
    /// Adds a field with type, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, null, in arguments, in Constants.EmptyMetadata, action);

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
        => AddFieldCore(fieldName, type, null, in arguments, in metadata, action);

    /// <summary>
    /// Adds a field with subfields and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, in Constants.EmptyArguments, in Constants.EmptyMetadata, action);

    /// <summary>
    /// Adds a field with subfields, metadata, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, in Constants.EmptyArguments, in metadata, action);

    /// <summary>
    /// Adds a field with type, subfields, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields, in Constants.EmptyArguments, in Constants.EmptyMetadata, action);

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
        => AddFieldCore(fieldName, type, subFields, in Constants.EmptyArguments, in metadata, action);

    /// <summary>
    /// Adds a field with subfields, arguments, and a nested builder action to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="action">The action to configure nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, string[] subFields, SortedDictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, in arguments, in Constants.EmptyMetadata, action);

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
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields, in arguments, in metadata, action);

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
        => AddFieldCore(fieldName, type, subFields, in arguments, in Constants.EmptyMetadata, action);

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
        => AddFieldCore(fieldName, type, subFields, in arguments, in metadata, action);

    private FieldBuilder AddFieldCore(string fieldName, string type, string[]? subFields, in SortedDictionary<string, object?>? arguments, in Dictionary<string, object?>? metadata, Action<FieldBuilder>? action)
    {
        var args = arguments ?? Constants.EmptyArguments;
        var field = GetOrAddField(_fieldDefinition.Fields, fieldName.AsSpan(), type, in args, _fieldDefinition.Path, metadata);

        if (subFields != null)
        {
            foreach (var subField in subFields)
            {
                GetOrAddField(field.Fields, subField.AsSpan(), Constants.DefaultFieldType, in Constants.EmptyArguments, field.Path, null);
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
        return Create(fieldDefinitions, fieldName, Constants.DefaultFieldType, Constants.EmptyArguments);
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
    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type, SortedDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
    {
        var argumentsToUse = arguments ?? Constants.EmptyArguments;
        var rootField = GetOrAddField(fieldDefinitions, fieldName.AsSpan(), type, in argumentsToUse, null, metadata);

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

    private static FieldDefinition GetOrAddField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, string? type, in SortedDictionary<string, object?>? arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
    {
        var currentFields = fieldDefinitions;
        var argumentsRef = arguments ?? Constants.EmptyArguments;
        var fieldType = type ?? Constants.DefaultFieldType;
        var fullPath = string.IsNullOrWhiteSpace(parentPath) ? new List<string>() : Helpers.SplitPathToList(parentPath.AsSpan());

        fieldPath = Helpers.ParseFieldTypeFromPath(fieldPath, fieldType, out var parsedFieldType);

        FieldDefinition? result = null;

        while (fieldPath.Length > 0)
        {
            var segment = ExtractNextSegment(ref fieldPath);
            if (string.IsNullOrWhiteSpace(segment.Name))
            {
                continue;
            }

            fullPath.Add(segment.Name);

            // Use segment-level parsed type if available, otherwise use the overall parsed type
            var typeToUse = segment.ParsedType ?? parsedFieldType;

            result = ProcessFieldSegment(currentFields, segment, argumentsRef, typeToUse, fullPath, metadata);
            currentFields = result.Fields;
        }

        return result ?? throw new InvalidOperationException("Failed to create field: no valid segments found");
    }

    private static (string Name, string? Alias, bool IsLastFragment, string? ParsedType) ExtractNextSegment(ref ReadOnlySpan<char> fieldPath)
    {
        var nextDot = fieldPath.IndexOf('.');
        var isLastFragment = nextDot == -1;
        var currentPart = isLastFragment ? fieldPath : fieldPath[..nextDot];
        var currentPath = currentPart.ToString().Trim();

        // Parse and remove type information from the segment
        var cleanedPath = Helpers.ParseFieldTypeFromPath(currentPath.AsSpan(), Constants.DefaultFieldType, out var parsedType);
        var cleanedPathString = cleanedPath.ToString();

        var (name, alias) = Helpers.GetFieldNameAndAlias(cleanedPathString);

        fieldPath = nextDot == -1 ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];

        // Only return parsed type if it's not the default (meaning type was actually specified)
        var typeToReturn = parsedType != Constants.DefaultFieldType ? parsedType : null;

        return (name, alias, isLastFragment, typeToReturn);
    }

    private static FieldDefinition ProcessFieldSegment(SortedDictionary<string, FieldDefinition> currentFields, (string Name, string? Alias, bool IsLastFragment, string? ParsedType) segment, SortedDictionary<string, object?> argumentsRef, string parsedFieldType, List<string> fullPath, Dictionary<string, object?>? metadata)
    {
        if (!currentFields.TryGetValue(segment.Name, out var field))
        {
            return CreateNewField(currentFields, segment, argumentsRef, parsedFieldType, fullPath, metadata);
        }

        return UpdateExistingField(currentFields, segment, field, argumentsRef, parsedFieldType);
    }

    private static FieldDefinition CreateNewField(SortedDictionary<string, FieldDefinition> currentFields, (string Name, string? Alias, bool IsLastFragment, string? ParsedType) segment, SortedDictionary<string, object?> argumentsRef, string parsedFieldType, List<string> fullPath, Dictionary<string, object?>? metadata)
    {
        // Guard: field names should not contain spaces (type info should be consumed)
        if (segment.Name.Contains(' '))
        {
            throw new InvalidOperationException($"Field name '{segment.Name}' contains spaces. Type information should be consumed during parsing.");
        }

        var fieldArgs = segment.IsLastFragment ? argumentsRef : Constants.EmptyArguments;
        var intermediateType = parsedFieldType == Constants.ArrayTypeMarker ? parsedFieldType : Constants.ObjectFieldType;
        var computedFieldType = segment.IsLastFragment ? parsedFieldType : intermediateType;
        var fieldMetadata = segment.IsLastFragment ? metadata : null;

        var field = Helpers.CreateFieldDefinition(segment.Name, computedFieldType, segment.Alias, in fieldArgs, string.Join(".", fullPath), fieldMetadata);
        currentFields[segment.Name] = field;
        return field;
    }

    private static FieldDefinition UpdateExistingField(SortedDictionary<string, FieldDefinition> currentFields, (string Name, string? Alias, bool IsLastFragment, string? ParsedType) segment, FieldDefinition field, SortedDictionary<string, object?> argumentsRef, string parsedFieldType)
    {
        if (segment.Alias != null && field.Alias == null)
        {
            field = currentFields[segment.Name] = field with { Alias = segment.Alias };
        }

        if (!segment.IsLastFragment && field.ShouldConvertToObjectType())
        {
            field = currentFields[segment.Name] = field with { Type = Constants.ObjectFieldType };
        }

        if (segment.IsLastFragment)
        {
            if (argumentsRef.Count > 0)
            {
                field = currentFields[segment.Name] = field.MergeFieldArguments(argumentsRef);
            }

            if (!string.Equals(parsedFieldType, Constants.DefaultFieldType, StringComparison.OrdinalIgnoreCase) && 
                !string.Equals(field.Type, parsedFieldType, StringComparison.OrdinalIgnoreCase))
            {
                field = currentFields[segment.Name] = field with { Type = parsedFieldType };
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
        var arguments = fieldDefinition.Arguments ?? Constants.EmptyArguments;

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
            fieldDefinition.Name,
            fieldDefinition.Type ?? Constants.DefaultFieldType,
            fieldDefinition.Alias,
            in arguments,
            fieldDefinition.Path,
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
        var existingMetadata = Helpers.NormalizeMetadata(_fieldDefinition.Metadata);
        var mergedMetadata = Helpers.MergeMetadata(existingMetadata, metadata);

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
        if (_fieldDefinition.Arguments != null && _fieldDefinition.Arguments.TryGetValue(key, out var existingValue))
        {
            if (existingValue is IDictionary<string, object> existingDict && value is IDictionary<string, object> newDict)
            {
                // Merge nested dictionaries
                _fieldDefinition.Arguments[key] = Helpers.MergeDictionaries(existingDict, newDict);
            }
            else
            {
                // Override the existing value with the new one
                _fieldDefinition.Arguments[key] = value;
            }
        }
        else
        {
            if (_fieldDefinition.Arguments is null)
            {
                _fieldDefinition = _fieldDefinition with { Arguments = [] };
            }
            else
            {
                _fieldDefinition.Arguments[key] = value;
            }
        }

        return this;
    }

}
