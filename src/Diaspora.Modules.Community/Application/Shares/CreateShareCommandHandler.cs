using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.Shares;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Shares;

/// <summary>
/// Records a share.
/// <para>
/// Not idempotent, deliberately: sharing the same post to two people is two shares. The counter
/// on the post is what a card renders, and it counts acts rather than distinct sharers.
/// </para>
/// </summary>
public sealed class CreateShareCommandHandler : ICommandHandler<CreateShareCommand, ShareResponse>
{
    private readonly IShareRepository _shares;
    private readonly ICommunityPostRepository _posts;
    private readonly ContentReferenceResolver _references;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateShareCommandHandler(
        IShareRepository shares,
        ICommunityPostRepository posts,
        ContentReferenceResolver references,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _shares = shares;
        _posts = posts;
        _references = references;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<ShareResponse>> Handle(
        CreateShareCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<ShareResponse>(CommunityErrors.NotAuthenticated);
        }

        var reference = _references.Resolve(request.ContentType, request.ContentId);

        if (reference.IsFailure)
        {
            return Result.Failure<ShareResponse>(reference.Error);
        }

        var kind = CommunityWireNames.ParseOr(request.Request.Kind, ShareKind.External);

        var target = kind switch
        {
            ShareKind.Internal when request.Request.RecipientId is { } recipient =>
                ShareTarget.Internal(recipient),
            ShareKind.Internal => null,
            ShareKind.Quote => ShareTarget.Quote(),
            ShareKind.Reshare => ShareTarget.Reshare(),
            _ => ShareTarget.External(request.Request.Destination)
        };

        if (target is null)
        {
            return Result.Failure<ShareResponse>(CommunityErrors.ShareInvalidTarget);
        }

        var descriptor = await _references.DescribeAsync(reference.Value, cancellationToken);

        if (descriptor is null || !descriptor.Exists)
        {
            return Result.Failure<ShareResponse>(CommunityErrors.ContentNotFound);
        }

        var share = Share.Record(
            reference.Value,
            userId,
            target,
            _clock.UtcNow,
            request.Request.Message,
            null,
            descriptor.AuthorId);

        await _shares.AddAsync(share, cancellationToken);

        if (reference.Value.Type == CommunityContentType.Post)
        {
            var post = await _posts.GetAsync(
                CommunityPostId.From(reference.Value.Id), cancellationToken);

            post?.ApplyEngagement(counters => counters.WithShares(1));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ShareResponse
        {
            ShareId = share.Id.Value,
            ContentType = CommunityWireNames.ToWire(reference.Value.Type),
            ContentId = reference.Value.Id,
            Kind = CommunityWireNames.ToWire(kind),
            Destination = target.Destination,
            QuotePostId = share.QuotePostId?.Value,
            ShareCount = await _shares.CountAsync(reference.Value, cancellationToken),
            CreatedAt = share.SharedAt
        });
    }
}
