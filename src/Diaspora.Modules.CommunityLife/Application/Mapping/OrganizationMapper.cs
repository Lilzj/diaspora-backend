using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Integration;

namespace Diaspora.Modules.CommunityLife.Application.Mapping;

/// <summary>
/// Turns an organization into the shapes the wire and the platform expect.
/// <para>
/// <b>Three renderings, and the differences between them are privacy decisions rather than payload
/// sizes.</b> The full response carries the pending-request count only for an administrator; the
/// summary carries no membership detail at all; and the published
/// <see cref="OrganizationSummary"/> carries nothing another module could use to infer who belongs.
/// </para>
/// </summary>
public static class OrganizationMapper
{
    public static OrganizationResponse ToResponse(
        Organization organization,
        OrganizationType? type = null,
        OrganizationMembership? viewerMembership = null,
        bool viewerIsAdministrator = false,
        Guid? ownerUserId = null)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new OrganizationResponse
        {
            OrganizationId = organization.Id.Value,
            Name = organization.Name.Value,
            ShortName = organization.Name.ShortName,
            Slug = organization.Slug.Value,
            OrganizationTypeKey = organization.OrganizationTypeKey,
            OrganizationTypeName = type?.Name,
            Summary = organization.Description.Summary,
            Description = organization.Description.Body,
            Mission = organization.Mission,
            Vision = organization.Vision,
            Location = CommunityLifeSharedMapper.ToResponse(organization.Location) with
            {
                Latitude = organization.Latitude,
                Longitude = organization.Longitude
            },
            Contact = CommunityLifeSharedMapper.ToResponse(organization.Contact),
            SocialLinks = CommunityLifeSharedMapper.ToResponse(organization.SocialLinks),
            Languages = [.. organization.Languages.Select(language => language.Name)],
            Services = [.. organization.Services.Select(service => service.Name)],
            Leaders =
            [
                .. organization.Leaders.Select(leader => new OrganizationLeaderResponse
                {
                    Id = leader.Id,
                    Name = leader.Name,
                    Role = leader.Role,
                    MemberUserId = leader.MemberUserId,
                    PhotoMediaId = leader.PhotoMediaId,
                    Order = leader.Order
                })
            ],
            OperatingHours =
            [
                .. organization.OperatingHours.Select(hour => new OperatingHourResponse
                {
                    Id = hour.Id,
                    Day = hour.Day.ToString(),
                    OpensAt = CommunityLifeSharedMapper.ToWireTime(hour.OpensAt),
                    ClosesAt = CommunityLifeSharedMapper.ToWireTime(hour.ClosesAt),
                    IsClosed = hour.IsClosed,
                    Note = hour.Note
                })
            ],
            Media =
            [
                .. organization.MediaReferences
                    .Where(media => media.Role != CommunityLifeMediaRole.VerificationEvidence)
                    .Select(media => new MediaReferenceResponse
                    {
                        MediaId = media.MediaId,
                        Role = media.Role.ToString(),
                        Order = media.Order,
                        Caption = media.Caption
                    })
            ],
            Guides =
            [
                .. organization.GuideReferences.Select(guide => new GuideReferenceResponse
                {
                    GuideId = guide.GuideId,
                    Title = guide.Title,
                    Slug = guide.Slug
                })
            ],
            LogoMediaId = organization.Logo?.MediaId,
            CoverMediaId = organization.CoverImage?.MediaId,
            Status = organization.Status.ToString(),
            Visibility = organization.Visibility.ToString(),
            ModerationState = organization.ModerationState.ToString(),
            MembershipPolicy = organization.MembershipPolicy.Kind.ToString(),
            MembershipRequirements = organization.MembershipPolicy.Requirements,
            IsMembershipPublic = organization.MembershipPolicy.IsMembershipPublic,
            AcceptsMembership = organization.AcceptsMembership,
            IsVerified = organization.TrustMark.IsVerified,
            VerificationBadge = organization.TrustMark.Badge,
            VerificationState = organization.TrustMark.State.ToString(),
            IsFeatured = organization.IsFeatured,
            Statistics = ToResponse(organization, viewerIsAdministrator),
            ProviderId = organization.ProviderId,

            // Who a message to this organization reaches. An organization is not a user and
            // cannot be the other party in a conversation — see the field's own summary.
            OwnerUserId = ownerUserId,
            ViewerMembershipStatus = viewerMembership?.Status.ToString(),
            ViewerMembershipRole = viewerMembership?.Role.ToString(),
            Completeness = organization.Completeness,
            Revision = organization.Revision,
            PublishedAt = organization.PublishedAt,
            LastActivityAt = organization.LastActivityAt,
            CreatedAt = organization.CreatedAt
        };
    }

    /// <summary>
    /// The counts.
    /// <para>
    /// <see cref="MembershipStatisticsResponse.PendingRequestCount"/> is null for anybody who is not
    /// an administrator. It is not a secret exactly, but it is a number only the people who can act
    /// on it have any use for, and rendering it publicly invites a "why has nobody been approved"
    /// conversation nobody outside the committee can answer.
    /// </para>
    /// </summary>
    public static MembershipStatisticsResponse ToResponse(
        Organization organization, bool viewerIsAdministrator)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new MembershipStatisticsResponse
        {
            MemberCount = organization.Statistics.MemberCount,
            PendingRequestCount = viewerIsAdministrator
                ? organization.Statistics.PendingRequestCount
                : null,
            AdministratorCount = organization.Statistics.AdministratorCount,
            VolunteerCount = organization.Statistics.VolunteerCount,
            PublishedEventCount = organization.Statistics.PublishedEventCount,
            CompletedEventCount = organization.Statistics.CompletedEventCount
        };
    }

    public static OrganizationSummaryResponse ToSummary(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new OrganizationSummaryResponse
        {
            OrganizationId = organization.Id.Value,
            Name = organization.Name.Value,
            ShortName = organization.Name.ShortName,
            Slug = organization.Slug.Value,
            OrganizationTypeKey = organization.OrganizationTypeKey,
            Summary = organization.Description.Summary,
            Location = CommunityLifeSharedMapper.ToResponse(organization.Location) with
            {
                Latitude = organization.Latitude,
                Longitude = organization.Longitude
            },
            LogoMediaId = organization.Logo?.MediaId,
            CoverMediaId = organization.CoverImage?.MediaId,
            IsVerified = organization.TrustMark.IsVerified,
            IsFeatured = organization.IsFeatured,
            AcceptsMembership = organization.AcceptsMembership,
            MemberCount = organization.Statistics.MemberCount,
            PublishedEventCount = organization.Statistics.PublishedEventCount,
            Languages = [.. organization.Languages.Select(language => language.Name)],
            LastActivityAt = organization.LastActivityAt
        };
    }

    /// <summary>The published contract other modules consume. Flat primitives only.</summary>
    public static OrganizationSummary ToPublished(Organization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new OrganizationSummary
        {
            OrganizationId = organization.Id.Value,
            Name = organization.Name.Value,
            ShortName = organization.Name.ShortName,
            Slug = organization.Slug.Value,
            OrganizationTypeKey = organization.OrganizationTypeKey,
            Summary = organization.Description.Summary,
            CountryCode = organization.Location.CountryCode,
            CityName = organization.Location.CityName,
            NeighborhoodAreaId = organization.Location.NeighborhoodAreaId,
            LogoMediaId = organization.Logo?.MediaId,
            CoverMediaId = organization.CoverImage?.MediaId,
            IsVerified = organization.TrustMark.IsVerified,
            Badge = organization.TrustMark.Badge,
            MemberCount = organization.Statistics.MemberCount,
            PublishedEventCount = organization.Statistics.PublishedEventCount,
            AcceptsMembership = organization.AcceptsMembership,
            IsFeatured = organization.IsFeatured,
            ProviderId = organization.ProviderId,
            PublishedAt = organization.PublishedAt,
            LastActivityAt = organization.LastActivityAt
        };
    }

    public static MembershipResponse ToResponse(OrganizationMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        return new MembershipResponse
        {
            MembershipId = membership.Id.Value,
            OrganizationId = membership.OrganizationId.Value,
            MemberUserId = membership.MemberUserId.Value,
            Status = membership.Status.ToString(),
            Role = membership.Role.ToString(),
            RequestMessage = membership.RequestMessage,
            RequestedAt = membership.RequestedAt,
            JoinedAt = membership.JoinedAt,
            EndedAt = membership.EndedAt,
            RenewsAt = membership.RenewsAt,
            AttendedEventCount = membership.AttendedEventCount,
            Revision = membership.Revision
        };
    }
}
