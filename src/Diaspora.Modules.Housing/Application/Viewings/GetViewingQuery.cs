using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// One viewing, with its history.
/// <para>
/// Scoped to the two parties inside the handler. Staff get no bypass: reading a named person
/// appointments is a different act from moderating a public listing.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ViewingManage)]
public sealed record GetViewingQuery : IQuery<HousingViewingResponse>
{
    public required Guid ViewingId { get; init; }
}
