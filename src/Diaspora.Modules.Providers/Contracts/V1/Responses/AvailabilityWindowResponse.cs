namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One period a provider is open.
/// <para>
/// Times are <c>HH:mm</c> strings — wall-clock, in the provider's own zone, which
/// <c>ProviderAvailabilityResponse.timeZoneId</c> names. Not an instant, because "we open at
/// nine" is not an instant, and serialising it as one would make the value wrong twice a year in
/// every country that changes its clocks.
/// </para>
/// <para>
/// <see cref="CrossesMidnight"/> is surfaced rather than left for the client to infer: a bar open
/// 20:00–02:00 has a closing time numerically before its opening time, and a client that renders
/// the pair naively shows an empty range.
/// </para>
/// </summary>
public sealed record AvailabilityWindowResponse
{
    public required Guid WindowId { get; init; }

    /// <summary><c>HH:mm</c>, wall-clock.</summary>
    public required string OpensAt { get; init; }

    /// <summary><c>HH:mm</c>, wall-clock.</summary>
    public required string ClosesAt { get; init; }

    /// <summary>What the provider calls this period. "Lunch", "Surgery hours".</summary>
    public string? Label { get; init; }

    /// <summary>True when the period runs past midnight into the following day.</summary>
    public required bool CrossesMidnight { get; init; }
}
