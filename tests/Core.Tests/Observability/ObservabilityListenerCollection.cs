using Xunit;

namespace NGql.Core.Tests.Observability;

/// <summary>
/// Marker for the "ObservabilityListener" test collection. Tests in this collection
/// register process-global ActivityListeners via ActivitySource.AddActivityListener;
/// without a CollectionDefinition the [Collection("...")] attribute is silently
/// ignored and tests parallelize, causing one fixture's listener to leak into another's
/// activity reads (and vice versa for the no-listener fixture).
/// </summary>
[CollectionDefinition("ObservabilityListener", DisableParallelization = true)]
public sealed class ObservabilityListenerCollection
{
}
