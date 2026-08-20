using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Jobs.Contracts.V1.Responses;
using Diaspora.Modules.Jobs.Domain.Offers;

namespace Diaspora.Modules.Jobs.Application.Mapping;

/// <summary>
/// Turns an offer into the shape the wire carries.
/// <para>
/// The status and the remaining time are both computed against <paramref name="now"/> rather than
/// read from a stored flag. An offer whose deadline has passed reads as expired here even before
/// the sweep has written that down — a candidate must never be shown a live offer they can no
/// longer accept.
/// </para>
/// </summary>
public static class OfferMapper
{
    public static JobOfferResponse ToResponse(
        JobOffer offer, string jobTitle, string employerName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(offer);

        return new JobOfferResponse
        {
            OfferId = offer.Id.Value,
            ApplicationId = offer.ApplicationId.Value,
            JobId = offer.PostingId.Value,
            JobTitle = jobTitle,
            EmployerId = offer.EmployerId.Value,
            EmployerName = employerName,

            SalaryAmount = offer.Terms.SalaryAmount,
            CurrencyCode = offer.Terms.CurrencyCode,
            SalaryPeriod = JobWireNames.ToWire(offer.Terms.Period),
            StartDate = offer.Terms.StartDate,
            ExpiresAt = offer.Terms.ExpiresAt,
            HoursRemaining = offer.TimeRemaining(now)?.TotalHours,

            BenefitsSummary = offer.Terms.BenefitsSummary,
            Notes = offer.Terms.Notes,
            IncludesRelocation = offer.Terms.IncludesRelocation,
            IncludesVisaSponsorship = offer.Terms.IncludesVisaSponsorship,

            // Clock-aware. See the class summary.
            Status = JobWireNames.ToWire(offer.StatusAsOf(now)),
            ExtendedAt = offer.ExtendedAt,
            AnsweredAt = offer.AnsweredAt,
            ResponseReason = offer.ResponseReason,
            SupersedesOfferId = offer.SupersedesOfferId?.Value,
            CanRespond = offer.IsAwaitingResponse(now),
            Revision = offer.Revision
        };
    }
}
