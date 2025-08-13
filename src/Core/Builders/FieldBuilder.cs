using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Builders;

public sealed class FieldBuilder
{
    internal static readonly SortedDictionary<string, object?> EmptyArguments = [];

    private FieldDefinition _fieldDefinition;

    private FieldBuilder(FieldDefinition fieldDefinition)
    {
        _fieldDefinition = fieldDefinition;
    }
    
    public FieldBuilder AddField(string fieldName, string type = Constants.DefaultFieldType)
    {
        GetOrAddField(_fieldDefinition.Fields, fieldName, type, in EmptyArguments, _fieldDefinition.Path, null);
        return this;
    }

    public FieldBuilder AddField(FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        var arguments = fieldDefinition.Arguments ?? EmptyArguments;
        GetOrAddField(_fieldDefinition.Fields, fieldDefinition.Name, fieldDefinition.Type, in arguments, _fieldDefinition.Path, fieldDefinition.Metadata);

        return this;
    }
    
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?> arguments, Dictionary<string, object?>? metadata = null)
    {
        GetOrAddField(_fieldDefinition.Fields, fieldName, type, in arguments, _fieldDefinition.Path, metadata);

        return this;
    }

    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? metadata = null)
    {
        var field = GetOrAddField(_fieldDefinition.Fields, fieldName, Constants.ObjectFieldType, in EmptyArguments, _fieldDefinition.Path, metadata);

        foreach (var subField in subFields)
        {
            GetOrAddField(field.Fields, subField, Constants.DefaultFieldType, in EmptyArguments, field.Path, null);
        }
        
        return this;
    }

    public FieldBuilder AddField(string fieldName, string type, Action<FieldBuilder> action)
    {
        var addedField = GetOrAddField(_fieldDefinition.Fields, fieldName, type, in EmptyArguments, _fieldDefinition.Path, null);

        var fieldBuilder = new FieldBuilder(addedField);

        action(fieldBuilder);

        var updatedField = fieldBuilder.Build();

        Helpers.ApplyFieldChanges(_fieldDefinition.Fields, updatedField);

        return this;
    }

    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName)
    {
        return Create(fieldDefinitions, fieldName, Constants.DefaultFieldType, EmptyArguments);
    }
    
    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type, SortedDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
    {
        var argumentsToUse = arguments ?? EmptyArguments;
        var rootField = GetOrAddField(fieldDefinitions, fieldName, type, in argumentsToUse, null, metadata);

        var fieldBuilder = new FieldBuilder(rootField);

        return fieldBuilder;
    }
    
    public static FieldBuilder Create(FieldDefinition fieldDefinition) => new(fieldDefinition);

    public FieldDefinition Build()
        => _fieldDefinition ?? throw new InvalidOperationException("Field definition is not set. Use AddField or CreateField methods to define fields.");

    #region Private Core Implementation Methods

    /// <summary>
    /// Core field creation and retrieval method with optimized parameter passing.
    /// This method handles the complex logic of parsing field paths, managing aliases,
    /// and creating nested field structures efficiently.
    /// </summary>
    /// <param name="fieldDefinitions">Dictionary to store field definitions</param>
    /// <param name="fieldPath">Field path to parse (supports dot notation and aliases)</param>
    /// <param name="type">Field type (optional, defaults to DefaultFieldType)</param>
    /// <param name="arguments">Field arguments (passed by reference for performance)</param>
    /// <param name="parentPath">Parent field path for nested structures</param>
    /// <param name="metadata">Field metadata (optional)</param>
    /// <returns>The created or retrieved FieldDefinition</returns>
    private static FieldDefinition GetOrAddField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, string? type, in SortedDictionary<string, object?> arguments, string? parentPath = null, Dictionary<string, object?>? metadata = null)
    {
        return GetOrAddFieldCore(fieldDefinitions, fieldPath, type, in arguments, parentPath, metadata);
    }

    /// <summary>
    /// Core implementation of field creation with all the complex parsing and caching logic.
    /// Separated from the public interface to allow for optimized parameter passing.
    /// </summary>
    private static FieldDefinition GetOrAddFieldCore(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, string? type, in SortedDictionary<string, object?> arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        FieldDefinition? value = null;
        var currentFields = fieldDefinitions;
        
        // Use the passed arguments directly since they're guaranteed to be non-null
        var argumentsRef = arguments;
        type ??= Constants.DefaultFieldType;

        var fullPath = string.IsNullOrWhiteSpace(parentPath)
            ? []
            : parentPath.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();

        // Parse type from fieldPath if provided in format "Type fieldPath"
        var fieldPathStr = fieldPath.ToString();
        var spaceIndex = fieldPathStr.IndexOf(' ');
        if (spaceIndex > 0 && (char.IsUpper(fieldPathStr[0]) || fieldPathStr[0] == '['))
        {
            // Type specification must be at beginning and start with uppercase letter or '['
            type = fieldPathStr[..spaceIndex];
            fieldPath = fieldPathStr[(spaceIndex + 1)..].AsSpan();
        }

        while (fieldPath.Length > 0)
        {
            var nextDot = fieldPath.IndexOf('.');
            var isLastFragment = nextDot == -1;

            var currentPart = isLastFragment
                ? fieldPath
                : fieldPath[..nextDot];

            var currentPath = currentPart.ToString(); 
            var (name, alias) = GetFieldNameAndAlias(currentPath);

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                fieldPath = GetNextFieldPath(fieldPath, nextDot);
                continue;
            }

            fullPath.Add(name);
            var cacheKey = string.Join(".", fullPath);

            if (fieldDefinitions.TryGetValue(cacheKey, out var cachedField))
            {
                value = cachedField;
                if (isLastFragment && argumentsRef.Count > 0)
                {
                    value = currentFields[name] = value.MergeFieldArguments(argumentsRef);
                }

                // Check if we should convert this field to object type for nesting
                if (!isLastFragment && value.ShouldConvertToObjectType())
                {
                    value = currentFields[name] = value with { Type = Constants.ObjectFieldType };
                }

                currentFields = value.Fields;

                fieldPath = GetNextFieldPath(fieldPath, nextDot);
                continue;
            }

            if (!currentFields.TryGetValue(name, out var childValue))
            {
                var fieldArguments = isLastFragment ? argumentsRef : EmptyArguments;
                // For intermediate nodes, always use object type
                var fieldType = isLastFragment ? type : Constants.ObjectFieldType;
                // Only pass metadata for the last fragment (final field)
                var fieldMetadata = isLastFragment ? metadata : null;
                value = currentFields[name] = GetNewField(name, fieldType, alias, in fieldArguments, cacheKey, fieldMetadata);
            }
            else
            {
                value = childValue;
                // Update alias if one is provided and the field doesn't already have one
                if (alias != null && value.Alias == null)
                {
                    value = currentFields[name] = value with
                    {
                        Alias = alias
                    };
                }

                // Check if we should convert this field to object type for nesting
                if (!isLastFragment && value.ShouldConvertToObjectType())
                {
                    value = currentFields[name] = value with { Type = Constants.ObjectFieldType };
                }

                if (isLastFragment && argumentsRef.Count > 0)
                {
                    value = currentFields[name] = value.MergeFieldArguments(argumentsRef);
                }

                // If this is the last fragment and we should update the type
                if (isLastFragment && type != Constants.DefaultFieldType && value.Type != type)
                {
                    value = currentFields[name] = value with { Type = type };
                }
            }
            
            fieldPath = GetNextFieldPath(fieldPath, nextDot);
            currentFields = value.Fields;
        }

        return value ?? throw new InvalidOperationException($"Failed to create a new field for path: {fieldPath}");

        // Local helper function for advancing through the field path
        ReadOnlySpan<char> GetNextFieldPath(ReadOnlySpan<char> readOnlySpan, int nextDot)
        {
            return nextDot == -1 ? ReadOnlySpan<char>.Empty : readOnlySpan[(nextDot + 1)..];
        }
    }

    /// <summary>
    /// Creates a new FieldDefinition with sorted arguments for consistent behavior.
    /// Arguments are passed by reference to avoid unnecessary copying of potentially large dictionaries.
    /// </summary>
    /// <param name="name">Field name</param>
    /// <param name="type">Field type</param>
    /// <param name="alias">Optional field alias</param>
    /// <param name="arguments">Field arguments (passed by reference for performance)</param>
    /// <param name="path">Field path for caching</param>
    /// <param name="metadata">Optional field metadata</param>
    /// <returns>New FieldDefinition instance</returns>
    private static FieldDefinition GetNewField(string name, string type, string? alias, in SortedDictionary<string, object?> arguments, string path, Dictionary<string, object?>? metadata = null)
    {
        // Create a new sorted dictionary to ensure consistent argument ordering
        var sortedArguments = new SortedDictionary<string, object?>(arguments.ToDictionary(
            kvp => kvp.Key,
            kvp => Helpers.SortArgumentValue(kvp.Value)));

        return new FieldDefinition(name, type, alias, sortedArguments, [])
        {
            Path = path,
            Metadata = metadata
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Parses a field string to extract the field name and optional alias.
    /// Supports the format "Alias:FieldName" where Alias is optional.
    /// </summary>
    /// <param name="field">Field string to parse</param>
    /// <returns>Tuple containing the field name and optional alias</returns>
    private static (string Name, string? Alias) GetFieldNameAndAlias(string field)
    {
        var fieldNameParts = field.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var namePart = fieldNameParts.Length == 2 ? fieldNameParts[1] : field;
        var alias = fieldNameParts.Length == 2 ? fieldNameParts[0] : null;

        return (namePart, alias);
    }

    /// <summary>
    /// Recursively creates and merges field definitions into the target field collection.
    /// This method handles field merging, argument consolidation, and nested field creation.
    /// </summary>
    /// <param name="fields">Target field collection</param>
    /// <param name="fieldDefinition">Field definition to create/merge</param>
    private static void RecursiveCreateField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        FieldDefinition parentField;

        // Try to find an existing field with the same name and alias
        var existingField = fields.Values.FirstOrDefault(f => 
            f.Name == fieldDefinition.Name && f.Alias == fieldDefinition.Alias);

        var arguments = fieldDefinition.Arguments ?? EmptyArguments;

        if (existingField != null)
        {
            // Merge with existing field by combining arguments
            parentField = existingField.MergeFieldArguments(arguments);
            fields[existingField.Name] = parentField;
        }
        else if (fields.TryGetValue(fieldDefinition.Path, out var pathField))
        {
            // Field exists with same path - merge arguments
            parentField = pathField.MergeFieldArguments(arguments);
            fields[pathField.Path] = parentField;
        }
        else
        {
            // Create new field since none exists
            parentField = GetNewField(
                fieldDefinition.Name, 
                fieldDefinition.Type ?? Constants.DefaultFieldType, 
                fieldDefinition.Alias, 
                in arguments, 
                fieldDefinition.Path, 
                fieldDefinition.Metadata);
            fields[fieldDefinition.Name] = parentField;
        }

        // Recursively process all child fields
        foreach (var childFieldDefinition in fieldDefinition.Fields.Values)
        {
            RecursiveCreateField(parentField.Fields, childFieldDefinition);
        }
    }

    #endregion

    internal static void Include(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
        => RecursiveCreateField(fields, fieldDefinition);

    public FieldBuilder WithAlias(string alias)
    {
        _fieldDefinition = _fieldDefinition with { Alias = alias };

        return this;
    }

    public FieldBuilder WithType(string type)
    {
        _fieldDefinition = _fieldDefinition with { Type = type };

        return this;
    }

    public FieldBuilder WithMetadata(Dictionary<string, object> metadata)
    {
        var existingMetadata = Helpers.NormalizeMetadata(_fieldDefinition.Metadata);
        var mergedMetadata = Helpers.MergeMetadata(existingMetadata, metadata);

        _fieldDefinition.Metadata = mergedMetadata;

        return this;
    }
    
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
