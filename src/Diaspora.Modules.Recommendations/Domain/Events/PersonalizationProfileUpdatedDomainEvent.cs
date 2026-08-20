using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Events;

/// <summary>
/// A member's derived profile changed.
/// <para>
/// <b>Carries no interests and no affinities, and that omission is the privacy decision of the
/// module.</b> A profile is the most inferential thing the platform holds about somebody — what the
/// system believes they want, assembled from what they looked at rather than from anything they
/// said. Publishing its contents on a bus every module reads would make this module the platform's
/// largest unnecessary disclosure surface, which is the mistake B13 was careful not to make with
/// notification text.
/// </para>
/// <para>
/// What travels is shape: how complete the profile is, whether the member is still a cold start, how
/// many signals are behind it. Enough for a consumer to decide whether to ask, never enough to know.
/// </para>
/// </summary>
public sealed record PersonalizationProfileUpdatedDomainEvent : DomainEvent
{
    public required PersonalizationProfileId ProfileId { get; init; }

    public required UserId UserId { get; init; }

    /// <summary>0–100. How much of the profile the platform has been able to fill in.</summary>
    public required int Completeness { get; init; }

    public required bool IsColdStart { get; init; }

    public required int SignalCount { get; init; }

    /// <summary>True when the whole profile was recomputed rather than incrementally folded.</summary>
    public required bool WasRebuilt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
