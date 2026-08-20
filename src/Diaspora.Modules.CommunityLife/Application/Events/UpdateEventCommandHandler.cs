using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Edits an event's descriptive content.
/// <para>
/// <b>Null means "leave it alone" throughout</b>, so an organizer correcting a dress code does not
/// round-trip the description and cannot blank it by omitting it.
/// </para>
/// <para>
/// <b>Time, place and capacity are not editable here.</b> Each has its own command because each has
/// its own consequence: a reschedule and a relocation are material changes attendees are notified
/// about, and a capacity change is refused below what is already committed. Folding them in would
/// make a typo fix carry the same weight as moving a wedding.
/// </para>
/// </summary>
public sealed class UpdateEventCommandHandler
    : ICommandHandler<UpdateEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityEventReadContext _read;

    public UpdateEventCommandHandler(
        ICommunityEventRepository events,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCatalogResolver catalog,
        CommunityEventReadContext read)
    {
        _events = events;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _read = read;
    }

    public async Task<Result<EventResponse>> Handle(
        UpdateEventCommand command, CancellationToken cancellationToken)
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

        var applied = await ApplyAsync(
            communityEvent, command.Request, caller.Value, cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<EventResponse>(applied.Error);
        }

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }

    private async Task<Result> ApplyAsync(
        CommunityEvent communityEvent,
        UpdateEventRequest request,
        UserId caller,
        CancellationToken cancellationToken)
    {
        var content = ApplyContent(communityEvent, request, caller);

        if (content.IsFailure)
        {
            return content;
        }

        if (request.Capacity is { } capacity)
        {
            var set = communityEvent.SetCapacity(capacity, request.BaseRevision);

            if (set.IsFailure)
            {
                return set;
            }
        }

        if (CommunityLifeSharedMapper.ParseEnum<EventVisibility>(request.Visibility)
            is { } visibility)
        {
            var set = communityEvent.SetVisibility(visibility, request.BaseRevision);

            if (set.IsFailure)
            {
                return set;
            }
        }

        if (request.CategoryKey is null)
        {
            return Result.Success();
        }

        var category = await _catalog.RequireCategoryAsync(
            request.CategoryKey, cancellationToken: cancellationToken);

        return category.IsFailure
            ? Result.Failure(category.Error)
            : communityEvent.ChangeCategory(category.Value.Key, request.BaseRevision);
    }

    /// <summary>
    /// The descriptive fields, each rebuilt only where the caller sent something for it.
    /// <para>
    /// The audience flags are merged against what the event already says, so a caller ticking
    /// "child friendly" does not silently declare the event no longer free.
    /// </para>
    /// </summary>
    private static Result ApplyContent(
        CommunityEvent communityEvent, UpdateEventRequest request, UserId caller)
    {
        CommunityLifeTitle? title = null;

        if (request.Title is not null)
        {
            var built = CommunityLifeTitle.Create(request.Title);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            title = built.Value;
        }

        CommunityLifeDescription? description = null;

        if (request.Summary is not null || request.Description is not null)
        {
            var built = CommunityLifeDescription.Create(
                request.Summary ?? communityEvent.Description.Summary,
                request.Description ?? communityEvent.Description.Body);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            description = built.Value;
        }

        var audience = BuildAudience(communityEvent, request);

        if (audience.IsFailure)
        {
            return Result.Failure(audience.Error);
        }

        ContactDetails? contact = null;

        if (request.Contact is { } contactRequest)
        {
            var built = ContactDetails.Create(
                contactRequest.Phone,
                contactRequest.WhatsApp,
                contactRequest.Email,
                contactRequest.Website,
                contactRequest.ContactName);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            contact = built.Value;
        }

        var updated = communityEvent.UpdateContent(
            request.BaseRevision,
            caller,
            title,
            description,
            audience.Value,
            contact,
            request.HostName,
            request.RequiresRsvp,
            request.IsAttendeeListPublic);

        return updated.IsFailure ? Result.Failure(updated.Error) : Result.Success();
    }

    private static Result<EventAudience?> BuildAudience(
        CommunityEvent communityEvent, UpdateEventRequest request)
    {
        if (request.IsFamilyFriendly is null
            && request.IsChildFriendly is null
            && request.IsStudentFocused is null
            && request.IsFree is null
            && request.DressCode is null
            && request.AccessibilityNotes is null)
        {
            return Result.Success<EventAudience?>(null);
        }

        var built = EventAudience.Create(
            request.IsFamilyFriendly ?? communityEvent.Audience.IsFamilyFriendly,
            request.IsChildFriendly ?? communityEvent.Audience.IsChildFriendly,
            request.IsStudentFocused ?? communityEvent.Audience.IsStudentFocused,
            request.IsFree ?? communityEvent.Audience.IsFree,
            request.DressCode ?? communityEvent.Audience.DressCode,
            request.AccessibilityNotes ?? communityEvent.Audience.AccessibilityNotes);

        return built.IsFailure
            ? Result.Failure<EventAudience?>(built.Error)
            : Result.Success<EventAudience?>(built.Value);
    }
}
