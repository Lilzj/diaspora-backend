using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// The member behind one viewing, as the kernel names them.
/// <para>
/// Null when the member service cannot answer. Not answered is not "nobody": the response carries
/// a null name and the client says who asked in other words rather than printing a guess.
/// </para>
/// </summary>
internal static class ViewingSeekers
{
    public static async Task<MemberProfile?> ResolveAsync(
        IMemberProfileService members,
        HousingViewing viewing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(viewing);

        if (!members.IsAvailable)
        {
            return null;
        }

        var resolved = await members.ResolveAsync([viewing.SeekerUserId], cancellationToken);

        return resolved.GetValueOrDefault(viewing.SeekerUserId.Value);
    }
}
