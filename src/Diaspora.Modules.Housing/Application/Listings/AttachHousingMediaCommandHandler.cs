using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Attaches a Media asset to a property.
/// <para>
/// <b>The claim to Media happens after the save.</b> An unclaimed asset is a photograph Media will
/// sweep, which is bad; a refused attach because Media was briefly unavailable is a landlord who
/// cannot add a photograph, which is worse. The claimer swallows its own failures for the same
/// reason.
/// </para>
/// </summary>
public sealed class AttachHousingMediaCommandHandler
    : ICommandHandler<AttachHousingMediaCommand, HousingListingResponse>
{
    private readonly HousingListingWriteGate _gate;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingMediaClaimer _media;
    private readonly HousingListingReadContext _read;
    private readonly IClock _clock;

    public AttachHousingMediaCommandHandler(
        HousingListingWriteGate gate,
        IUnitOfWork unitOfWork,
        HousingMediaClaimer media,
        HousingListingReadContext read,
        IClock clock)
    {
        _gate = gate;
        _unitOfWork = unitOfWork;
        _media = media;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<HousingListingResponse>> Handle(
        AttachHousingMediaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var listing = await _gate.LoadAsync(command.ListingId, cancellationToken);

        if (listing.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(listing.Error);
        }

        if (!Enum.TryParse<HousingMediaRole>(
                command.Request.Role, ignoreCase: true, out var role))
        {
            return Result.Failure<HousingListingResponse>(HousingErrors.MediaInvalid);
        }

        var attached = listing.Value.AttachMedia(
            command.Request.MediaId, role, command.Request.Caption, _clock.UtcNow);

        if (attached.IsFailure)
        {
            return Result.Failure<HousingListingResponse>(attached.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _media.ClaimAsync(
            listing.Value.Id.Value,
            command.Request.MediaId,
            role.ToString(),
            cancellationToken: cancellationToken);

        var response = await _read.ToResponseAsync(listing.Value, cancellationToken);

        return response is null
            ? Result.Failure<HousingListingResponse>(HousingErrors.ListingNotFound)
            : Result.Success(response);
    }
}
