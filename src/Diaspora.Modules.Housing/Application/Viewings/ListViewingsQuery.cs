using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// The caller viewings, as a member or as a landlord.
/// <para>
/// The landlord side is a diary: which properties are being shown, to whom, and when. The member
/// side is a schedule. Both are private to the caller.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ViewingManage)]
public sealed record ListViewingsQuery : IQuery<IReadOnlyList<HousingViewingResponse>>
{
    /// <summary>Seeker or Landlord. Defaults to the member view.</summary>
    public string? Side { get; init; }

    /// <summary>Only viewings still ahead of the parties.</summary>
    public bool OpenOnly { get; init; } = true;

    public Guid? ListingId { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
