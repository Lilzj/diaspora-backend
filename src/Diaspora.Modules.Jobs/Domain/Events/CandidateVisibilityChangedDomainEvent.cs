using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a candidate changes who can see their profile.
/// <para>
/// The one candidate event a search index must act on immediately, and in the strict direction:
/// a candidate who has just made their profile private may be job-hunting while employed, and
/// the gap between their choosing that and the index honouring it is a gap in which their
/// current employer can find them.
/// </para>
/// </summary>
public sealed record CandidateVisibilityChangedDomainEvent : DomainEvent
{
    public required CandidateProfileId CandidateId { get; init; }

    public required UserId UserId { get; init; }

    public required CandidateVisibility PreviousVisibility { get; init; }

    public required CandidateVisibility CurrentVisibility { get; init; }
}
