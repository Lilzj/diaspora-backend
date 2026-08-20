namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// Terms put in front of a candidate.
/// <para>
/// <see cref="Status"/> is computed against the clock, so an offer past its deadline reads as
/// <c>Expired</c> here even before the sweep has written that down. A candidate must never be
/// shown a live offer they can no longer accept.
/// </para>
/// <para>
/// <see cref="HoursRemaining"/> exists because a deadline rendered as a date leaves the reader to
/// work out the urgency, and the whole point of the field is the urgency.
/// </para>
/// </summary>
public sealed record JobOfferResponse
{
    public required Guid OfferId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required string JobTitle { get; init; }

    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    public required decimal SalaryAmount { get; init; }

    /// <summary>ISO 4217. Never converted.</summary>
    public required string CurrencyCode { get; init; }

    public required string SalaryPeriod { get; init; }

    public DateOnly? StartDate { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Null once the offer is no longer answerable.</summary>
    public double? HoursRemaining { get; init; }

    public string? BenefitsSummary { get; init; }

    public string? Notes { get; init; }

    public required bool IncludesRelocation { get; init; }

    public required bool IncludesVisaSponsorship { get; init; }

    /// <summary>Accounts for the clock. See the record summary.</summary>
    public required string Status { get; init; }

    public DateTimeOffset? ExtendedAt { get; init; }

    public DateTimeOffset? AnsweredAt { get; init; }

    public string? ResponseReason { get; init; }

    /// <summary>The offer this one replaced, when terms were revised.</summary>
    public Guid? SupersedesOfferId { get; init; }

    /// <summary>Whether the candidate can still answer.</summary>
    public required bool CanRespond { get; init; }

    public required int Revision { get; init; }
}
