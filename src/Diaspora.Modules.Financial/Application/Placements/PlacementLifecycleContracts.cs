using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>
/// One placement of the caller's.
/// <para>
/// The route <c>billing.placement.activated</c> deep-links to and which nothing resolved — a
/// placement outside the first page of <c>/placements/me</c> was unreachable from its own
/// notification.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PurchasesRead)]
public sealed record GetPlacementQuery : IQuery<PlacementResponse>
{
    public required Guid PlacementId { get; init; }
}

/// <summary>
/// Extends a placement rather than opening a second one.
/// <para>
/// <b>The difference the member's money notices.</b> Re-purchasing produces a second placement
/// starting when the first ends, because B15 refuses overlaps; renewing moves the end of the one that
/// exists, which is what the word has always meant.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
[Transactional(Reason = "The purchase, the payment and the extended window land together.")]
[Auditable(Action = "financial.placement.renew")]
public sealed record RenewPlacementCommand : ICommand<MonetizationPurchaseResponse>
{
    public required Guid PlacementId { get; init; }

    /// <summary>Omit to buy the same product again, which is what renewing usually means.</summary>
    public string? ProductCode { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// Stops delivery and stops the clock.
/// <para>
/// The paused days are banked and handed back on resume. Cancelling and re-buying loses them, and
/// presenting that as a pause would misdescribe what the member's money just did.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
[Auditable(Action = "financial.placement.pause")]
public sealed record PausePlacementCommand : ICommand<PlacementResponse>
{
    public required Guid PlacementId { get; init; }
}

/// <summary>Starts delivery again and extends the window by exactly the paused duration.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
[Auditable(Action = "financial.placement.resume")]
public sealed record ResumePlacementCommand : ICommand<PlacementResponse>
{
    public required Guid PlacementId { get; init; }
}

/// <summary>Records whether a placement should renew itself when its window closes.</summary>
[RequirePermission(FinancialPermissions.PurchasesCreate)]
[Auditable(Action = "financial.placement.auto_renew")]
public sealed record SetPlacementAutoRenewCommand : ICommand<PlacementResponse>
{
    public required Guid PlacementId { get; init; }

    public required bool Enabled { get; init; }
}
