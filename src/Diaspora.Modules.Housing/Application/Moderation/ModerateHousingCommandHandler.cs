using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// Applies a moderation decision to a property.
/// <para>
/// <b>Every action also writes a note, whatever it was.</b> The trail is what a second moderator
/// reads before overturning a first one's decision, and what an appeal is answered from — and a
/// decision with no recorded reason is one nobody can review. Adding the note deliberately does not
/// bump the listing's revision, so annotating a property cannot make the landlord's in-flight save
/// fail.
/// </para>
/// <para>
/// Uses the repository directly rather than the write gate: a moderator does not own the listing
/// and the gate would refuse them. The moderation permission on the command is the authorisation.
/// </para>
/// </summary>
public sealed class ModerateHousingCommandHandler : ICommandHandler<ModerateHousingCommand>
{
    private readonly IHousingListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ModerateHousingCommandHandler(
        IHousingListingRepository listings,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ModerateHousingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        // Including deleted: a moderator may need to annotate or clear a listing its landlord has
        // already taken down, which is exactly the case a complaint arrives about.
        var listing = await _listings.GetIncludingDeletedAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure(HousingErrors.ListingNotFound);
        }

        var now = _clock.UtcNow;
        var reason = command.Request.Reason;

        var applied = command.Request.Action?.Trim().ToLowerInvariant() switch
        {
            "hide" => Apply(() => listing.Hide(reason, caller.Value, now)),
            "suspend" => Apply(() => listing.Suspend(reason, caller.Value, now)),
            "clear" => Apply(() => listing.Clear(caller.Value, now)),
            "archive" => listing.Archive(now, caller.Value, reason),

            // A note on its own is a legitimate moderation action: recording that a report was
            // looked at and found to be nothing is as much a part of the trail as a takedown.
            "note" => Result.Success(),

            _ => Result.Failure(HousingErrors.ModerationNoteInvalid)
        };

        if (applied.IsFailure)
        {
            return applied;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            listing.AddModerationNote(
                _guids.NewSequentialGuid(), caller.Value, reason, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>Adapts the aggregate's void moderation mutators to a result.</summary>
    private static Result Apply(Action action)
    {
        action();

        return Result.Success();
    }
}
