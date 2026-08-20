using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>A monetization product was added, repriced or retired.</summary>
public sealed record MonetizationCatalogChangedDomainEvent : DomainEvent
{
    public required MonetizationProductId ProductId { get; init; }

    public required string ProductCode { get; init; }

    public required MonetizationProductCategory Category { get; init; }

    public required MonetizationProductStatus Status { get; init; }

    /// <summary><c>created</c>, <c>updated</c>, <c>repriced</c> or <c>retired</c>.</summary>
    public required string ChangeType { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
