using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when terms go to a candidate.
/// <para>
/// The salary is on the event because the notification has to state it — an offer notification
/// that says only "you have an offer" makes the candidate open an app to find out whether it is
/// worth reading, and for someone deciding whether to move country that is the wrong friction
/// to add. It is a private message to one person, not a broadcast.
/// </para>
/// </summary>
public sealed record OfferExtendedDomainEvent : DomainEvent
{
    public required JobOfferId OfferId { get; init; }

    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    public required decimal SalaryAmount { get; init; }

    public required string CurrencyCode { get; init; }

    public required SalaryPeriod Period { get; init; }

    public DateOnly? StartDate { get; init; }

    /// <summary>When the offer lapses. The notification leads with it.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public UserId? ExtendedBy { get; init; }

    public required DateTimeOffset ExtendedAt { get; init; }
}
