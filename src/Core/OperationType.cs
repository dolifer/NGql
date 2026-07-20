namespace NGql.Core;

/// <summary>
/// The kind of GraphQL root operation a <see cref="Abstractions.QueryDefinition"/> represents.
/// </summary>
/// <remarks>
/// Internal because the operation type is selected via the appropriate factory on
/// <see cref="Builders.QueryBuilder"/> (e.g. <c>CreateDefaultBuilder</c> for queries,
/// <c>CreateMutationBuilder</c> for mutations) — consumers should not need to construct
/// the enum value directly.
/// </remarks>
internal enum OperationType
{
    /// <summary>
    ///     Renders as <c>query Name { ... }</c>.
    /// </summary>
    Query,

    /// <summary>
    ///     Renders as <c>mutation Name { ... }</c>.
    /// </summary>
    Mutation,

    /// <summary>
    ///     Renders as <c>subscription Name { ... }</c>.
    /// </summary>
    Subscription,
}
