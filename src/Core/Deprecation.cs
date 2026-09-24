namespace NGql.Core;

/// <summary>
/// Shared text for <see cref="ObsoleteAttribute"/> on the classic API, so every deprecated type
/// reports the same diagnostic ID, message and link.
/// </summary>
internal static class Deprecation
{
    /// <summary>
    /// Diagnostic ID for the classic API. Callers can suppress only this deprecation with
    /// <c>&lt;NoWarn&gt;NGQL0001&lt;/NoWarn&gt;</c> or <c>#pragma warning disable NGQL0001</c>.
    /// </summary>
    internal const string ClassicApiDiagnosticId = "NGQL0001";

    internal const string ClassicApiMessage =
        "The classic Query/Mutation API is deprecated and will be removed in NGql 3.0. " +
        "Use QueryBuilder.CreateDefaultBuilder, CreateMutationBuilder or CreateSubscriptionBuilder instead.";

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S1075:URIs should not be hardcoded",
        Justification = "Documentation link shown by the compiler; attribute arguments must be constants.")]
    internal const string ClassicApiUrl = "https://github.com/dolifer/NGql/blob/main/docs/reference/MIGRATION.md";
}
