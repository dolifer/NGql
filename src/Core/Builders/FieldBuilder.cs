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
        GetOrAddField(_fieldDefinition.Fields, fieldName, type, EmptyArguments, _fieldDefinition.Path, null);
        return this;
    }

    public FieldBuilder AddField(FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        GetOrAddField(_fieldDefinition.Fields, fieldDefinition.Name, fieldDefinition.Type, fieldDefinition.Arguments ?? [], _fieldDefinition.Path, fieldDefinition.Metadata);

        return this;
    }
    
    public FieldBuilder AddField(string fieldName, string type, SortedDictionary<string, object?> arguments)
    {
        GetOrAddField(_fieldDefinition.Fields, fieldName, type, arguments, _fieldDefinition.Path, null);

        return this;
    }

    public FieldBuilder AddField(string fieldName, string[] subFields)
    {
        var field = GetOrAddField(_fieldDefinition.Fields, fieldName, Constants.ObjectFieldType, EmptyArguments, _fieldDefinition.Path, null);

        foreach (var subField in subFields)
        {
            GetOrAddField(field.Fields, subField, Constants.DefaultFieldType, EmptyArguments, field.Path, null);
        }
        
        return this;
    }

    public FieldBuilder AddField(string fieldName, string type, Action<FieldBuilder> action)
    {
        var addedField = GetOrAddField(_fieldDefinition.Fields, fieldName, type, EmptyArguments, _fieldDefinition.Path, null);

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
    
    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type, SortedDictionary<string, object?>? arguments = null)
    {
        var rootField = GetOrAddField(fieldDefinitions, fieldName, type, arguments ?? EmptyArguments, null, null);

        var fieldBuilder = new FieldBuilder(rootField);

        return fieldBuilder;
    }
    
    public static FieldBuilder Create(FieldDefinition fieldDefinition) => new(fieldDefinition);

    public FieldDefinition Build()
        => _fieldDefinition ?? throw new InvalidOperationException("Field definition is not set. Use AddField or CreateField methods to define fields.");

    private static FieldDefinition GetOrAddField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, string? type = null, SortedDictionary<string, object?>? arguments = null, string? parentPath = null, Dictionary<string, object?>? metadata = null)
    {
        FieldDefinition? value = null;
        var currentFields = fieldDefinitions;
        arguments ??= new SortedDictionary<string, object?>();
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
                if (isLastFragment && arguments.Count > 0)
                {
                    value = currentFields[name] = value.MergeFieldArguments(arguments);
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
                var fieldArguments = isLastFragment ? arguments : EmptyArguments;
                // For intermediate nodes, always use object type
                var fieldType = isLastFragment ? type : Constants.ObjectFieldType;
                // Only pass metadata for the last fragment (final field)
                var fieldMetadata = isLastFragment ? metadata : null;
                value = currentFields[name] = GetNewField(name, fieldType, alias, fieldArguments, cacheKey, fieldMetadata);
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

                if (isLastFragment && arguments.Count > 0)
                {
                    value = currentFields[name] = value.MergeFieldArguments(arguments);
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

        ReadOnlySpan<char> GetNextFieldPath(ReadOnlySpan<char> readOnlySpan, int nextDot)
        {
            return nextDot == -1 ? ReadOnlySpan<char>.Empty : readOnlySpan[(nextDot + 1)..];
        }
    }

    private static FieldDefinition GetNewField(string name, string type, string? alias, SortedDictionary<string, object?> arguments, string path, Dictionary<string, object?>? metadata = null)
    {
        var sortedArguments = new SortedDictionary<string, object?>(arguments.ToDictionary(
            kvp => kvp.Key,
            kvp => Helpers.SortArgumentValue(kvp.Value)));

        return new FieldDefinition(name, type, alias, sortedArguments, [])
        {
            Path = path,
            Metadata = metadata
        };
    }

    private static (string Name, string? Alias) GetFieldNameAndAlias(string field)
    {
        var fieldNameParts = field.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var namePart = fieldNameParts.Length == 2 ? fieldNameParts[1] : field;
        var alias = fieldNameParts.Length == 2 ? fieldNameParts[0] : null;

        return (namePart, alias);
    }

    private static void RecursiveCreateField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        FieldDefinition parentField;

        // Try to find an existing field with the same name (not path)
        var existingField = fields.Values.FirstOrDefault(f => f.Name == fieldDefinition.Name && f.Alias == fieldDefinition.Alias);

        if (existingField != null)
        {
            // Merge with existing field
            parentField = existingField.MergeFieldArguments(fieldDefinition.Arguments ?? EmptyArguments);
            fields[existingField.Name] = parentField;
        }
        else if (fields.TryGetValue(fieldDefinition.Path, out var pathField))
        {
            // Field exists with same path
            parentField = pathField.MergeFieldArguments(fieldDefinition.Arguments ?? EmptyArguments);
            fields[pathField.Path] = parentField;
        }
        else
        {
            // Create new field
            parentField = GetNewField(fieldDefinition.Name, fieldDefinition.Type ?? Constants.DefaultFieldType, fieldDefinition.Alias, fieldDefinition.Arguments ?? EmptyArguments, fieldDefinition.Path, fieldDefinition.Metadata);
            fields[fieldDefinition.Name] = parentField;
        }

        foreach (var childFieldDefinition in fieldDefinition.Fields.Values)
        {
            RecursiveCreateField(parentField.Fields, childFieldDefinition);
        }
    }

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
