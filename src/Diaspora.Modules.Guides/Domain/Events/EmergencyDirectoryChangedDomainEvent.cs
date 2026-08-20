using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A country's emergency information changed.
/// <para>
/// <b>Carries a count and no numbers.</b> An emergency directory is the one payload on this platform
/// where putting the content on the event would be actively dangerous: a stale <c>999</c> replayed
/// from an outbox three days after a country changed it is a message telling somebody to dial a
/// number that no longer answers. A consumer is told that the set moved and reads the current set.
/// </para>
/// </summary>
public sealed record EmergencyDirectoryChangedDomainEvent : DomainEvent
{
    public required CountryEmergencyDirectoryId CountryEmergencyDirectoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>One of the verbs in <see cref="IntelligenceChangeKinds"/>.</summary>
    public required string Change { get; init; }

    /// <summary>How many contacts the country now lists. Never which.</summary>
    public required int ContactCount { get; init; }

    public UserId? ChangedBy { get; init; }
}
