using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;

namespace NGql.Core.Builders;

public sealed class FieldBuilder
{
    private static readonly SortedDictionary<string, object?> EmptyArguments = [];

    private FieldDefinition _fieldDefinition;

    private FieldBuilder(FieldDefinition fieldDefinition)
    {
        _fieldDefinition = fieldDefinition;
    }
    
    public FieldBuilder AddField(string fieldName)
    {
        GetOrAddField(_fieldDefinition.Fields, fieldName, EmptyArguments, _fieldDefinition.Path);
        return this;
    }
    
    public FieldBuilder AddField(string fieldName, SortedDictionary<string, object?> arguments)
    {
        GetOrAddField(_fieldDefinition.Fields, fieldName, arguments, _fieldDefinition.Path);

        return this;
    }

    public FieldBuilder AddField(string fieldName, string[] subFields)
    {
        var field = GetOrAddField(_fieldDefinition.Fields, fieldName, EmptyArguments, _fieldDefinition.Path);
        
        foreach (var subField in subFields)
        {
            GetOrAddField(field.Fields, subField, EmptyArguments, field.Path);
        }
        
        return this;
    }

    public FieldBuilder AddField(string fieldName, Action<FieldBuilder> action)
    {
        var addedField = GetOrAddField(_fieldDefinition.Fields, fieldName, EmptyArguments, _fieldDefinition.Path);

        var fieldBuilder = new FieldBuilder(addedField);

        action(fieldBuilder);

        var updatedField = fieldBuilder.Build();

        Helpers.ApplyFieldChanges(_fieldDefinition.Fields, updatedField);

        return this;
    }

    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, SortedDictionary<string, object?>? arguments = null)
    {
        var rootField = GetOrAddField(fieldDefinitions, fieldName, arguments ?? EmptyArguments, null);
        
        var fieldBuilder = new FieldBuilder(rootField);

        return fieldBuilder;
    }
    
    public static FieldBuilder Create(FieldDefinition fieldDefinition)
    {
        return new(fieldDefinition);
    }

    public FieldDefinition Build()
    {
        return _fieldDefinition ?? throw new InvalidOperationException("Field definition is not set. Use AddField or CreateField methods to define fields.");
    }

    private static FieldDefinition GetOrAddField(SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldPath, SortedDictionary<string, object?> arguments, string? parentPath)
    {
        FieldDefinition? value = null;
        var currentFields = fieldDefinitions;
        var fullPath = string.IsNullOrWhiteSpace(parentPath)
            ? []
            : parentPath.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();

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
                    value = currentFields[name] = MergeFieldArguments(value, arguments);
                }
                currentFields = value.Fields;
                
                fieldPath = GetNextFieldPath(fieldPath, nextDot);
                continue;
            }

            if (!currentFields.TryGetValue(name, out var childValue))
            {
                var fieldArguments = isLastFragment ? arguments : EmptyArguments;
                value = currentFields[name] = GetNewField(name, alias, fieldArguments, cacheKey);
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
                if (isLastFragment && arguments.Count > 0)
                {
                    value = currentFields[name] = MergeFieldArguments(value, arguments);
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

    private static FieldDefinition MergeFieldArguments(FieldDefinition existingField, SortedDictionary<string, object?> newArguments)
    {
        if (existingField.Arguments is null || existingField.Arguments.Count == 0)
        {
            return existingField;
        }

        var mergedArguments = new SortedDictionary<string, object?>(existingField.Arguments ?? EmptyArguments);
        foreach (var (key, newValue) in newArguments)
        {
            if (!mergedArguments.TryGetValue(key, out var existingValue))
            {
                mergedArguments[key] = newValue;
                continue;
            }

            if (existingValue is IDictionary<string, object> existingDict && newValue is IDictionary<string, object> newDict)
            {
                // Merge nested dictionaries
                mergedArguments[key] = Helpers.MergeDictionaries(existingDict, newDict);
                continue;
            }

            // For non-dictionary values, the new value overrides existing
            mergedArguments[key] = newValue;
        }

        return existingField with { Arguments = mergedArguments };
    }

    private static FieldDefinition GetNewField(string name, string? alias, SortedDictionary<string, object?> arguments, string path)
    {
        var sortedArguments = new SortedDictionary<string, object?>(arguments.ToDictionary(
            kvp => kvp.Key,
            kvp => Helpers.SortArgumentValue(kvp.Value)));

        return new FieldDefinition(name, alias, sortedArguments, [])
        {
            Path = path
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
        if (fields.TryGetValue(fieldDefinition.Path, out var parentField))
        {
            fields[parentField.Path] = MergeFieldArguments(parentField, fieldDefinition.Arguments ?? EmptyArguments);
        }
        else
        {
            parentField = GetNewField(fieldDefinition.Name, fieldDefinition.Alias, fieldDefinition.Arguments ?? EmptyArguments, fieldDefinition.Path);
            fields[parentField.Path] = parentField;
        }

        foreach (var childFieldDefinition in fieldDefinition.Fields.Values)
        {
            RecursiveCreateField(parentField.Fields, childFieldDefinition);
        }
    }
    
    internal static void Include(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        RecursiveCreateField(fields, fieldDefinition);
    }

    public FieldBuilder WithAlias(string alias)
    {
        _fieldDefinition = _fieldDefinition with { Alias = alias };

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
                _fieldDefinition = _fieldDefinition with { Arguments = new SortedDictionary<string, object?>() };
            }
            else
            {
                _fieldDefinition.Arguments[key] = value;
            }
        }

        return this;
    }
}
