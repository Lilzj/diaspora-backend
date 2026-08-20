using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Jobs.V1.Events;

/// <summary>
/// Terms were put in front of a candidate.
/// <para>
/// The salary is on the contract because the notification has to state it. An offer message that
/// says only "you have an offer" makes the candidate open an app to find out whether it is worth
/// reading, and for someone deciding whether to move country that is the wrong friction to add.
/// It is a private message to one named person, not a broadcast.
/// </para>
/// <para>
/// <see cref="ExpiresAt"/> is the field the notification should lead with: an offer the candidate
/// does not answer in time lapses, and nobody should discover that after the fact.
/// </para>
/// </summary>
public sealed record OfferExtendedIntegrationEvent : IntegrationEvent
{
    public required Guid OfferId { get; init; }

    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required Guid EmployerId { get; init; }

    public required Guid CandidateId { get; init; }

    /// <summary>
    /// The member, so the notification can be addressed.
    /// <para>
    /// Events published before B13.1 carry <see cref="Guid.Empty"/> here: the relay had no route
    /// from an offer to the applying member and wrote a placeholder. A consumer must treat an
    /// empty value as absent rather than as a member.
    /// </para>
    /// </summary>
    public required Guid CandidateUserId { get; init; }

    public required string JobTitle { get; init; }

    public required string EmployerName { get; init; }

    public required decimal SalaryAmount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted anywhere in this module.</summary>
    public required string CurrencyCode { get; init; }

    /// <summary><c>Yearly</c>, <c>Monthly</c>, <c>Hourly</c> and so on.</summary>
    public required string SalaryPeriod { get; init; }

    public DateOnly? StartDate { get; init; }

    /// <summary>When the offer lapses unanswered. The notification leads with it.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public required DateTimeOffset ExtendedAt { get; init; }

    /// <summary>
    /// The employer's notifiable member — B13.1. The employer's current primary owner.
    /// <para>
    /// The employer extended the offer, so nothing here is addressed to them now. It is carried
    /// so the lapse — an offer nobody answered — has an employer-side recipient without a second
    /// contract, because that outcome is news to both parties and neither caused it.
    /// </para>
    /// </summary>
    public Guid? EmployerUserId { get; init; }
}
