﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

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
                    WriteCollection('[', ']', listValue, listValue.Count, builder);
                    break;
                }

                case IDictionary dictValue:
                {
                    WriteCollection('{', '}', dictValue, dictValue.Count, builder);
                    break;
                }

                default:
                {
                    var values = valueType
                        .GetProperties()
                        .ToDictionary(x => x.Name, x => x.GetValue(value));
                    WriteCollection('{', '}', values, values.Count, builder);
                    break;
                }
            }
        }

        private static void WriteCollection(char prefix, char suffix, IEnumerable list, int count, StringBuilder builder)
        {
            builder.Append(prefix);

            foreach (var obj in list)
            {
                WriteObject(builder, obj);
                builder.Append(", ");
            }

            if (count != 0)
            {
                builder.Length -= 2;
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

            foreach (var (key, value) in arguments)
            {
                if (value is Variable variable)
                {
                    variable.Print(_stringBuilder, key, isRootElement);
                    continue;
                }

                _stringBuilder.Append(key);
                _stringBuilder.Append(':');

                WriteObject(_stringBuilder, value);

                _stringBuilder.Append(", ");
            }

            _stringBuilder.Length -= 2;
            _stringBuilder.Append(')');
        }
    }
}
