namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// One observation a client wants recorded.
/// <para>
/// <b>There is no user identifier on this request, and its absence is the security model.</b> A
/// client records signals about itself; the caller's identity comes from the token. Accepting one
/// here would let any authenticated member poison anybody else's recommendations, which is the sort
/// of hole that is obvious in hindsight and easy to leave open.
/// </para>
/// <para>
/// <b>Weight is accepted and then clamped.</b> A client may say a view was unusually engaged — a long
/// dwell time, a gallery scrolled to the end — and that is genuine information. It cannot claim more
/// than a client is trusted with, which the signal store enforces rather than the validator.
/// </para>
/// </summary>
public sealed record RecordSignalRequest
{
    /// <summary><c>Viewed</c>, <c>Saved</c>, <c>Searched</c>, and so on.</summary>
    public required string Kind { get; init; }

    /// <summary>What it was about, when it was about a thing.</summary>
    public string? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    /// <summary>A category, tag, skill or search term.</summary>
    public string? Key { get; init; }

    /// <summary>Which vocabulary <see cref="Key"/> is drawn from.</summary>
    public string? KeyKind { get; init; }

    /// <summary>Which surface it happened on.</summary>
    public string? Surface { get; init; }

    /// <summary>Clamped by the signal store. See the class summary.</summary>
    public decimal? Weight { get; init; }

    /// <summary>When it happened. Defaults to the moment it was received.</summary>
    public DateTimeOffset? OccurredAt { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }
}
