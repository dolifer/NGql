using System.Buffers;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using NGql.Core.Abstractions;
using NGql.Core.Caching;

namespace NGql.Core.Extensions;

[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class Helpers
{
    /// <summary>
    /// Adds every <see cref="Variable"/> found in <paramref name="value"/> (nested dictionaries,
    /// lists and object properties included) to <paramref name="variables"/>. The same name with
    /// the same type is declared once; the same name with another type throws before anything is
    /// added, because GraphQL allows one declaration per variable name.
    /// </summary>
    /// <exception cref="ArgumentException">A found variable's name is already declared, in
    /// <paramref name="variables"/> or elsewhere in <paramref name="value"/>, with another type.</exception>
    internal static void ExtractVariablesFromValue(object? value, SortedSet<Variable> variables)
    {
        List<Variable>? found = null;
        ExtractVariablesFromValueCore(value, ref found, null);
        if (found is null) return;

        foreach (var variable in found)
        {
            // A declared variable with the same name and type is not a conflict, and finding it
            // is a set lookup; only a new name/type pair scans for a same-named declaration.
            var conflict = variables.Contains(variable) ? null : FindTypeConflict(variables, variable) ?? FindTypeConflict(found, variable);
            if (conflict is { } existing)
            {
                throw new ArgumentException(VariableTypeConflictMessage(existing, variable));
            }
        }

        foreach (var variable in found)
        {
            variables.Add(variable);
        }
    }

    /// <summary>
    /// Returns a variable in <paramref name="declared"/> with <paramref name="variable"/>'s name
    /// but a different type. Names and types compare the way <see cref="Variable"/> equality does.
    /// </summary>
    internal static Variable? FindTypeConflict(SortedSet<Variable> declared, Variable variable)
    {
        foreach (var existing in declared)
        {
            if (IsTypeConflict(existing, variable)) return existing;
        }
        return null;
    }

    private static Variable? FindTypeConflict(List<Variable> declared, Variable variable)
    {
        foreach (var existing in declared)
        {
            if (IsTypeConflict(existing, variable)) return existing;
        }
        return null;
    }

    private static bool IsTypeConflict(Variable existing, Variable variable)
        => string.Equals(existing.Name, variable.Name, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(existing.Type, variable.Type, StringComparison.OrdinalIgnoreCase);

    internal static string VariableTypeConflictMessage(Variable existing, Variable incoming)
        => $"Variable '{incoming.Name}' is declared as '{existing.Type}' and as '{incoming.Type}'. " +
           "GraphQL allows one declaration per variable name; use one type or rename one of them.";

    private static void ExtractVariablesFromValueCore(object? value, ref List<Variable>? found, HashSet<object>? visited)
    {
        if (value == null)
        {
            return;
        }

        if (value is Variable variable)
        {
            found ??= [];
            found.Add(variable);
            return;
        }

        if (value is IDictionary dict)
        {
            ExtractVariablesFromDictionary(dict, ref found, visited);
            return;
        }

        if (value is IList list)
        {
            ExtractVariablesFromList(list, ref found, visited);
            return;
        }

        if (ShouldExtractFromObjectProperties(value))
        {
            ExtractVariablesFromObjectProperties(value, ref found, visited);
        }
    }

    private static void ExtractVariablesFromDictionary(IDictionary dict, ref List<Variable>? found, HashSet<object>? visited)
    {
        if (dict.Count == 0) return;
        if (visited is not null && !visited.Add(dict)) return; // cycle detected

        // Argument dictionaries are usually Dictionary<string, object?>; its struct enumerator
        // avoids boxing the non-generic IDictionary.Values one.
        if (dict is Dictionary<string, object?> arguments)
        {
            foreach (var val in arguments.Values)
            {
                ExtractVariablesFromChild(dict, val, ref found, ref visited);
            }
            return;
        }

        foreach (var val in dict.Values)
        {
            ExtractVariablesFromChild(dict, val, ref found, ref visited);
        }
    }

    private static void ExtractVariablesFromList(IList list, ref List<Variable>? found, HashSet<object>? visited)
    {
        if (list.Count == 0) return;
        if (visited is not null && !visited.Add(list)) return; // cycle detected

        foreach (var item in list)
        {
            ExtractVariablesFromChild(list, item, ref found, ref visited);
        }
    }

    // A cycle needs a nested container, so the visited set is created only on the first descent
    // into one, seeded with the container being walked. Flat collections never allocate it.
    private static void ExtractVariablesFromChild(object container, object? child, ref List<Variable>? found, ref HashSet<object>? visited)
    {
        if (child is null or Variable || ValueFormatter.IsPrimitiveType(child))
        {
            ExtractVariablesFromValueCore(child, ref found, visited);
            return;
        }

        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance) { container };
        ExtractVariablesFromValueCore(child, ref found, visited);
    }

    private static bool ShouldExtractFromObjectProperties(object obj)
    {
        return obj is not string &&
               obj is not Variable &&
               obj is not QueryBlock &&
               obj is not IDictionary &&
               obj is not IList &&
               !ValueFormatter.IsPrimitiveType(obj);
    }

    private static void ExtractVariablesFromObjectProperties(object obj, ref List<Variable>? found, HashSet<object>? visited)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(obj)) return; // cycle detected
        var properties = TypeMetadataCache.GetObjectProperties(obj.GetType());
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(obj);
            if (propertyValue != null)
            {
                ExtractVariablesFromValueCore(propertyValue, ref found, visited);
            }
        }
    }

    /// <summary>
    /// Generic dictionary merging with recursive support for nested dictionaries.
    /// </summary>
    /// <summary>
    /// Merges two dictionaries with support for nullable values and recursive merging of nested dictionaries.
    /// This is the primary efficient implementation that avoids extra allocations.
    /// </summary>
    /// <param name="existing">The existing dictionary</param>
    /// <param name="update">The dictionary to merge in</param>
    /// <returns>A merged SortedDictionary with nullable values</returns>
    internal static SortedDictionary<string, object?> MergeNullableDictionaries(
        IDictionary<string, object?> existing,
        IDictionary<string, object?> update)
    {
        var result = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        // Copy existing entries
        foreach (var kvp in existing)
        {
            result[kvp.Key] = kvp.Value;
        }

        // Merge update entries with recursive handling for nested dictionaries
        foreach (var (key, newValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) &&
                existingValue is IDictionary<string, object?> existingDict && 
                newValue is IDictionary<string, object?> newDict)
            {
                // Recursively merge nested dictionaries
                result[key] = MergeNullableDictionaries(existingDict, newDict);
            }
            else
            {
                // Override with new value for non-dictionary values or new keys
                result[key] = newValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges metadata dictionaries, handling nullable values appropriately with deep/recursive merging for nested dictionaries.
    /// </summary>
    /// <param name="existing">The existing metadata dictionary</param>
    /// <param name="update">The metadata dictionary to merge in</param>
    /// <returns>A merged Dictionary with nullable values suitable for metadata</returns>
    internal static Dictionary<string, object?> MergeMetadata(
        Dictionary<string, object?>? existing,
        Dictionary<string, object> update)
    {
        if (existing is null || existing.Count == 0) return ConvertToNullable(update);
        if (update.Count == 0) return existing;

        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedMetadataValue(result, key, newValue);
        }

        return result;
    }

    private static Dictionary<string, object?> ConvertToNullable(Dictionary<string, object> source)
    {
        var converted = new Dictionary<string, object?>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source) converted[key] = value;
        return converted;
    }

    /// <summary>
    /// Merges metadata dictionaries where both existing and update can have nullable values.
    /// </summary>
    /// <param name="existing">The existing metadata dictionary</param>
    /// <param name="update">The metadata dictionary to merge in</param>
    /// <returns>A merged Dictionary with nullable values suitable for metadata</returns>
    internal static Dictionary<string, object?> MergeNullableMetadata(
        Dictionary<string, object?>? existing,
        Dictionary<string, object?>? update)
    {
        if (update is null || update.Count == 0)
            return existing ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (existing is null || existing.Count == 0)
            return new Dictionary<string, object?>(update, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedNullableMetadataValue(result, key, newValue);
        }
        return result;
    }

    private static object? MergedNullableMetadataValue(Dictionary<string, object?> target, string key, object? newValue)
    {
        if (target.TryGetValue(key, out var existingValue)
            && existingValue is Dictionary<string, object?> existingDict
            && newValue is Dictionary<string, object?> newDict)
        {
            return MergeNullableMetadataDictionaries(existingDict, newDict);
        }
        return newValue;
    }

    /// <summary>
    /// Deep merges two metadata dictionaries recursively.
    /// </summary>
    /// <param name="existing">The existing dictionary</param>
    /// <param name="update">The dictionary to merge in</param>
    /// <returns>A merged dictionary with deep merging of nested dictionaries</returns>
    private static Dictionary<string, object?> MergeMetadataDictionaries(
        Dictionary<string, object?> existing,
        Dictionary<string, object> update)
    {
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedMetadataValue(result, key, newValue);
        }
        return result;
    }

    private static object? MergedMetadataValue(Dictionary<string, object?> target, string key, object newValue)
    {
        if (target.TryGetValue(key, out var existingValue)
            && existingValue is Dictionary<string, object?> existingNested
            && newValue is Dictionary<string, object> newNested)
        {
            return MergeMetadataDictionaries(existingNested, newNested);
        }
        return newValue;
    }

    private static Dictionary<string, object?> MergeNullableMetadataDictionaries(
        Dictionary<string, object?> existing,
        Dictionary<string, object?> update)
    {
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedNullableMetadataValue(result, key, newValue);
        }
        return result;
    }

    internal static object? SortArgumentValue(object? value)
    {
        if (value is null) return null;
        if (ValueFormatter.IsPrimitiveType(value)) return value;
        return SortNonPrimitive(value);
    }

    private static object? SortNonPrimitive(object value) => value switch
    {
        IDictionary<string, object?> dict => SortDictionary(dict),
        Array arr => SortArrayItems(arr),
        IEnumerable<object> list when !value.GetType().IsArray => SortListItems(list),
        _ => IsDecomposable(value) ? DecomposeToDictionary(value) : value,
    };

    // Nested dictionaries reject keys colliding under OrdinalIgnoreCase rather than silently
    // last-winning — otherwise the rendered query carries different data than the caller supplied.
    private static SortedDictionary<string, object?> SortDictionary(IDictionary<string, object?> dict)
        => ToSortedArguments(dict, rejectCaseCollisions: true);

    /// <summary>
    /// Copies <paramref name="source"/> into a case-insensitive sorted dictionary, normalizing each
    /// value with <see cref="SortArgumentValue"/>. A caller's <see cref="Dictionary{TKey,TValue}"/>
    /// is enumerated through its struct enumerator instead of a boxed interface one.
    /// </summary>
    internal static SortedDictionary<string, object?> ToSortedArguments(IDictionary<string, object?> source, bool rejectCaseCollisions)
        => source is Dictionary<string, object?> dictionary
            ? ToSortedArguments(dictionary.GetEnumerator(), rejectCaseCollisions)
            : ToSortedArguments(source.GetEnumerator(), rejectCaseCollisions);

    private static SortedDictionary<string, object?> ToSortedArguments<TEnumerator>(TEnumerator entries, bool rejectCaseCollisions)
        where TEnumerator : IEnumerator<KeyValuePair<string, object?>>
    {
        var sorted = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            while (entries.MoveNext())
            {
                var (key, value) = entries.Current;
                if (rejectCaseCollisions) sorted.Add(key, SortArgumentValue(value));
                else sorted[key] = SortArgumentValue(value);
            }
        }
        finally
        {
            entries.Dispose();
        }
        return sorted;
    }

    private static object?[] SortArrayItems(Array arr)
    {
        var result = new object?[arr.Length];
        var i = 0;
        foreach (var item in arr)
        {
            result[i++] = SortArgumentValue(item);
        }
        return result;
    }

    /// <summary>True for arbitrary CLR objects whose properties should be reflected into a
    /// sorted dictionary. Excludes types we already format/serialize specially.</summary>
    private static bool IsDecomposable(object obj)
        => obj is not string and not Variable and not QueryBlock and not IDictionary and not IList;

    private static SortedDictionary<string, object?> DecomposeToDictionary(object obj)
    {
        // SortedDictionary orders by its comparer on insert — pre-sorting the properties or
        // staging them in an intermediate Dictionary is wasted work.
        var sorted = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in TypeMetadataCache.GetObjectProperties(obj.GetType()))
        {
            // Add (not the indexer): property names colliding under OrdinalIgnoreCase must
            // throw — reflection order is unspecified, so last-wins would be nondeterministic.
            sorted.Add(property.Name, SortArgumentValue(property.GetValue(obj)));
        }
        return sorted;
    }

    /// <summary>
    /// Efficiently sorts list items in a single pass without double allocations.
    /// Avoids: .Select().ToList() which creates intermediate IEnumerable + List allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<object?> SortListItems(IEnumerable<object> list)
    {
        var result = new List<object?>(list.TryGetNonEnumeratedCount(out var count) ? count : 0);
        foreach (var item in list)
        {
            result.Add(SortArgumentValue(item));
        }
        return result;
    }

    /// <summary>
    /// True when an argument key set on both sides carries different values. A key only one side
    /// sets is no conflict: merging adds it.
    /// </summary>
    internal static bool HaveConflictingArguments(SortedDictionary<string, object?>? existing, SortedDictionary<string, object?>? incoming)
    {
        if (existing is not { Count: > 0 } || incoming is not { Count: > 0 }) return false;

        foreach (var (key, value) in incoming)
        {
            if (existing.TryGetValue(key, out var existingValue) && !AreValuesEqual(existingValue, value)) return true;
        }
        return false;
    }

    /// <summary>
    /// Compares two argument dictionaries for equality.
    /// </summary>
    /// <param name="args1">First argument dictionary</param>
    /// <param name="args2">Second argument dictionary</param>
    /// <returns>True if arguments are equal, false otherwise</returns>
    internal static bool AreArgumentsEqual(SortedDictionary<string, object?>? args1, SortedDictionary<string, object?>? args2)
    {
        var status = CompareArgumentShapes(args1, args2);
        return status switch
        {
            ArgumentShape.Equal => true,
            ArgumentShape.Mismatch => false,
            _ => ArgumentEntriesMatch(args1!, args2!),
        };
    }

    private enum ArgumentShape { Equal, Mismatch, NeedsEntryComparison }

    private static ArgumentShape CompareArgumentShapes(SortedDictionary<string, object?>? a, SortedDictionary<string, object?>? b)
    {
        if (ReferenceEquals(a, b)) return ArgumentShape.Equal;
        if (a is null || b is null) return ArgumentShape.Mismatch;
        if (a.Count != b.Count) return ArgumentShape.Mismatch;
        if (a.Count == 0) return ArgumentShape.Equal;
        return ArgumentShape.NeedsEntryComparison;
    }

    private static bool ArgumentEntriesMatch(SortedDictionary<string, object?> a, SortedDictionary<string, object?> b)
    {
        foreach (var (key, value1) in a)
        {
            if (!b.TryGetValue(key, out var value2)) return false;
            if (!AreValuesEqual(value1, value2)) return false;
        }
        return true;
    }

    // Fixed sentinel hash contributed for any argument value whose type is not on the
    // verified-safe allow list below. Every such value collides into the same bucket, so
    // FindMergeTarget falls back to running the full CanMergeFields check for all of them —
    // correctness over speed. This is what keeps the fingerprint conservative: a hash MUST
    // NEVER cause AreArgumentsEqual-equal values to land in different buckets, and folding
    // an unrecognized shape into one shared bucket guarantees that.
    private const ulong ConservativeValueSentinel = 0x9E3779B97F4A7C15UL;

    // Distinct sentinel for null so "null" never accidentally collides with the fallback
    // bucket used for unrecognized non-null shapes (harmless either way, but keeps hash
    // distribution meaningful when arguments frequently omit optional keys).
    private const ulong NullValueSentinel = 0xD1B54A32D192ED03UL;

    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong CombineHash(ulong hash, ulong value)
    {
        hash = (hash ^ value) * FnvPrime;
        return hash;
    }

    /// <summary>
    /// Computes a conservative fingerprint over a field's own arguments, for use as a cheap
    /// pre-filter bucket key in <c>QueryMerger.FindMergeTarget</c> — NEVER as a merge decision
    /// by itself. The contract this fingerprint must uphold: any two argument dictionaries that
    /// <see cref="AreArgumentsEqual"/> considers equal MUST produce the same fingerprint. A hash
    /// collision between unequal arguments is harmless (costs one extra, already-unmodified
    /// <c>CanMergeFields</c> call); a fingerprint mismatch between equal arguments would be a
    /// silent correctness bug (a genuine merge candidate skipped), so this method is designed to
    /// only ever be "too coarse", never "too fine".
    ///
    /// <see cref="SortedDictionary{TKey,TValue}"/> already enumerates keys in a fixed, deterministic
    /// order (its own comparer), so entries are hashed in that existing order — no extra sort.
    /// </summary>
    internal static ulong ComputeArgumentFingerprint(SortedDictionary<string, object?>? arguments)
    {
        if (arguments is null || arguments.Count == 0) return FnvOffsetBasis;

        var hash = FnvOffsetBasis;
        foreach (var (key, value) in arguments)
        {
            // Keys compare OrdinalIgnoreCase inside the argument dictionary itself (its own
            // comparer) — mirror that here via the case-insensitive hash overload so two keys
            // differing only by case still contribute the same hash contribution.
            hash = CombineHash(hash, (ulong)key.GetHashCode(StringComparison.OrdinalIgnoreCase));
            hash = CombineHash(hash, ComputeValueFingerprint(value));
        }
        return hash;
    }

    /// <summary>
    /// Conservative per-value hash contribution. Only contributes a "real" (equality-consistent)
    /// hash for shapes verified against <see cref="AreValuesEqual"/>'s own comparison rule:
    /// strings (ordinal, matching <c>string.Equals</c>) and CLR value types whose documented
    /// <c>GetHashCode</c>/<c>Equals</c> contract is known to agree (numeric primitives, bool,
    /// char, Guid, DateTime/DateTimeOffset, Enum-derived types, and NGql's own <see cref="EnumValue"/>,
    /// whose <c>Equals</c>/<c>GetHashCode</c> pair is explicitly case-insensitive-consistent).
    /// Everything else — nested <c>IDictionary&lt;string,object?&gt;</c>, <c>IList</c>/arrays,
    /// reflected objects compared via <c>AreObjectsStructurallyEqual</c>, and any other type not
    /// on this allow list — returns the shared <see cref="ConservativeValueSentinel"/> so it is
    /// never split from a value it might structurally equal.
    /// </summary>
    private static ulong ComputeValueFingerprint(object? value)
    {
        if (value is null) return NullValueSentinel;

        switch (value)
        {
            case string s:
                // AreValuesEqual uses value1.Equals(value2) for strings, i.e. ordinal equality —
                // hash ordinally to match, not case-insensitively.
                return CombineHash((ulong)typeof(string).GetHashCode(), (ulong)s.GetHashCode(StringComparison.Ordinal));
            case bool or byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal or char or Guid or DateTime or DateTimeOffset or Enum or NGql.Core.EnumValue:
                // These types' Equals/GetHashCode pairs are the standard, documented .NET (or
                // NGql, for EnumValue) contract: GetHashCode is consistent with Equals. Folding
                // the runtime type into the hash mirrors AreValuesEqual's own
                // `type1 != value2.GetType()` early rejection.
                return CombineHash((ulong)value.GetType().GetHashCode(), (ulong)value.GetHashCode());
            default:
                return ConservativeValueSentinel;
        }
    }

    /// <summary>
    /// Compares two values for equality, using optimized comparison strategies.
    /// </summary>
    /// <param name="value1">First value</param>
    /// <param name="value2">Second value</param>
    /// <returns>True if values are equal, false otherwise</returns>
    private static bool AreValuesEqual(object? value1, object? value2)
    {
        if (ReferenceEquals(value1, value2)) return true;
        if (value1 is null || value2 is null) return false;

        var type1 = value1.GetType();
        if (type1 != value2.GetType()) return false;

        if (type1.IsValueType || value1 is string) return value1.Equals(value2);

        return AreReferenceTypedValuesEqual(value1, value2);
    }

    private static bool AreReferenceTypedValuesEqual(object value1, object value2)
    {
        if (value1 is IDictionary<string, object?> dict1 && value2 is IDictionary<string, object?> dict2)
            return AreDictionariesEqual(dict1, dict2);

        if (value1 is IList list1 && value2 is IList list2)
            return AreListsEqual(list1, list2);

        return AreObjectsStructurallyEqual(value1, value2);
    }

    /// <summary>
    /// Optimized dictionary equality comparison
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreDictionariesEqual(IDictionary<string, object?> dict1, IDictionary<string, object?> dict2)
    {
        if (dict1.Count != dict2.Count) return false;
        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value2)) return false;
            if (!AreValuesEqual(kvp.Value, value2)) return false;
        }
        return true;
    }

    /// <summary>
    /// Optimized list equality comparison
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreListsEqual(IList list1, IList list2)
    {
        var count = list1.Count;
        if (count != list2.Count) return false;
        for (int i = 0; i < count; i++)
        {
            if (!AreValuesEqual(list1[i], list2[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// Structural equality comparison for complex objects using reflection
    /// </summary>
    private static bool AreObjectsStructurallyEqual(object obj1, object obj2)
    {
        var type = obj1.GetType();
        var properties = TypeMetadataCache.GetObjectProperties(type);

        foreach (var property in properties)
        {
            var value1 = property.GetValue(obj1);
            var value2 = property.GetValue(obj2);

            if (!AreValuesEqual(value1, value2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses field type from path if provided in format "Type fieldPath".
    /// </summary>
    /// <param name="fieldPath">Field path to parse</param>
    /// <param name="defaultType">Default type to use if none specified</param>
    /// <param name="type">Parsed type output</param>
    /// <returns>Field path with type removed and trimmed</returns>
    internal static ReadOnlySpan<char> ParseFieldTypeFromPath(ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> defaultType, out ReadOnlySpan<char> type)
    {
        var spaceIndex = fieldPath.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            type = defaultType;
            return fieldPath.TrimEndDotsAndSpaces();
        }

        var potentialType = fieldPath[..spaceIndex];
        if (LooksLikeTypeAnnotation(potentialType))
        {
            type = potentialType;
            fieldPath = fieldPath[(spaceIndex + 1)..];
        }
        else
        {
            type = defaultType;
        }
        return fieldPath.TrimEndDotsAndSpaces();
    }

    // Type annotation must start with a letter or '[', contain no dots, and either include a
    // letter/digit OR be the bare "[]" array marker.
    private static bool LooksLikeTypeAnnotation(ReadOnlySpan<char> candidate)
        => candidate.Length > 0
        && (char.IsLetter(candidate[0]) || candidate[0] == '[')
        && candidate.IndexOf('.') < 0
        && (candidate.HasLetterOrDigit() || candidate.SequenceEqual("[]".AsSpan()));

    /// <summary>
    /// Creates a new FieldDefinition with sorted arguments for consistent behavior.
    /// Normalizes argument values into an independent sorted dictionary.
    /// <param name="name">Field name</param>
    /// <param name="type">Field type</param>
    /// <param name="alias">Optional field alias</param>
    /// <param name="arguments">Field arguments to normalize</param>
    /// <param name="path">Field path for caching</param>
    /// <param name="metadata">Optional field metadata</param>
    /// <returns>New FieldDefinition instance</returns>
    /// </summary>
    internal static FieldDefinition CreateFieldDefinition(ReadOnlySpan<char> name, ReadOnlySpan<char> type, ReadOnlySpan<char> alias, IDictionary<string, object?>? arguments, ReadOnlySpan<char> path, Dictionary<string, object?>? metadata = null)
        => CreateFieldDefinitionCore(name.ToString(), TypeCache.GetInternedType(type),
            alias.IsEmpty ? null : alias.ToString(), arguments, path.ToString(), metadata);

    // Merge inputs already own immutable name/path/alias strings. Reuse those strings while
    // retaining exactly the same type and recursive argument normalization as parsed fields.
    internal static FieldDefinition CreateFieldDefinition(string name, ReadOnlySpan<char> type, string? alias, IDictionary<string, object?>? arguments, string path, Dictionary<string, object?>? metadata = null)
        => CreateFieldDefinitionCore(name, TypeCache.GetInternedType(type),
            string.IsNullOrEmpty(alias) ? null : alias, arguments, path, metadata);

    private static FieldDefinition CreateFieldDefinitionCore(string name, string type, string? alias, IDictionary<string, object?>? arguments, string path, Dictionary<string, object?>? metadata)
    {
        // FAST PATH: Skip dictionary operations when arguments are empty or null
        if (arguments?.Count == 0 || arguments == null)
        {
            return new FieldDefinition(name, type, alias, null)
            {
                Path = path,
                _metadata = metadata
            };
        }

        // Create a new sorted dictionary to ensure consistent argument ordering
        var sortedArguments = ToSortedArguments(arguments, rejectCaseCollisions: false);

        return new FieldDefinition(name, type, alias, sortedArguments)
        {
            Path = path,
            _metadata = metadata
        };
    }

    /// <summary>
    /// Finds an existing field in the collection by name, alias, or path.
    /// </summary>
    /// <param name="fields">Field collection to search</param>
    /// <param name="fieldDefinition">Field definition to find</param>
    /// <returns>Existing field if found, null otherwise</returns>
    internal static FieldDefinition? FindExistingField(Dictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        FieldDefinition? existingField = null;
        foreach (var f in fields.Values)
        {
            if (f.Name == fieldDefinition.Name && f._alias == fieldDefinition._alias)
            {
                existingField = f;
                break;
            }
        }
        return existingField ?? fields.GetValueOrDefault(fieldDefinition.Path);
    }

    internal static FieldDefinition? FindExistingField(FieldChildren children, FieldDefinition fieldDefinition)
    {
        // Shared ordinal (Name, alias) probe — see FindByOrdinalNameAndAlias's own remarks for the
        // fast-path/slow-path rationale. A miss here does NOT prove absence for this caller: unlike
        // FieldDefinitionExtensions.FindChildByNameAndAlias (which returns null and lets its callers
        // treat that as "append a new sibling"), CreateOrMergeField additionally tolerates a child
        // that shares this field's Path under a DIFFERENT (Name, alias) — the Path fallback below is
        // load-bearing for that caller and must not be dropped.
        var byIdentity = FindByOrdinalNameAndAlias(children, fieldDefinition.Name, fieldDefinition._alias);
        return byIdentity ?? children.Find(fieldDefinition.Path.AsSpan());
    }

    /// <summary>
    /// Finds a child matching an exact GraphQL identity — (Name, alias), not Name alone. Two
    /// children may legitimately share a Name while differing by alias (distinct response keys); a
    /// Name-only lookup would wrongly treat one as a match for the other. Shared kernel for
    /// <see cref="FindExistingField(FieldChildren, FieldDefinition)"/> and
    /// <see cref="Extensions.FieldDefinitionExtensions.CanMergeFields"/>'s merge-compatibility
    /// lookups — both need exactly this ordinal probe; what differs between them is what each does
    /// on an outright miss (a Path fallback here, a plain null there), which stays with the caller.
    /// </summary>
    internal static FieldDefinition? FindByOrdinalNameAndAlias(FieldChildren children, string name, string? alias)
    {
        // FAST PATH #1: a NAME-based (case-insensitive) miss via FieldChildren's own Find is
        // strictly broader than this method's ordinal (Name, alias) match — if no child shares the
        // Name even case-insensitively, none can share it ordinally either, so absence here proves
        // absence in the exhaustive scan below too. This is the dominant case for a growing set of
        // never-before-seen sibling names (e.g. Including N distinct fields under one shared
        // parent) and is what turns the old unconditional per-Include linear scan into an O(1)/
        // O(log) index lookup once FieldChildren's index is built.
        var candidate = children.Find(name);
        if (candidate is null)
        {
            return null;
        }

        // FAST PATH #2: a case-insensitive HIT that also matches ordinally (Name) and by alias is
        // exactly the entry a subsequent Include() of the "same" field will hit, since FieldChildren's
        // index always maps a name to its LAST-appended occurrence (see FieldChildren.Append and
        // BuildIndexLocked) — precisely the slot a genuine repeated-merge candidate lives in.
        if (candidate.Name == name && candidate._alias == alias)
        {
            return candidate;
        }

        // SLOW PATH: the index found *a* same-name (case-insensitively) child, but it doesn't match
        // ordinally or by alias — this does NOT prove absence, because two children CAN legitimately
        // share a Name with different aliases (FieldFactory.CreateOrMergeField itself appends such a
        // sibling when the caller's own fallback finds no match) and FieldChildren's index only ever
        // remembers ONE slot per name. Fall back to the exhaustive scan, which remains authoritative
        // and preserves first-match-in-append-order semantics.
        foreach (var f in children.AsSpan())
        {
            if (f.Name == name && f._alias == alias)
                return f;
        }
        return null;
    }

    /// <summary>
    /// Validates that a field name conforms to GraphQL identifier rules: [_A-Za-z][_0-9A-Za-z]*
    /// Handles type-annotation prefixes (e.g. "[User!]! user" → validates "user") and
    /// alias prefixes (e.g. "alias:name" → validates both "alias" and "name").
    /// For dotted paths, validate each segment individually before calling this method.
    /// </summary>
    internal static void ValidateFieldName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
            throw new ArgumentException("Field name cannot be empty.");

        // Strip type annotation prefix: take identifier part after the last space
        var spaceIndex = name.LastIndexOf(' ');
        var identifier = spaceIndex >= 0 ? name[(spaceIndex + 1)..].Trim() : name.Trim();

        // Handle alias:name syntax
        var colonIndex = identifier.IndexOf(':');
        if (colonIndex >= 0)
        {
            var alias = identifier[..colonIndex].Trim();
            var fieldName = identifier[(colonIndex + 1)..].Trim();
            if (!alias.IsEmpty) ValidateIdentifier(alias);
            if (!fieldName.IsEmpty) ValidateIdentifier(fieldName);
            return;
        }

        ValidateIdentifier(identifier);
    }

    private static void ValidateIdentifier(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
            throw new ArgumentException("Field name cannot be empty.");

        if (!IsValidGraphQlNameStart(name[0]))
            throw new ArgumentException($"Invalid GraphQL field name '{name.ToString()}': must start with a letter or underscore.");

        var invalid = name[1..].IndexOfAnyExcept(GraphQlNameChars);
        if (invalid >= 0)
        {
            var position = invalid + 1;
            throw new ArgumentException($"Invalid GraphQL field name '{name.ToString()}': contains invalid character '{name[position]}' at position {position}.");
        }
    }

    private static bool IsValidGraphQlNameStart(char c)
        => c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    // GraphQL name characters (spec §2.1.9); a vectorized search finds the first one outside it.
    private static readonly SearchValues<char> GraphQlNameChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");

    /// <summary>
    /// Writes a collection with specified prefix/suffix characters and custom item writer
    /// </summary>
    internal static void WriteCollection(char prefix, char suffix, IEnumerable list, StringBuilder builder, Action<StringBuilder, object?> itemWriter)
    {
        builder.Append(prefix);

        bool first = true;
        foreach (var obj in list)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            itemWriter(builder, obj);
        }

        builder.Append(suffix);
    }
}
