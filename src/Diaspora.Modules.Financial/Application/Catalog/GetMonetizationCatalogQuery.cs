using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Catalog;

/// <summary>
/// What the caller can buy.
/// <para>
/// Returns products the caller is not eligible for, with the reason. A product that silently
/// vanished would leave the member with no idea what to do; "verify your identity to buy this" is a
/// conversion path.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PurchasesRead)]
public sealed record GetMonetizationCatalogQuery : IQuery<IReadOnlyList<MonetizationProductResponse>>
{
    /// <summary>Narrow to what can be bought for one kind of thing.</summary>
    public string? SubjectType { get; init; }

    /// <summary>The specific subject, when checking a one-time-only rule.</summary>
    public Guid? SubjectId { get; init; }

    public string? Category { get; init; }

    public string? CountryCode { get; init; }
}
