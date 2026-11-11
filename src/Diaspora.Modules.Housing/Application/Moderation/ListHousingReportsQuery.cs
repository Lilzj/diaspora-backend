using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// The housing moderation queue.
/// <para>
/// Scam allegations first by default. A rental advance-fee fraud takes a newcomer deposit before
/// they have a bank account or anybody to ask, and it is the one report type where an hour in the
/// queue matters.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.Moderate)]
public sealed record ListHousingReportsQuery : IQuery<IReadOnlyList<HousingReportResponse>>
{
    public bool OpenOnly { get; init; } = true;

    public bool ScamsFirst { get; init; } = true;

    public Guid? ListingId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
