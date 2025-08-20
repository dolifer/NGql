using System.Collections.Generic;

namespace NGql.Core.Abstractions;

/// <summary>
/// Result of a query merge operation
/// </summary>
/// <param name="QueryMap">Dictionary mapping original query names to their field paths</param>
/// <param name="UpdatedFields">Collection of field updates to apply</param>
internal record MergeResult(
    Dictionary<string, string> QueryMap,
    SortedDictionary<string, FieldDefinition> UpdatedFields
);