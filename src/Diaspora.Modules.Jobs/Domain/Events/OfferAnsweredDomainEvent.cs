using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when an offer reaches an ending — accepted, rejected, withdrawn or expired.
/// <para>
/// One event for all four, because the consumers are the same: the application moves, the
/// employer's acceptance rate updates, and an acceptance fills a vacancy on the posting.
/// <see cref="Status"/> is what separates them, and <see cref="AnsweredBy"/> is null for an
/// expiry, which nobody decided.
/// </para>
/// <para>
/// <b>The acceptance path is the only thing in the module that fills a vacancy</b>, and doing it
/// through this event rather than inline is what keeps the offer aggregate from reaching into
/// the posting aggregate to mutate it.
/// </para>
/// </summary>
public sealed record OfferAnsweredDomainEvent : DomainEvent
{
    public required JobOfferId OfferId { get; init; }

    public required JobApplicationId ApplicationId { get; init; }

    public required JobPostingId PostingId { get; init; }

    public required EmployerProfileId EmployerId { get; init; }

    public required CandidateProfileId CandidateId { get; init; }

    /// <summary>Accepted, Rejected, Withdrawn or Expired.</summary>
    public required OfferStatus Status { get; init; }

    /// <summary>Null on an expiry, which nobody decided.</summary>
    public UserId? AnsweredBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset AnsweredAt { get; init; }
}
