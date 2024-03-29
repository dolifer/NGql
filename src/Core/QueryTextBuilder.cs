using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGql.Core.Abstractions;

namespace NGql.Core
{
    internal sealed class QueryTextBuilder
    {
        private readonly StringBuilder _stringBuilder = new();
        private const int IndentSize = 4;

        public string Build(QueryBlock queryBlock, int indent = 0, string? prefix = null)
        {
            string pad = new(' ', indent);
            string prevPad = pad;

            if (!string.IsNullOrWhiteSpace(queryBlock.Alias))
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

        public static string BuildQueryParam(object value)
        {
            var builder = new StringBuilder();
            WriteObject(builder, value);
            return builder.ToString();
        }

        private static void WriteObject(StringBuilder builder, object? value)
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

            void WriteCollection(char prefix, char suffix, IEnumerable list)
            {
                builder.Append(prefix);

                var hasValues = false;
                foreach (var obj in list)
                {
                    WriteObject(builder, obj);
                    builder.Append(", ");
                    hasValues = true;
                }

                // strip comma-space if list was not empty
                if (hasValues)
                {
                    builder.Length -= 2;
                }

                builder.Append(suffix);
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
                    WriteCollection('[', ']', listValue);
                    break;
                }

                case IDictionary dictValue:
                {
                    WriteCollection('{', '}', dictValue);
                    break;
                }

                default:
                {
                    var values = valueType
                        .GetProperties()
                        .ToDictionary(x => x.Name, x => x.GetValue(value));
                    WriteCollection('{', '}', values);
                    break;
                }
            }
        }

        private void AddFields(QueryBlock queryBlock, string prevPad, int indent = 0)
        {
            _stringBuilder.AppendLine("{");
            string padding = new(' ', indent);

            foreach (var field in queryBlock.FieldsList)
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
                    default:
                        throw new InvalidOperationException("Unsupported Field type found, must be a `string` or `QueryBlock`");
                }
            }

            _stringBuilder.Append(prevPad);
            _stringBuilder.Append('}');
        }

        private void AddArguments(QueryBlock queryBlock, bool isRootElement)
        {
            var arguments = GetArguments(queryBlock, isRootElement);

            if (arguments.Count == 0)
                return;

            _stringBuilder.Append('(');

            var hasValues = false;
            var printedVariables = new HashSet<string>();
            
            foreach (var (key, value) in arguments)
            {
                if (value is Variable variable && printedVariables.Add(variable.Name))
                {
                    _stringBuilder.Append(isRootElement ? variable.Name : key);
                    _stringBuilder.Append(':');
                    _stringBuilder.Append(isRootElement? variable.Type : variable.Name);
                    _stringBuilder.Append(", ");
                    hasValues = true;
                    continue;
                }
                
                _stringBuilder.Append(key);
                _stringBuilder.Append(':');

                WriteObject(_stringBuilder, value);

                _stringBuilder.Append(", ");
                hasValues = true;
            }

            if (hasValues)
            {
                _stringBuilder.Length -= 2;
            }

            _stringBuilder.Append(')');
        }

        private static IReadOnlyDictionary<string, object> GetArguments(QueryBlock queryBlock, bool isRootElement)
        {
            var arguments = new SortedDictionary<string, object>(StringComparer.Ordinal);

            foreach (var kvp in queryBlock.Arguments)
            {
                var existingArgument = arguments.Values
                    .FirstOrDefault(x => x is Variable v && v.Name == kvp.Key);

                if (existingArgument is not null)
                {
                    continue;
                }
                
                if (kvp.Value is Variable variable)
                {
                    arguments[isRootElement ? variable.Name: kvp.Key] = kvp.Value;
                    continue;
                }
                        
                arguments[kvp.Key] = kvp.Value;
            }

            foreach (var variable in queryBlock.Variables)
            {
                var existingArgument = arguments.Values
                    .FirstOrDefault(x => x is Variable v && v.Name == variable.Name);

                if (existingArgument is null)
                {
                    arguments[variable.Name] = variable;
                }
            }

            return arguments;
        }
    }
}
