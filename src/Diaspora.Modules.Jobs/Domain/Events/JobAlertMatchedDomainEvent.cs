using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a standing alert has matches to deliver.
/// <para>
/// <b>Once per sweep with a batch, not once per matching posting.</b> A candidate whose alert
/// is "nursing jobs in Germany" may match forty postings in a day, and forty notifications is
/// how a member turns notifications off permanently. The batch and the frequency together are
/// what let the notification layer send one digest.
/// </para>
/// <para>
/// Carries identifiers rather than postings: the notification layer renders from the read model
/// at send time, so a posting withdrawn between the match and the send is never advertised.
/// </para>
/// </summary>
public sealed record JobAlertMatchedDomainEvent : DomainEvent
{
    public required JobAlertId AlertId { get; init; }

    public required UserId UserId { get; init; }

    public required string AlertName { get; init; }

    public required JobAlertFrequency Frequency { get; init; }

    /// <summary>The matching postings. Identifiers only — rendered at send time.</summary>
    public required IReadOnlyList<Guid> PostingIds { get; init; }

    public required DateTimeOffset MatchedAt { get; init; }
}
