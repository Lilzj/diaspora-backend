using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>
/// Raised when a node moves in the hierarchy.
/// <para>
/// Rare and consequential. Any module caching a breadcrumb, a slug or a "listings in this
/// area" count has to invalidate on this — which is exactly why it is published rather than
/// left as an internal detail of the path rewrite.
/// </para>
/// </summary>
public sealed record AdministrativeAreaRelocatedDomainEvent : DomainEvent
{
    public required AreaId AreaId { get; init; }

    public required string PreviousPath { get; init; }

    public required string NewPath { get; init; }
}
