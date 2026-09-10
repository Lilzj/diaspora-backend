using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Landlords;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>
/// Turns landlord aggregates into wire responses.
/// <para>
/// <b>Contact details and the no-show count are both viewer-dependent.</b> A phone number on a
/// public listing page is harvested within the hour; a public count of missed viewings is a shaming
/// number rather than a safety signal. Both are passed through the redaction the value object owns,
/// and both are absent by default.
/// </para>
/// </summary>
public static class HousingLandlordMapper
{
    /// <summary>
    /// The placeholder for a listing whose landlord could not be resolved.
    /// <para>
    /// A card with a blank landlord is better than a card that fails to render: a landlord row that
    /// has been closed while a listing survives is rare but real, and a search page should not
    /// return an error because of it.
    /// </para>
    /// </summary>
    public static LandlordSummaryResponse Unknown => new()
    {
        Id = Guid.Empty,
        Slug = string.Empty,
        DisplayName = "Unknown landlord",
        Kind = Domain.Enums.LandlordKind.Individual.ToString(),
        IsVerified = false,

        // A landlord that could not be loaded has no rating to report, which is exactly what
        // null says. Reporting zero stars would libel a landlord over a failed join.
        Rating = null
    };

    public static LandlordSummaryResponse ToSummary(LandlordProfile landlord)
    {
        ArgumentNullException.ThrowIfNull(landlord);

        return new LandlordSummaryResponse
        {
            Id = landlord.Id.Value,
            // Who a message about this reaches. See the field's own summary.
            OwnerUserId = landlord.OwnerUserId.Value,
            Slug = landlord.Slug.Value,
            DisplayName = landlord.Identity.DisplayName,
            Kind = landlord.Identity.Kind.ToString(),
            IsVerified = landlord.IsVerified,
            Badge = landlord.TrustMark.Badge,
            LogoMediaId = landlord.LogoMediaId,
            ProviderId = landlord.ProviderId,

            // Null, not a zeroed rollup, when nobody has reviewed them. See the field's note.
            Rating = landlord.ReviewSummary.HasReviews
                ? landlord.ReviewSummary.ToResponse()
                : null
        };
    }

    public static LandlordProfileResponse ToResponse(
        LandlordProfile landlord,
        bool isOwner,
        bool isStaff,
        bool hasConfirmedViewing)
    {
        ArgumentNullException.ThrowIfNull(landlord);

        var contact = landlord.Contact.Disclose(isOwner, isStaff, hasConfirmedViewing);
        var privileged = isOwner || isStaff;

        return new LandlordProfileResponse
        {
            Id = landlord.Id.Value,
            // Who a message about this reaches. See the field's own summary.
            OwnerUserId = landlord.OwnerUserId.Value,
            Slug = landlord.Slug.Value,
            DisplayName = landlord.Identity.DisplayName,
            Kind = landlord.Identity.Kind.ToString(),

            // The legal name is published for a business, because that is exactly what a member is
            // trying to check before they send a deposit. It is absent for an individual, where it
            // would be a person's full legal name on a public page.
            LegalName = landlord.IsBusiness ? landlord.Identity.LegalName : null,

            PrimaryCountryCode = landlord.PrimaryCountryCode,
            Bio = landlord.Bio,
            LogoMediaId = landlord.LogoMediaId,
            ProviderId = landlord.ProviderId,
            Status = landlord.Status.ToString(),
            SuspensionReason = privileged ? landlord.SuspensionReason : null,

            Trust = new HousingTrustResponse
            {
                State = landlord.TrustMark.State.ToString(),
                IsVerified = landlord.IsVerified,
                Badge = landlord.TrustMark.Badge,
                DecidedAt = landlord.TrustMark.DecidedAt
            },

            Statistics = ToStatistics(landlord, privileged),

            PhoneNumber = contact.PhoneNumber,
            Email = contact.Email,
            WebsiteUrl = contact.WebsiteUrl,

            IsOwner = isOwner,
            Revision = landlord.Revision,
            CreatedAt = landlord.CreatedAt
        };
    }

    /// <param name="includePrivate">
    /// Whether the no-show count is included. True for the landlord and for staff, false for
    /// everybody else — see the class summary.
    /// </param>
    public static LandlordStatisticsResponse ToStatistics(
        LandlordProfile landlord, bool includePrivate)
    {
        ArgumentNullException.ThrowIfNull(landlord);

        return new LandlordStatisticsResponse
        {
            ActiveListings = landlord.Statistics.ActiveListings,
            TotalListings = landlord.Statistics.TotalListings,
            CompletedLets = landlord.Statistics.CompletedLets,
            ResponseRate = landlord.Statistics.ResponseRate,
            NoShowCount = includePrivate ? landlord.Statistics.NoShowCount : null,
            LastRespondedAt = landlord.Statistics.LastRespondedAt
        };
    }
}
