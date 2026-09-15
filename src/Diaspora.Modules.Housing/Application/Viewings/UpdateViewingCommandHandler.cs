using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// Moves a viewing along.
/// <para>
/// <b>Who may do what differs by action, and the differences are not arbitrary.</b> Only the
/// landlord may schedule — it is their property and their time. Either party may cancel, always,
/// because a viewing that could not be called off leaves somebody standing outside a door. Either
/// party may report a no-show, because both directions of that failure matter and only the person
/// who turned up knows which happened.
/// </para>
/// <para>
/// <b>Scheduling is what unlocks the address</b>, which is why it also stamps the landlord's
/// response record: a landlord who answers promptly is the single most useful thing a member can
/// know before they commit to anything.
/// </para>
/// </summary>
public sealed class UpdateViewingCommandHandler
    : ICommandHandler<UpdateViewingCommand, HousingViewingResponse>
{
    private readonly IHousingViewingRepository _viewings;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IMemberProfileService _members;

    public UpdateViewingCommandHandler(
        IHousingViewingRepository viewings,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IMemberProfileService members)
    {
        _members = members;
        _viewings = viewings;
        _listings = listings;
        _landlords = landlords;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingViewingResponse>> Handle(
        UpdateViewingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var viewing = await _viewings.GetAsync(
            HousingViewingId.From(command.ViewingId), cancellationToken);

        if (viewing is null)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.ViewingNotFound);
        }

        var party = await _authorization.RequirePartyAsync(
            viewing.SeekerUserId,
            viewing.LandlordId,
            HousingErrors.NotViewingParty,
            cancellationToken);

        if (party.IsFailure)
        {
            return Result.Failure<HousingViewingResponse>(party.Error);
        }

        var landlord = await _landlords.GetAsync(viewing.LandlordId, cancellationToken);
        var isLandlord = landlord?.OwnerUserId == party.Value;
        var now = _clock.UtcNow;
        var entryId = _guids.NewSequentialGuid();

        var applied = command.Request.Action?.Trim().ToLowerInvariant() switch
        {
            "schedule" => Schedule(viewing, command, party.Value, isLandlord, now, entryId),
            "complete" => viewing.Complete(party.Value, now, entryId),
            "cancel" => viewing.Cancel(party.Value, command.Request.Reason, now, entryId),
            "noshow" or "no-show" => NoShow(viewing, command, party.Value, now, entryId),
            _ => Result.Failure(HousingErrors.ViewingTransitionInvalid)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<HousingViewingResponse>(applied.Error);
        }

        await RecordSideEffectsAsync(viewing, landlord, isLandlord, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var listing = await _listings.GetForReadAsync(viewing.ListingId, cancellationToken);
        var seeker = await ViewingSeekers.ResolveAsync(_members, viewing, cancellationToken);

        return Result.Success(
            HousingRelocationMapper.ToResponse(viewing, listing, landlord, seeker));
    }

    /// <summary>
    /// The landlord agreeing a time.
    /// <para>
    /// Landlord only: a member who could schedule their own viewing would be granting themselves
    /// the address, which is exactly what the whole disclosure design refuses.
    /// </para>
    /// </summary>
    private static Result Schedule(
        HousingViewing viewing,
        UpdateViewingCommand command,
        SharedKernel.Identity.UserId actorId,
        bool isLandlord,
        DateTimeOffset now,
        Guid entryId)
    {
        if (!isLandlord)
        {
            return Result.Failure(HousingErrors.NotViewingParty);
        }

        if (command.Request.ScheduledFor is not { } scheduledFor)
        {
            return Result.Failure(HousingErrors.ViewingWindowInvalid);
        }

        return viewing.Schedule(
            actorId, scheduledFor, command.Request.Place, now, entryId);
    }

    /// <summary>
    /// Recording that nobody turned up.
    /// <para>
    /// The reporting party is asked which side failed rather than having it inferred from who is
    /// calling, because a landlord reporting their own no-show is exactly the honest case the
    /// record should be able to hold.
    /// </para>
    /// </summary>
    private static Result NoShow(
        HousingViewing viewing,
        UpdateViewingCommand command,
        SharedKernel.Identity.UserId actorId,
        DateTimeOffset now,
        Guid entryId)
    {
        if (command.Request.LandlordFailedToAttend is not { } landlordFailed)
        {
            return Result.Failure(HousingErrors.ViewingTransitionInvalid);
        }

        return viewing.RecordNoShow(
            actorId, landlordFailed, command.Request.Reason, now, entryId);
    }

    /// <summary>
    /// Updates the landlord record and the analytics from whatever the transition turned out to be.
    /// <para>
    /// Read from the aggregate's resulting state rather than from the requested action, so a
    /// transition the aggregate refused or reinterpreted cannot leave the counters describing
    /// something that did not happen.
    /// </para>
    /// </summary>
    private async Task RecordSideEffectsAsync(
        HousingViewing viewing,
        LandlordProfile? landlord,
        bool isLandlord,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var analytics = await _analytics.GetByListingAsync(viewing.ListingId, cancellationToken);

        switch (viewing.Status)
        {
            case Domain.Enums.HousingViewingStatus.Scheduled:
                landlord?.RecordViewingAnswered(now);

                HousingTelemetry.ViewingsScheduled.Add(1);

                if (viewing.Window.PreferredAt is null)
                {
                    break;
                }

                HousingTelemetry.ViewingResponseHours.Record(
                    (now - viewing.CreatedAt).TotalHours);

                break;

            case Domain.Enums.HousingViewingStatus.Completed:
                analytics?.RecordViewingCompleted(now);

                HousingTelemetry.ViewingsCompleted.Add(1);
                break;

            case Domain.Enums.HousingViewingStatus.NoShow:
                if (viewing.LandlordFailedToAttend == true)
                {
                    landlord?.RecordNoShow();
                }

                HousingTelemetry.ViewingNoShows.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        "party", viewing.LandlordFailedToAttend == true ? "landlord" : "seeker"));
                break;

            case Domain.Enums.HousingViewingStatus.Cancelled when isLandlord:
                // A landlord declining promptly is still a response, and the record must show it.
                // Otherwise a prompt refusal counts against them exactly as silence does.
                landlord?.RecordViewingAnswered(now);
                break;

            default:
                break;
        }
    }
}
