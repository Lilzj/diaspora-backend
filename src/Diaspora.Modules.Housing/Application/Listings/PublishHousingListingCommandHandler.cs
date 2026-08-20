using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Puts a property on the market.
/// <para>
/// <b>The one write path that consults the fraud seam, and the reason it exists.</b> A rental
/// advance-fee scam takes a newcomer's deposit before they have a bank account, a job or anybody to
/// ask; by the time they land the advert is gone. Publication is the last moment the platform can
/// hold one cheaply, so it is where the providers run.
/// </para>
/// <para>
/// <b>Signals are always recorded and only sometimes act.</b> Everything raised goes onto the
/// moderation trail regardless; only a blocking signal stops the publication, and only where the
/// deployment is configured to enforce. That asymmetry is deliberate — there are far more honest
/// landlords with unusual listings than there are fraudsters, and a heuristic that silently refused
/// a genuine listing costs the platform the landlord.
/// </para>
/// <para>
/// <b>Where it lands depends on the property, not on the request.</b> Available when the move-in
/// date has arrived, published when it has not: a flat advertised in March for a June tenancy is
/// discoverable and not yet occupiable, and one status cannot say both.
/// </para>
/// </summary>
public sealed class PublishHousingListingCommandHandler
    : ICommandHandler<PublishHousingListingCommand, HousingListingResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly HousingFraudAssessor _fraud;
    private readonly HousingGuideResolver _guides;
    private readonly HousingListingReadContext _read;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PublishHousingListingCommandHandler(
        IHousingListingRepository listings,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        HousingFraudAssessor fraud,
        HousingGuideResolver guides,
        HousingListingReadContext read,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _fraud = fraud;
        _guides = guides;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        PublishHousingListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _listings.GetAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound);
        }

        var access = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        if (access.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(access.Error);
        }

        var now = _clock.UtcNow;
        var request = command.Request;

        // The move-in date is often fixed as part of publishing, which is exactly when a landlord
        // knows it. Forcing a separate call would leave the common case as two round trips.
        if (request?.AvailableFrom is { } availableFrom)
        {
            var window = listing.SetAvailabilityWindow(
                availableFrom,
                listing.Availability.AvailableUntil,
                listing.Availability.MinimumStayNights,
                listing.Availability.MaximumStayNights,
                baseRevision: null);

            if (window.IsFailure)
            {
                return Result.Failure<HousingListingResponse>(window.Error);
            }
        }

        if (!listing.IsPublishable)
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.ListingNotPublishable);
        }

        var assessment = await _fraud.AssessAsync(listing, access.Value, cancellationToken);

        if (assessment.ToModerationNote() is { Length: > 0 } note)
        {
            // Recorded whatever the outcome. A moderator reviewing this listing next month should
            // see what the providers thought at the moment it went live.
            listing.AddModerationNote(
                _guids.NewSequentialGuid(), access.Value.OwnerUserId, note, now);
        }

        if (assessment.ShouldBlock(_options.Value.EnforceFraudSignals))
        {
            listing.Hide("Held pending review.", moderatorId: null, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<HousingListingResponse>(HousingErrors.SuspectedFraud);
        }

        var visibility = Enum.TryParse<HousingVisibility>(
            request?.Visibility, ignoreCase: true, out var parsed)
            ? parsed
            : HousingVisibility.Public;

        var published = listing.Publish(now, _authorization.CallerId);

        if (published.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(published.Error);
        }

        if (visibility != HousingVisibility.Public)
        {
            listing.SetVisibility(visibility, baseRevision: null);
        }

        // Contextual guides are attached at publication rather than at creation, because the
        // property's neighbourhood and country are settled by now and those are what choose them.
        var guides = await _guides.ResolveForAsync(listing, cancellationToken);

        if (guides.Count > 0)
        {
            listing.SetGuideReferences(guides);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.ListingsPublished.Add(
            1,
            new KeyValuePair<string, object?>("country", listing.Location.CountryCode),
            new KeyValuePair<string, object?>("type", listing.AccommodationTypeKey));

        var response = await _read.ToResponseAsync(listing, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
