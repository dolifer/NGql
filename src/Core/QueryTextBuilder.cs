using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NGql.Core.Abstractions;

namespace NGql.Core
{
    public sealed class QueryTextBuilder
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

            prefix = !string.IsNullOrWhiteSpace(prefix) ? $"{prefix} " : string.Empty;
            _stringBuilder.Append(pad + prefix + queryBlock.Name);

            AddVariables(queryBlock);

            AddArguments(queryBlock);
            indent += IndentSize;

            AddFields(queryBlock, prevPad, indent);
            return _stringBuilder.ToString();
        }

        public static string? BuildQueryParam(object value)
        {
            if (TryParsePrimitiveType(value, out var stringValue))
                return stringValue;

            string WrapEnumerable(char prefix, char suffix, IEnumerable list)
            {
                StringBuilder builder = new();
                builder.Append(prefix);

                var hasValues = false;
                foreach (var obj in list)
                {
                    builder.Append(BuildQueryParam(obj));
                    builder.Append(", ");
                    hasValues = true;
                }

                // strip comma-space if list was not empty
                if (hasValues)
                {
                    builder.Length -= 2;
                }

                builder.Append(suffix);
                return builder.ToString();
            }

            switch (value)
            {
                case KeyValuePair<string, object>(var key, var o):
                    StringBuilder valueStr = new();
                    valueStr.Append(key);
                    valueStr.Append(':');
                    valueStr.Append(BuildQueryParam(o));
                    return valueStr.ToString();

                case IList listValue:
                    return WrapEnumerable('[', ']', listValue);

                case IDictionary dictValue:
                    return WrapEnumerable('{', '}', dictValue);

                default:
                    throw new InvalidOperationException("Unsupported Query argument type found: " + value.GetType());
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
                        throw new InvalidOperationException("Unsupported Field type found, must be `string` or `IQueryPart`");
                }
            }

            _stringBuilder.Append(prevPad);
            _stringBuilder.Append('}');
        }

        private void AddVariables(QueryBlock queryBlock)
        {
            if (queryBlock.Variables.Count == 0)
                return;

            _stringBuilder.Append('(');

            var hasValues = false;
            foreach (var variable in queryBlock.Variables)
            {
                _stringBuilder.Append(variable.Name);
                _stringBuilder.Append(':');
                _stringBuilder.Append(variable.Type);
                _stringBuilder.Append(", ");
                hasValues = true;
            }

            if (hasValues)
            {
                _stringBuilder.Length -= 2;
            }

            _stringBuilder.Append(')');
        }

        private void AddArguments(QueryBlock queryBlock)
        {
            if (queryBlock.Arguments.Count == 0)
                return;

            _stringBuilder.Append('(');

            var hasValues = false;
            foreach (var (key, value) in queryBlock.Arguments)
            {
                _stringBuilder.Append(key);
                _stringBuilder.Append(':');
                _stringBuilder.Append(BuildQueryParam(value));
                _stringBuilder.Append(", ");
                hasValues = true;
            }

            if (hasValues)
            {
                _stringBuilder.Length -= 2;
            }

            _stringBuilder.Append(')');
        }

        private static bool TryParsePrimitiveType(object value, out string? stringValue)
        {
            stringValue = value switch
            {
                string s => "\"" + s + "\"",
                bool boolValue => boolValue.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
                byte byteValue => byteValue.ToString(CultureInfo.CurrentCulture),
                sbyte sbyteValue => sbyteValue.ToString(CultureInfo.CurrentCulture),
                short shortValue => shortValue.ToString(CultureInfo.CurrentCulture),
                ushort ushortValue => ushortValue.ToString(CultureInfo.CurrentCulture),
                int intValue => intValue.ToString(CultureInfo.CurrentCulture),
                uint uintValue => uintValue.ToString(CultureInfo.CurrentCulture),
                long longValue => longValue.ToString(CultureInfo.CurrentCulture),
                ulong ulongValue => ulongValue.ToString(CultureInfo.CurrentCulture),
                float floatValue => floatValue.ToString(CultureInfo.CurrentCulture),
                double doubleValue => doubleValue.ToString(CultureInfo.CurrentCulture),
                decimal decimalValue => decimalValue.ToString(CultureInfo.CurrentCulture),
                Enum enumValue => enumValue.ToString(),
                Variable variable => variable.Name,
                _ => default
            };

            return !string.IsNullOrWhiteSpace(stringValue);
        }
    }
}
