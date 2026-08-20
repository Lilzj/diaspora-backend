using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when staff give or withdraw featured placement.
/// <para>
/// One event with a flag rather than two, because a ranking consumer's response to both is the
/// same recalculation. <see cref="ChangedBy"/> is null on a withdrawal that came from archival
/// or deletion rather than from a curator — naming the wrong staff member would be worse than
/// naming nobody.
/// </para>
/// </summary>
public sealed record JobPostingFeaturedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>Null when the change was a side effect rather than a curator's decision.</summary>
    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
