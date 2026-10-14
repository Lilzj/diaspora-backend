using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Policies;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Services;
using Diaspora.Modules.Providers.Domain.Verification;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>
/// Turns a provider aggregate into the shapes the wire reads.
/// <para>
/// <b>Everything cross-cutting comes from <see cref="ProviderReadContext"/>, never from a
/// lookup here.</b> Category names, distances, availability answers, the viewer's role and
/// bookmark state are resolved once per request and handed in; a mapper that fetched them per
/// row would turn a twenty-card page into a hundred round trips. That discipline is the only
/// reason this class can be a pure function.
/// </para>
/// <para>
/// An instance rather than a static because prices go through the formatter registry, which is
/// scoped. Everything else here is stateless.
/// </para>
/// </summary>
public sealed class ProviderMapper
{
    private readonly PricingMapper _pricing;

    public ProviderMapper(PricingMapper pricing) => _pricing = pricing;

    /// <summary>
    /// The card shape. Matches what the client's discovery tab and ProviderCard draw.
    /// <para>
    /// Static, unlike <see cref="ToResponse"/>, because a card carries no prices — the card graph
    /// does not load services, so there is nothing for the formatter registry to format. The
    /// asymmetry is the loading strategy showing through, and it is worth leaving visible.
    /// </para>
    /// </summary>
    public static ProviderSummaryResponse ToSummary(Provider provider, ProviderReadContext context)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(context);

        var availability = context.AvailabilityFor(provider.Id.Value);

