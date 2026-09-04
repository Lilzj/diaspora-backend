using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Application.Communities;

/// <summary>
/// The one projection from the community aggregate to the wire.
/// <para>
/// <b>One place, because the directory and the profile must not disagree.</b> A row in the list and
/// the page it opens are the same community; two mappers would eventually differ on a field, and
/// the client seeds its per-community cache from list responses precisely so opening a profile
/// costs no request — a difference would show as a page that changed under the reader.
/// </para>
/// <para>
/// <b>Every enum crosses as its name, never its number.</b> The client switches on
/// <c>'NationalDiaspora'</c> and <c>'Suspended'</c>; a number would make reordering the enum a
/// silent client bug, and the storage layer already writes names for the same reason.
/// </para>
/// </summary>
internal static class CommunityMapper
{
    /// <summary>
    /// Projects a community.
    /// <para>
    /// <b><paramref name="reportMemberCount"/> is the caller's decision, not this mapper's.</b>
    /// Whether a community will say how big it is depends on who is asking, and the mapper does not
    /// know who is asking. Withheld means the field is absent — never zero, which would be a
    /// statement rather than a silence.
    /// </para>
    /// </summary>
    public static CommunityResponse ToResponse(
        CommunityAggregate community, bool reportMemberCount = true)
    {
        ArgumentNullException.ThrowIfNull(community);

        return new CommunityResponse
        {
            Id = community.Id.Value,
            Slug = community.Slug.Value,
            Name = community.Name,
            Type = community.Type.ToString(),
            Status = community.Status.ToString(),
            Visibility = community.Visibility.ToString(),
            Tagline = community.Tagline,
            Description = community.Description,
            PrimaryLanguage = community.PrimaryLanguage,
            SupportedLanguages = [.. community.SupportedLanguages],
            PresenceCountryCodes = [.. community.PresenceCountryCodes],
            CreatedAt = community.CreatedAt,
            UpdatedAt = community.ModifiedAt,
            StatusReason = community.StatusReason,

            MemberCount = reportMemberCount
                ? SourcedResponse<int>.Backed(community.MemberCount)
                : null,

            Origin = new CommunityOriginResponse
            {
                CountryCode = community.OriginCountryCode,
                RegionName = community.OriginRegionName,

                // Left for the device, which already holds all 192 names and flags. See
                // CommunityOriginResponse.
                CountryName = null,
                Flag = null
            },

            Identity = new CommunityIdentityResponse
            {
                LogoMediaId = community.LogoMediaId,
                IconName = community.IconName,
                AccentColor = community.AccentColor
            }
        };
    }

    /// <summary>
    /// Projects one directory row, with the caller's own standing against it.
    /// <para>
    /// <b>A membership row is not a standing.</b> Somebody who left, was declined or was revoked
    /// keeps their row, so the state is read from it rather than inferred from its existence —
    /// which is the difference between offering Join and offering Leave to the wrong person.
    /// </para>
    /// </summary>
    public static CommunityDirectoryEntryResponse ToEntry(
        CommunityAggregate community,
        CommunityMembership? membership,
        bool reportMemberCount = true,
        string? reason = null) =>
        new()
        {
            Community = ToResponse(community, reportMemberCount),
            MembershipState = (membership?.State ?? CommunityMembershipState.None).ToString(),
            IsPrimary = membership?.IsPrimary ?? false,
            Reason = reason
        };
}
