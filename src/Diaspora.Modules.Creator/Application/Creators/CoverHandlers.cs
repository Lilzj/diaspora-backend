using System.Text.RegularExpressions;
using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Creators;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Media.Application.Assets;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;
using MediatR;

namespace Diaspora.Modules.Creator.Application.Creators;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The creator cover, from an upload (20 Oct 2026).
//
// PUT /creators/me has always taken `coverImageUrl`, but the app holds a media id after uploading,
// and a member's upload is private: an address to it rendered for its uploader and nobody else.
// These two routes take the media id instead, have Media confirm it is the caller's own image and
// make it public, and store the public address in the same `coverImageUrl` every response already
// carries — so no reader changes and no column is added.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary><c>PUT /creators/me/cover</c>.</summary>
[RequirePermission(CreatorPermissions.ProfileManage)]
[Auditable(Action = "creator.profile.cover.set")]
public sealed record SetCreatorCoverCommand : ICommand<CreatorProfileResponse>
{
    public required SetCreatorCoverRequest Request { get; init; }
}

/// <summary><c>DELETE /creators/me/cover</c>.</summary>
[RequirePermission(CreatorPermissions.ProfileManage)]
[Auditable(Action = "creator.profile.cover.remove")]
public sealed record RemoveCreatorCoverCommand : ICommand<CreatorProfileResponse>;

public sealed class SetCreatorCoverCommandValidator : AbstractValidator<SetCreatorCoverCommand>
{
    public SetCreatorCoverCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.MediaId).NotEmpty();
    }
}

public sealed class SetCreatorCoverCommandHandler
    : ICommandHandler<SetCreatorCoverCommand, CreatorProfileResponse>
{
    private readonly CreatorContext _context;
    private readonly CreatorCoverWriter _writer;

    public SetCreatorCoverCommandHandler(CreatorContext context, CreatorCoverWriter writer)
    {
        _context = context;
        _writer = writer;
    }

    public async Task<Result<CreatorProfileResponse>> Handle(
        SetCreatorCoverCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanModify(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(allowed.Error);
        }

        // Media decides: the caller's own upload, an image, not quarantined. Its refusal is the
        // member's answer, unchanged.
        var published = await _writer.PublishAsync(command.Request.MediaId, cancellationToken);

        if (published.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(published.Error);
        }

        return await _writer.ReplaceAsync(creator.Value, published.Value, cancellationToken);
    }
}

public sealed class RemoveCreatorCoverCommandHandler
    : ICommandHandler<RemoveCreatorCoverCommand, CreatorProfileResponse>
{
    private readonly CreatorContext _context;
    private readonly CreatorCoverWriter _writer;

    public RemoveCreatorCoverCommandHandler(CreatorContext context, CreatorCoverWriter writer)
    {
        _context = context;
        _writer = writer;
    }

    public async Task<Result<CreatorProfileResponse>> Handle(
        RemoveCreatorCoverCommand command, CancellationToken cancellationToken)
    {
        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanModify(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorProfileResponse>(allowed.Error);
        }

        return await _writer.ReplaceAsync(creator.Value, null, cancellationToken);
    }
}

/// <summary>
/// Stores the new cover and withdraws the one it replaced.
/// <para>
/// The old cover is made private again only when it was a Media upload (its address names the
/// asset) — an address set through <c>PUT /creators/me</c> points somewhere this module cannot
/// withdraw. Withdrawing never fails the change; Media treats anything that is not still this
/// member's public cover as nothing to do.
/// </para>
/// </summary>
public sealed partial class CreatorCoverWriter
{
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISender _sender;
    private readonly IClock _clock;

    public CreatorCoverWriter(
        ICreatorProfileRepository creators,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        ISender sender,
        IClock clock)
    {
        _creators = creators;
        _status = status;
        _unitOfWork = unitOfWork;
        _sender = sender;
        _clock = clock;
    }

    /// <summary>
    /// Has Media claim the caller's upload as their cover and make it public; answers its public
    /// address, or Media's refusal unchanged.
    /// </summary>
    public async Task<Result<string>> PublishAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        var published = await _sender.Send(
            new PublishProfileImageCommand { MediaId = mediaId, Role = ProfileImageRoles.Cover },
            cancellationToken);

        return published.IsFailure
            ? Result.Failure<string>(published.Error)
            : Result.Success(published.Value.Url);
    }

    public async Task<Result<CreatorProfileResponse>> ReplaceAsync(
        CreatorProfile creator, string? coverImageUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var previous = MediaIdIn(creator.CoverImageUrl);

        creator.ChangeCoverImage(coverImageUrl, _clock.UtcNow);
        _creators.Update(creator);

        await _status.RefreshAsync(creator, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (previous is { } previousId && previousId != MediaIdIn(coverImageUrl))
        {
            await _sender.Send(
                new WithdrawProfileImageCommand { MediaId = previousId, Role = ProfileImageRoles.Cover },
                cancellationToken);
        }

        return Result.Success(creator.ToResponse([]));
    }

    /// <summary>The Media asset a public media address names, or null for any other address.</summary>
    internal static Guid? MediaIdIn(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = MediaRoute().Match(url);

        return match.Success && Guid.TryParse(match.Groups["id"].Value, out var id) ? id : null;
    }

    [GeneratedRegex(
        @"/api/v1/media/(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/(variants|content)",
        RegexOptions.CultureInvariant)]
    private static partial Regex MediaRoute();
}
