using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Builders;

internal sealed class QueryTextBuilder
{
    private readonly StringBuilder _stringBuilder = new();
    private const int IndentSize = 4;

    public string Build(QueryBlock queryBlock, int indent = 0, string? prefix = null)
    {
        string pad = new(' ', indent);
        string prevPad = pad;

        if (!string.IsNullOrWhiteSpace(queryBlock.Alias) && indent != 0)
        {
            _stringBuilder.Append(pad);
            _stringBuilder.Append(queryBlock.Alias);
            _stringBuilder.Append(':');
            pad = "";
        }

        _stringBuilder.Append(pad);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            _stringBuilder.Append(prefix).Append(' ');
        }

        _stringBuilder.Append(queryBlock.Name);

        AddArguments(queryBlock, indent == 0);
        indent += IndentSize;

        AddFields(queryBlock, prevPad, indent);
        return _stringBuilder.ToString();
    }

    public string Build(QueryDefinition queryDefinition)
    {
        _stringBuilder.Clear();
        _stringBuilder.Append("query ");

        if (!string.IsNullOrEmpty(queryDefinition.Name))
            _stringBuilder.Append(queryDefinition.Name);

        if (queryDefinition.Variables.Count > 0)
        {
            _stringBuilder.Append('(');
            bool first = true;
            foreach (var variable in queryDefinition.Variables)
            {
                if (!first) _stringBuilder.Append(", ");
                first = false;
                variable.Print(_stringBuilder, variable.Name, true);
            }

            _stringBuilder.Append(')');
        }

        _stringBuilder.AppendLine("{");

        BuildFieldDefinitions(queryDefinition.Fields, IndentSize);

        _stringBuilder.Append("}");
        return _stringBuilder.ToString();
    }
    
    private void BuildFieldDefinitions(SortedDictionary<string, FieldDefinition> fields, int indent)
    {
        string padding = new(' ', indent);

        // Sort fields by both alias and name to maintain consistent ordering
        var orderedFields = fields.Values
            .OrderBy(f => f.Alias ?? f.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase);
        
        foreach (var field in orderedFields)
        {
            _stringBuilder.Append(padding);

            if (field.Alias != null)
            {
                _stringBuilder.Append(field.Alias);
                _stringBuilder.Append(':');
            }

            _stringBuilder.Append(field.Name);

            if (field.Arguments is { Count: > 0})
                BuildFieldArguments(field.Arguments);

            if (field.Fields.Count > 0)
            {
                _stringBuilder.AppendLine("{");
                BuildFieldDefinitions(field.Fields, indent + IndentSize);
                _stringBuilder.Append(padding);
                _stringBuilder.AppendLine("}");
            }
            else
            {
                _stringBuilder.AppendLine();
            }
        }
    }

    private void BuildFieldArguments(IReadOnlyDictionary<string, object?> arguments)
    {
        _stringBuilder.Append('(');

        bool first = true;
        foreach (var (key, value) in arguments)
        {
            if (!first) _stringBuilder.Append(", ");
            first = false;

            _stringBuilder.Append(key);
            _stringBuilder.Append(':');
            WriteObject(_stringBuilder, value);
        }

        _stringBuilder.Append(')');
    }

    internal static void WriteObject(StringBuilder builder, object? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        if (ValueFormatter.TryFormatPrimitiveType(value, out var formattedValue))
        {
            builder.Append(formattedValue);
            return;
        }

        var valueType = value.GetType();
        if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
        {
            var kvp = (dynamic)value;
            builder.Append(kvp.Key);
            builder.Append(':');

            WriteObject(builder, kvp.Value);
            return;
        }

        switch (value)
        {
            case IList listValue:
            {
                WriteCollection('[', ']', listValue, builder);
                break;
            }

            case IDictionary dictValue:
            {
                WriteCollection('{', '}', dictValue, builder);
                break;
            }

            default:
            {
                var values = valueType
                    .GetProperties()
                    .ToDictionary(x => x.Name, x => x.GetValue(value));
                WriteCollection('{', '}', values, builder);
                break;
            }
        }
    }

    private static void WriteCollection(char prefix, char suffix, IEnumerable list, StringBuilder builder)
    {
        builder.Append(prefix);

        bool first = true;
        foreach (var obj in list)
        {
            if (!first) builder.Append(", ");
            first = false;
            WriteObject(builder, obj);
        }

        builder.Append(suffix);
    }

    private void AddFields(QueryBlock queryBlock, string prevPad, int indent = 0)
    {
        if (queryBlock.IsEmpty)
        {
            return;
        }

        _stringBuilder.AppendLine("{");
        string padding = new(' ', indent);
        
        // Sort fields by their string representation or QueryBlock properties
        var orderedFields = queryBlock.FieldsList
            .OrderBy(field => field switch
            {
                QueryBlock block => block.Alias ?? block.Name,
                string str => str,
                _ => field.ToString()
            }, StringComparer.OrdinalIgnoreCase);

        foreach (var field in orderedFields)
        {
            switch (field)
            {
                case string strValue:
                    _stringBuilder.Append(padding);
                    _stringBuilder.AppendLine(strValue);
                    break;
                case QueryBlock subQuery:
                    QueryTextBuilder builder = new();
                    _stringBuilder.AppendLine(builder.Build(subQuery, indent));
                    break;
            }
        }

        _stringBuilder.Append(prevPad);
        _stringBuilder.Append('}');
    }

    private void AddArguments(QueryBlock queryBlock, bool isRootElement)
    {
        var arguments = queryBlock.GetArguments(isRootElement);

        if (arguments.Count == 0)
        {
            return;
        }

        _stringBuilder.Append('(');

        bool first = true;
        foreach (var (key, value) in arguments)
        {
            if (!first) _stringBuilder.Append(", ");
            first = false;
            if (value is Variable variable)
            {
                variable.Print(_stringBuilder, key, isRootElement);
                continue;
            }

            _stringBuilder.Append(key);
            _stringBuilder.Append(':');

            WriteObject(_stringBuilder, value);
        }

        _stringBuilder.Append(')');
    }
}