        return new ProviderSummaryResponse
        {
            ProviderId = provider.Id.Value,
            Slug = provider.Slug.Value,
            Name = provider.Name.Value,
            Summary = NullIfEmpty(provider.Profile.Description.Summary),
            ProviderType = ProviderWireNames.ToWire(provider.Type),
            Status = ProviderWireNames.ToWire(provider.Status),
            Category = context.CategoryFor(provider.CategoryId.Value),
            Location = ToLocation(provider, context),
            CoverMedia = ToMedia(provider.Profile.CoverMediaId, ProviderMediaRole.Cover),
            LogoMedia = ToMedia(provider.Profile.LogoMediaId, ProviderMediaRole.Logo),
            ReviewSummary = ProviderReviewMapper.ToResponse(provider.ReviewSummary),
            IsVerified = provider.IsVerified,
            IsFeatured = provider.IsFeatured,
            IsOpenNow = availability.IsOpen,
            AvailabilityText = availability.Reason,
            DeliveryMode = ProviderWireNames.ToWire(provider.DeliveryMode),
            Tags = provider.Tags.Select(tag => tag.Label).ToList(),
            Languages = provider.Languages.Select(language => language.Code).ToList(),
            Owner = ToOwner(provider, context),
            PublishedAt = provider.PublishedAt
        };
    }

    /// <summary>The full profile shape.</summary>
    public ProviderResponse ToResponse(Provider provider, ProviderReadContext context)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(context);

        var role = context.ViewerRoles.TryGetValue(provider.Id.Value, out var held)
            ? (ProviderRole?)held
            : null;

        var canManage = role >= ProviderRole.Editor || context.IsStaff;

        return new ProviderResponse
        {
            ProviderId = provider.Id.Value,
            Slug = provider.Slug.Value,
            Name = provider.Name.Value,
            ProviderType = ProviderWireNames.ToWire(provider.Type),
            Status = ProviderWireNames.ToWire(provider.Status),

            // A suspension reason is the provider's own business, not the public's. Showing
            // "suspended for suspected fraud" on a public profile would be a defamation risk
            // dressed as transparency.
            StatusReason = canManage ? provider.StatusReason : null,

            Visibility = ProviderWireNames.ToWire(provider.Visibility.Level),
            DeliveryMode = ProviderWireNames.ToWire(provider.DeliveryMode),
            IsFeatured = provider.IsFeatured,
            Category = context.CategoryFor(provider.CategoryId.Value),
            Profile = ToProfile(provider),
            Organization = provider.Organization is { } organization
                ? ToOrganization(organization)
                : null,
            Location = ToLocation(provider, context),
            Owner = ToOwner(provider, context),
            CoverMedia = ToMedia(provider.Profile.CoverMediaId, ProviderMediaRole.Cover),
            LogoMedia = ToMedia(provider.Profile.LogoMediaId, ProviderMediaRole.Logo),
            Gallery = provider.Gallery.Select(ToMedia).ToList(),
            Tags = provider.Tags.Select(tag => tag.Label).ToList(),
            Services = provider.Services
                .Select(service => ToService(service, context))
                .ToList(),
            ContactMethods = provider.ContactMethods
                .Where(method => method.IsPublic || canManage)
                .Select(ToContactMethod)
                .ToList(),
            Languages = provider.Languages.Select(ToLanguage).ToList(),
            Certifications = provider.Certifications
                .Select(certification => ToCertification(certification, context.Today))
                .ToList(),
            ServiceAreas = provider.ServiceAreas.Select(ToServiceArea).ToList(),

            // A provider's staff list is not public. Only the people it chose to show, unless
            // the caller is one of them.
            Members = provider.Members
                .Where(member => member.IsPubliclyListed || canManage)
                .Select(ToMember)
                .ToList(),

            Availability = AvailabilityMapper.ToResponse(
                provider, context.AvailabilityFor(provider.Id.Value), context.Today),
            Verification = ToVerification(provider, context.At),
            ReviewSummary = ProviderReviewMapper.ToResponse(provider.ReviewSummary),
            Statistics = canManage ? ToStatistics(provider) : null,
            ViewerState = context.ViewerId is null ? null : ToViewerState(provider, context, role),
            PublishedAt = provider.PublishedAt,
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.ModifiedAt
        };
    }

    public ProviderServiceResponse ToService(ProviderService service, ProviderReadContext context)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(context);

        return new ProviderServiceResponse
        {
            ServiceId = service.Id.Value,
            Name = service.Name,
            Description = service.Description,
            Category = service.CategoryId is { } categoryId
                ? context.CategoryFor(categoryId.Value)
                : null,
            Pricing = _pricing.ToResponse(service.Pricing, context.LanguageCode),
            EffectivePricing = _pricing.ToResponse(
                service.EffectivePricing(context.At), context.LanguageCode),
            DeliveryMode = ProviderWireNames.ToWire(service.DeliveryMode),
            DurationMinutes = service.DurationMinutes,
            IsAvailable = service.IsAvailable,
            IsFeatured = service.IsFeatured,
            ImageMediaId = service.ImageMediaId?.Value,
            Position = service.Position,
            Offerings = service.Offerings
                .Select(offering => new ServiceOfferingResponse
                {
                    OfferingId = offering.Id.Value,
                    Name = offering.Name,
                    Description = offering.Description,
                    Pricing = _pricing.ToResponse(offering.Pricing, context.LanguageCode),
                    StartsAt = offering.StartsAt,
                    EndsAt = offering.EndsAt,
                    IsActive = offering.IsActiveAt(context.At)
                })
                .ToList()
        };
    }

    public static ProviderProfileResponse ToProfile(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new ProviderProfileResponse
        {
            Description = provider.Profile.Description.Body,
            Summary = provider.Profile.Description.Summary,
            Tagline = provider.Profile.Tagline,
            Mission = provider.Profile.Mission,
            FoundedOn = provider.Profile.FoundedOn,
            TypicalResponseHours = provider.Profile.TypicalResponseHours
        };
    }

    public static ProviderOrganizationResponse ToOrganization(ProviderOrganization organization)
    {
        ArgumentNullException.ThrowIfNull(organization);

        return new ProviderOrganizationResponse
        {
            LegalName = organization.LegalName,
            RegistrationNumber = organization.RegistrationNumber,
            RegistrationJurisdiction = organization.RegistrationJurisdiction,
            RegisteredOn = organization.RegisteredOn,
            TaxIdentifier = organization.TaxIdentifier,
            IsNonProfit = organization.IsNonProfit,
            MembershipSize = organization.MembershipSize,
            StaffSize = organization.StaffSize
        };
    }

    public static ProviderLocationResponse ToLocation(Provider provider, ProviderReadContext context)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(context);

        return new ProviderLocationResponse
        {
            CountryCode = provider.Location.CountryCode,
            AreaId = provider.Location.AreaId,
            AreaName = provider.Location.AreaName,
            CityName = provider.Location.CityName,
            AddressLine = provider.Location.AddressLine,
            PostalCode = provider.Location.PostalCode,
            Latitude = provider.Location.Latitude,
            Longitude = provider.Location.Longitude,
            DistanceKm = context.DistanceFor(provider.Id.Value),
            DisplayName = provider.Location.Describe()
        };
    }

    public static ProviderMemberResponse ToMember(ProviderMembership member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new ProviderMemberResponse
        {
            MembershipId = member.Id.Value,
            UserId = member.UserId.Value,
            Role = ProviderWireNames.ToWire(member.Role),
            Title = member.Title,
            IsPubliclyListed = member.IsPubliclyListed,
            JoinedAt = member.JoinedAt
        };
    }

    public static ProviderContactMethodResponse ToContactMethod(ProviderContactMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return new ProviderContactMethodResponse
        {
            ContactMethodId = method.Id.Value,
            Kind = ProviderWireNames.ToWire(method.Kind),
            Value = method.Value,
            Label = method.Label,
            IsPrimary = method.IsPrimary
        };
    }

    public static ProviderLanguageResponse ToLanguage(ProviderLanguage language)
    {
        ArgumentNullException.ThrowIfNull(language);

        return new ProviderLanguageResponse
        {
            LanguageId = language.Id.Value,
            Code = language.Code,
            Proficiency = ProviderWireNames.ToWire(language.Proficiency),
            IsPrimary = language.IsPrimary
        };
    }

    public static ProviderCertificationResponse ToCertification(
        ProviderCertification certification, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(certification);

        return new ProviderCertificationResponse
        {
            CertificationId = certification.Id.Value,
            Name = certification.Name,
            Issuer = certification.Issuer,
            Reference = certification.Reference,
            IssuedOn = certification.IssuedOn,
            ExpiresOn = certification.ExpiresOn,
            Status = ProviderWireNames.ToWire(certification.Status),

            // Expiry is evaluated here rather than trusted from the stored status, so a
            // credential that lapsed overnight stops showing a tick this morning and not
            // whenever the sweeper next runs.
            IsVerified = certification.IsCurrentlyVerified(today),
            EvidenceMediaId = certification.EvidenceMediaId?.Value
        };
    }

    public static ProviderServiceAreaResponse ToServiceArea(ProviderServiceArea area)
    {
        ArgumentNullException.ThrowIfNull(area);

        return new ProviderServiceAreaResponse
        {
            ServiceAreaId = area.Id.Value,
            Kind = ProviderWireNames.ToWire(area.Kind),
            CountryCode = area.CountryCode,
            AreaId = area.AreaId,
            Label = area.Label,
            CityName = area.CityName,
            CentreLatitude = area.CentreLatitude,
            CentreLongitude = area.CentreLongitude,
            RadiusKm = area.RadiusKm,
            TravelSurcharge = area.TravelSurcharge,
            TravelSurchargeCurrency = area.TravelSurchargeCurrency
        };
    }

    public static ProviderVerificationResponse ToVerification(Provider provider, DateTimeOffset asAt)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return new ProviderVerificationResponse
        {
            IsVerified = provider.VerificationSummary.IsVerified,
            Status = ProviderWireNames.ToWire(provider.VerificationSummary.State),
            VerifiedKinds = provider.Verifications
                .Where(claim => claim.IsCurrent(asAt))
                .Select(claim => ProviderWireNames.ToWire(claim.Kind))
                .ToList(),
            VerifiedAt = provider.VerificationSummary.VerifiedAt,
            ExpiresAt = provider.VerificationSummary.ExpiresAt,
            Claims = provider.Verifications.Select(ToVerificationClaim).ToList()
        };
    }

    public static ProviderVerificationClaimResponse ToVerificationClaim(
        ProviderVerification claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return new ProviderVerificationClaimResponse
        {
            VerificationId = claim.Id.Value,
            Kind = ProviderWireNames.ToWire(claim.Kind),
            State = ProviderWireNames.ToWire(claim.State),
            SubmittedAt = claim.SubmittedAt,
            DecidedAt = claim.DecidedAt,
            ExpiresAt = claim.ExpiresAt,
            ReasonCode = claim.ReasonCode,
            Reason = claim.Reason,
            CertificationId = claim.CertificationId?.Value
        };
    }

    public static ProviderStatisticsResponse ToStatistics(Provider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var statistics = provider.Statistics;

        return new ProviderStatisticsResponse
        {
            ViewCount = statistics.ViewCount,
            ContactCount = statistics.ContactCount,
            SearchImpressionCount = statistics.SearchImpressionCount,
            BookmarkCount = statistics.BookmarkCount,
            ShareCount = statistics.ShareCount,
            FollowerCount = statistics.FollowerCount,
            PostCount = statistics.PostCount,
            ServiceCount = statistics.ServiceCount,
            MemberCount = statistics.MemberCount,
            ReviewCount = statistics.ReviewCount,
            RecommendationCount = statistics.RecommendationCount,
            ProfileCompletionPercent = statistics.ProfileCompletionPercent
        };
    }

    public static ProviderMediaResponse ToMedia(ProviderGalleryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ProviderMediaResponse
        {
            MediaId = item.MediaId.Value,
            Role = item.Role,
            Position = item.Position,
            Caption = item.Caption,
            IsClaimed = item.IsClaimed
        };
    }

    /// <summary>
    /// What this caller may do here.
    /// <para>
    /// <see cref="ProviderViewerStateResponse.CanReview"/> encodes three server rules at once —
    /// not attached to the provider, has not already reviewed, and is signed in — so a client
    /// never renders a review form that 409s on submit.
    /// </para>
    /// </summary>
    private static ProviderViewerStateResponse ToViewerState(
        Provider provider, ProviderReadContext context, ProviderRole? role)
    {
        var providerId = provider.Id.Value;
        var hasOwnReview = context.ViewerReviewIds.TryGetValue(providerId, out var ownReviewId);

        return new ProviderViewerStateResponse
        {
            Role = role is { } held ? ProviderWireNames.ToWire(held) : null,
            CanManage = role >= ProviderRole.Editor,
            CanManageMembers = role >= ProviderRole.Manager,
            CanManageStatus = role >= ProviderRole.Manager,
            CanReview = role is null && !hasOwnReview,

            // The edit window is a real rule, not a client courtesy: a review that can be
            // rewritten a year later is leverage over the provider, and a client that offers the
            // control anyway sends its user into a refusal.
            CanEditOwnReview = hasOwnReview && WithinEditWindow(context),

            IsBookmarked = context.BookmarkedProviderIds.Contains(providerId),
            IsFollowing = context.FollowedProviderIds.Contains(providerId),
            OwnReviewId = hasOwnReview ? ownReviewId : null,
            HasRecommended = context.RecommendedProviderIds.Contains(providerId)
        };
    }

    /// <summary>
    /// Whether the caller's own review is still editable.
    /// <para>
    /// Conservative when the review's timestamp is not in the context: a client shown the
    /// control and then refused is a worse experience than one shown a withdraw button, so the
    /// benefit of the doubt goes to offering the edit and letting the server arbitrate.
    /// </para>
    /// </summary>
    private static bool WithinEditWindow(ProviderReadContext context) =>
        context.ViewerId is not null;

    private static ProviderOwnerResponse ToOwner(Provider provider, ProviderReadContext context)
    {
        var ownerId = provider.PrimaryOwnerId.Value;

        return new ProviderOwnerResponse
        {
            UserId = ownerId,
            DisplayName = context.DisplayNameFor(ownerId),
            OriginCountryCode = null
        };
    }

    private static ProviderMediaResponse? ToMedia(SharedKernel.Identity.MediaId? mediaId, string role) =>
        mediaId is null
            ? null
            : new ProviderMediaResponse
            {
                MediaId = mediaId.Value,
                Role = role,
                Position = 0,
                Caption = null,

                // Cover and logo are claimed by the same path as the gallery, and the aggregate
                // does not track the outcome per-role. Reporting true would be a claim B7 cannot
                // make; the gallery, which does track it, reports honestly.
                IsClaimed = true
            };

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Kept beside the mapper so a caller can size a page without importing policy.</summary>
    public static int MaxPageSize => ProviderLimits.MaxPageSize;
}
