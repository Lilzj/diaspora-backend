namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A landlord track record, as the platform observed it.
/// <para>
/// Nothing here is self-reported. <c>NoShowCount</c> is present for the landlord and for staff and
/// absent for everybody else: one missed Saturday is a burst tyre, and a public count of it would
/// be a shaming number rather than a safety signal.
/// </para>
/// </summary>
public sealed record LandlordStatisticsResponse
{
    public required int ActiveListings { get; init; }

    public required int TotalListings { get; init; }

    /// <summary>Properties somebody actually moved into. The figure that means the landlord is real.</summary>
    public required int CompletedLets { get; init; }

    /// <summary>
    /// The share of viewing requests answered either way. Null until there are enough requests for
    /// the fraction to mean anything - one ignored message is not a nought per cent response rate.
    /// </summary>
    public double? ResponseRate { get; init; }

    /// <summary>Present only for the landlord and for staff. See the class summary.</summary>
    public int? NoShowCount { get; init; }

    public DateTimeOffset? LastRespondedAt { get; init; }
}
