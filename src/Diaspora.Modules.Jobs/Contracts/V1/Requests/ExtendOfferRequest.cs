namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// Drafts and extends an offer.
/// <para>
/// <b>The salary is required here, unlike on a posting.</b> A candidate being asked to accept a
/// job — very often to move country for it — is entitled to the number in writing, and an offer
/// that permitted "to be discussed" would be the platform lending its record-keeping to the
/// oldest trick in recruitment.
/// </para>
/// <para>
/// <see cref="ExpiresAt"/> is enforced by the clock. An offer with no deadline leaves a candidate
/// unable to tell whether it still stands and the employer's pipeline permanently open.
/// </para>
/// </summary>
public sealed record ExtendOfferRequest
{
    public required decimal SalaryAmount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary><c>Yearly</c>, <c>Monthly</c>, <c>Hourly</c> and so on.</summary>
    public required string SalaryPeriod { get; init; }

    /// <summary>The proposed first day. Null when it is still to be agreed.</summary>
    public DateOnly? StartDate { get; init; }

    /// <summary>When the offer lapses if unanswered. Must be in the future.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public string? BenefitsSummary { get; init; }

    public string? Notes { get; init; }

    /// <summary>Whether relocation support is part of the offer, not just the advert.</summary>
    public bool IncludesRelocation { get; init; }

    /// <summary>Whether visa sponsorship is part of the offer, not just the advert.</summary>
    public bool IncludesVisaSponsorship { get; init; }

    /// <summary>
    /// Send it to the candidate straight away. False leaves it a draft for internal approval,
    /// which is what most employers actually need.
    /// </summary>
    public bool ExtendImmediately { get; init; } = true;
}
