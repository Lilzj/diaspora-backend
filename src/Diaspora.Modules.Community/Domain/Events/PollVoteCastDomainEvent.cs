using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a vote is cast.
/// <para>
/// Carries the option but never the voter's choice history, and is not published as an
/// integration event. A poll result is public; who voted for what is not, and an event stream
/// that leaks it defeats the only privacy property a poll has.
/// </para>
/// </summary>
public sealed record PollVoteCastDomainEvent : DomainEvent
{
    public required PollId PollId { get; init; }

    public required PollOptionId OptionId { get; init; }

    public required UserId VoterId { get; init; }
}
