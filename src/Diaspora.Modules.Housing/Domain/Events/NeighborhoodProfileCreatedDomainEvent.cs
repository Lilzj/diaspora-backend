using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a neighbourhood is first described.
/// <para>
/// <b>Creating a profile does not create a neighbourhood.</b> The place already existed in B4's
/// hierarchy - this event says the platform has started keeping a relocation brief about it. The
/// area identifier is carried so a consumer can join to B4 without joining to Housing.
/// </para>
/// </summary>
public sealed record NeighborhoodProfileCreatedDomainEvent : DomainEvent
{
    public required NeighborhoodProfileId ProfileId { get; init; }

    /// <summary>B4's area. The neighbourhood itself, which this module does not own.</summary>
    public required Guid AreaId { get; init; }

    public required string CountryCode { get; init; }

    public required string Name { get; init; }

    public UserId? CreatedBy { get; init; }
}
