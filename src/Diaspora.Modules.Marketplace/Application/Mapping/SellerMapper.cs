using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Providers.Integration;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>
/// Turns seller aggregates into wire responses.
/// <para>
/// <b>The seller badge is computed here from the aggregate, never read from a column.</b> The
/// aggregate derives it from the declared kind, the live Trust mark and the Providers link; the
/// mapper simply names it. A cached badge would keep saying "Verified Business" after Trust revoked
/// the verification.
/// </para>
/// <para>
/// <b>Contact details are gated on the seller's own preference and on who is asking.</b> A seller
/// who chose in-app messaging is not handing their phone number to every visitor, and a suspension
/// reason is theirs and staff's alone.
/// </para>
/// </summary>
internal static class SellerMapper
{
    /// <summary>The card-sized seller a listing carries.</summary>
    public static SellerSummaryResponse ToSummary(SellerProfile seller)
    {
        ArgumentNullException.ThrowIfNull(seller);

        return new SellerSummaryResponse
        {
            SellerId = seller.Id.Value,
            // Who a message about this reaches. See the field's own summary.
            OwnerUserId = seller.OwnerUserId.Value,
            DisplayName = seller.Identity.DisplayName,
            Slug = seller.Slug.Value,
            SellerType = seller.SellerType.ToString(),
            IsVerified = seller.IsVerified,
            Badge = seller.TrustMark.Badge,
            ProviderId = seller.ProviderId,
            LogoMediaId = seller.LogoMediaId,
            CompletedSales = seller.Statistics.SoldListings,
            CancellationRate = seller.Statistics.CancellationRate,
            AverageResponseMinutes = seller.Statistics.AverageResponseMinutes,
            MemberSince = seller.CreatedAt,

            // Null, not a zeroed rollup, when nobody has reviewed them. See the field's note.
            Rating = seller.ReviewSummary.HasReviews
                ? seller.ReviewSummary.ToResponse()
                : null
        };
    }

    /// <summary>
    /// A placeholder for a listing whose seller could not be loaded.
    /// <para>
    /// A card with an empty seller is worse than one that says the seller is unavailable, and both
    /// are far better than a results page that fails because one row's join came back empty.
    /// </para>
    /// </summary>
    public static SellerSummaryResponse UnknownSeller(Guid sellerId) => new()
    {
        SellerId = sellerId,
        DisplayName = string.Empty,
        Slug = string.Empty,
        SellerType = Domain.Enums.SellerType.Individual.ToString(),
        IsVerified = false,
        CompletedSales = 0,
        MemberSince = DateTimeOffset.MinValue,

        // A seller that could not be loaded has no rating to report, which is exactly what
        // null says. Reporting zero stars would libel a seller over a failed join.
        Rating = null
    };

    /// <summary>The full storefront.</summary>
    public static SellerProfileResponse ToResponse(
        SellerProfile seller,
        bool canManage,
        bool isStaff,
        ProviderSummary? provider = null)
    {
        ArgumentNullException.ThrowIfNull(seller);

        var showContact = canManage || seller.Contact.Preference
            is Domain.Enums.ContactPreference.Phone
            or Domain.Enums.ContactPreference.Email
            or Domain.Enums.ContactPreference.Any;

        return new SellerProfileResponse
        {
            SellerId = seller.Id.Value,
            // Who a message about this reaches. See the field's own summary.
            OwnerUserId = seller.OwnerUserId.Value,
            DisplayName = seller.Identity.DisplayName,
            Slug = seller.Slug.Value,
            Kind = seller.Identity.Kind.ToString(),
            SellerType = seller.SellerType.ToString(),
            IsVerified = seller.IsVerified,
            Badge = seller.TrustMark.Badge,
            Status = seller.Status.ToString(),
            Bio = seller.Identity.Bio,

            // The seller's and staff's, for the reason the field's own summary gives: a client
            // updating one thing has to send everything back, and it can only send what it was
            // told. A buyer browsing storefronts sees the trading name and nothing else.
            LegalName = canManage || isStaff ? seller.Identity.LegalName : null,
            RegistrationNumber = canManage || isStaff ? seller.Identity.RegistrationNumber : null,
            CountryCode = seller.PrimaryCountryCode,
            LogoMediaId = seller.LogoMediaId,
            BannerMediaId = seller.BannerMediaId,
            ProviderId = seller.ProviderId,
            ProviderName = provider?.Name,

            // Published only when the seller invited the channel, or to the seller themselves.
            Phone = showContact ? seller.Contact.Phone : null,
            Email = showContact ? seller.Contact.Email : null,
            Website = seller.Contact.Website,
            ContactPreference = seller.Contact.Preference.ToString(),
            Statistics = ToStatistics(seller.Statistics),
            MemberSince = seller.CreatedAt,
            Revision = seller.Revision,
            CanManage = canManage,

            // The reason is the seller's and staff's. A buyer browsing storefronts has no business
            // reading why somebody was suspended.
            SuspensionReason = canManage || isStaff ? seller.SuspensionReason : null
        };
    }

    public static SellerStatisticsResponse ToStatistics(
        Domain.ValueObjects.SellerStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        return new SellerStatisticsResponse
        {
            PublishedListings = statistics.PublishedListings,
            CompletedSales = statistics.SoldListings,
            CompletedReservations = statistics.CompletedReservations,
            CancelledReservations = statistics.CancelledReservations,
            AcceptedOffers = statistics.AcceptedOffers,
            CancellationRate = statistics.CancellationRate,
            AverageResponseMinutes = statistics.AverageResponseMinutes
        };
    }
}
