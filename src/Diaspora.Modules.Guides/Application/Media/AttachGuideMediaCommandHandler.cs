using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Media;

/// <summary>
/// Records the reference, then tells Media the asset has been claimed.
/// <para>
/// The claim is what takes the asset out of the orphan population Media sweeps — a guide
/// illustrated with four uploads whose assets were never claimed loses its images a day later,
/// silently, because every upload succeeded. It runs after the save and never fails the command:
/// an unclaimed asset is a lost image, a refused attach is a lost afternoon.
/// </para>
/// </summary>
public sealed class AttachGuideMediaCommandHandler
    : ICommandHandler<AttachGuideMediaCommand, GuideResponse>
{
    private readonly IGuideRepository _guides;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideMediaClaimer _media;

    public AttachGuideMediaCommandHandler(
        IGuideRepository guides,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        GuideMediaClaimer media)
    {
        _guides = guides;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _media = media;
    }

    public async Task<Result<GuideResponse>> Handle(
        AttachGuideMediaCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideResponse>(GuideErrors.NotFound);
        }

        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideResponse>(caller.Error);
        }

        if (GuideWireNames.ParseMediaRole(command.Request.Role) is not { } role)
        {
            return Result.Failure<GuideResponse>(GuideErrors.MediaInvalid);
        }

        var attached = guide.AttachMedia(
            command.Request.MediaId, role, command.Request.Caption);

        if (attached.IsFailure)
        {
            return Result.Failure<GuideResponse>(attached.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _media.ClaimAsync(
            guide.Id.Value,
            command.Request.MediaId,
            role.ToString(),
            cancellationToken: cancellationToken);

        return Result.Success(GuideMapper.ToResponse(guide, forAuthor: true));
    }
}
