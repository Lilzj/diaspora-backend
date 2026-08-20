using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>
/// Is this thing paid for, and until when.
/// <para>
/// <b>The question every consuming surface asks, and the reason this module needs no knowledge of
/// what a listing is.</b> A caller passes a type and an identifier and gets back what has been paid
/// for; nothing here resolves the subject, joins to it or checks that it exists.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PurchasesRead)]
public sealed record GetPlacementsForSubjectQuery : IQuery<IReadOnlyList<PlacementResponse>>
{
    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }
}
