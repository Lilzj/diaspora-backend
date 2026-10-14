using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Moves an event in space, or between in-person and online.
/// <para>
/// <b>The venue and the attendance mode move together, because the aggregate refuses combinations
/// that make no sense.</b> An in-person event with no address is one nobody can get to; an online
/// one with a hall and no link is one nobody can join. Letting a client change either alone would
/// leave the event momentarily in exactly that state.
/// </para>
/// <para>
/// The neighbourhood is resolved against B4 rather than taken from the request, so a moved event
/// inherits the authoritative name, path and country.
/// </para>
/// </summary>
public sealed class RelocateEventCommandHandler
    : ICommandHandler<RelocateEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityEventReadContext _read;

    public RelocateEventCommandHandler(
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityEventReadContext read)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _neighborhoods = neighborhoods;
        _read = read;
    }

    public async Task<Result<EventResponse>> Handle(
        RelocateEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(command.EventId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.EventNotFound);
        }

        var caller = await _authorization.RequireEventWriteAsync(
            communityEvent, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventResponse>(caller.Error);
        }

        var request = command.Request;

        var context = CommunityLifeLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName);

        if (context.IsFailure)
        {
            return Result.Failure<EventResponse>(context.Error);
        }

        var location = await _neighborhoods.ResolveAsync(
            context.Value, request.NeighborhoodAreaId, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<EventResponse>(location.Error);
        }

        if (!await _neighborhoods.CountryExistsAsync(request.CountryCode, cancellationToken))
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.CountryInvalid);
        }

        var venue = EventVenue.Create(
            request.Venue?.Name,
            request.Venue?.AddressLine1,
            request.Venue?.AddressLine2,
            request.Venue?.Landmark,
            request.Venue?.PostalCode,
            request.Venue?.Latitude,
            request.Venue?.Longitude,
            request.Venue?.OnlineUrl);

        if (venue.IsFailure)
        {
            return Result.Failure<EventResponse>(venue.Error);
        }

        var moved = communityEvent.Relocate(
            location.Value,
            venue.Value,
            CommunityLifeSharedMapper.ParseEnum<EventAttendanceMode>(request.AttendanceMode)
                ?? communityEvent.AttendanceMode,
            request.BaseRevision,
            caller.Value);

        if (moved.IsFailure)
        {
            return Result.Failure<EventResponse>(moved.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }
}
