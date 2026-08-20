namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// A batch of observations from a client.
/// <para>
/// <b>Batched rather than one call per signal, and the arithmetic is the reason.</b> A single
/// home-feed impression produces a view signal per card; twenty HTTP requests for one scroll would
/// be the platform's highest-volume endpoint by an order of magnitude, on mobile connections that
/// are expensive in the countries this product serves.
/// </para>
/// </summary>
public sealed record RecordSignalsRequest
{
    public required IReadOnlyList<RecordSignalRequest> Signals { get; init; }
}
