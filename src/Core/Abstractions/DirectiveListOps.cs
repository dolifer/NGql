using System.Diagnostics.CodeAnalysis;

namespace NGql.Core.Abstractions;

/// <summary>
/// Shared directive-list mutation logic for the two node kinds that carry a <c>List&lt;FieldDirective&gt;</c>
/// — <see cref="FieldDefinition"/> and <see cref="InlineFragmentDefinition"/>. Kept as a single
/// static implementation (rather than duplicated per type) so the dedup/collapse rules — and any
/// future fix to them — apply identically everywhere a directive can be attached.
/// </summary>
internal static class DirectiveListOps
{
    /// <summary>
    /// Append <paramref name="directive"/> to <paramref name="directives"/>. Order is preserved. A
    /// directive that is structurally identical to one already present is skipped. Directives that
    /// differ (name or arguments) are all kept, EXCEPT for the two non-repeatable, spec-constrained
    /// directive names <c>include</c> and <c>skip</c> (GraphQL spec §5.7.3: a non-repeatable
    /// directive may appear at most once per location) — see <see cref="AddOrReplaceIfDirective"/>.
    /// </summary>
    [SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "The plain loop short-circuits without allocating an enumerator on the builder hot path.")]
    public static void Add(ref List<FieldDirective>? directives, FieldDirective directive)
    {
        if (IsNonRepeatableConditional(directive.Name))
        {
            AddOrReplaceIfDirective(ref directives, directive);
            return;
        }

        directives ??= new List<FieldDirective>();
        foreach (var existing in directives)
        {
            if (existing.IsStructurallyEqualTo(directive))
            {
                return;
            }
        }
        directives.Add(directive);
    }

    /// <summary>
    /// <c>@include</c> and <c>@skip</c> are non-repeatable per the GraphQL spec (§5.7.3) — a field
    /// or inline fragment may carry at most one of each. Rather than appending a second occurrence
    /// (which would render spec-invalid GraphQL like <c>@include(if:$a) @include(if:$b)</c>), a
    /// later call with the SAME directive name replaces the earlier one IN PLACE (same list
    /// position), so ordering relative to any other directives is preserved. This is
    /// last-call-wins: calling <c>IncludeIf(a)</c> then <c>IncludeIf(b)</c> keeps only
    /// <c>@include(if:$b)</c>. A structurally identical replacement (same name AND same arguments)
    /// is a no-op, matching the existing dedup behavior for repeatable directives.
    /// </summary>
    [SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "The plain loop finds the replacement index without allocating an enumerator.")]
    private static void AddOrReplaceIfDirective(ref List<FieldDirective>? directives, FieldDirective directive)
    {
        directives ??= new List<FieldDirective>();
        for (var i = 0; i < directives.Count; i++)
        {
            var existing = directives[i];
            if (!string.Equals(existing.Name, directive.Name, StringComparison.Ordinal))
            {
                continue;
            }

            if (existing.IsStructurallyEqualTo(directive))
            {
                return;
            }

            directives[i] = directive;
            return;
        }

        directives.Add(directive);
    }

    /// <summary>
    /// Whether <paramref name="name"/> is one of the two universal client directives constrained by
    /// the GraphQL spec to appear at most once per location. Checked by <see cref="Add"/> so
    /// <c>Directive("include", …)</c> called directly collapses exactly like
    /// <c>IncludeIf(Variable)</c> — there is no entry point that can silently emit two
    /// <c>@include</c>s on one field or fragment.
    /// </summary>
    private static bool IsNonRepeatableConditional(string name)
        => name.Equals("include", StringComparison.Ordinal) || name.Equals("skip", StringComparison.Ordinal);
}
