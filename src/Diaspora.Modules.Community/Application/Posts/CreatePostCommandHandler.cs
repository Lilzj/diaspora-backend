using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using MediaId = Diaspora.SharedKernel.Identity.MediaId;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Posts;

/// <summary>
/// Creates a post.
/// <para>
/// The location on the request is loose â€” a city name, or coordinates, or nothing â€” and it is
/// <b>Location's</b> scope resolver that turns it into a country, an area and a hierarchy path.
/// B6 stores the answer and does none of the resolving, which is the line the brief draws around
/// geographic behaviour.
/// </para>
/// <para>
/// Transactional, because a published post writes a post row, a revision, hashtag rows, mention
/// rows and a media claim. A partial failure that left the post without its hashtags would be
/// invisible until somebody wondered why a tag feed was missing something.
/// </para>
/// </summary>
public sealed class CreatePostCommandHandler
    : ICommandHandler<CreatePostCommand, CommunityPostResponse>
{
    private readonly ICommunityPostRepository _posts;
    private readonly PostComposer _composer;
    private readonly ViewerContextResolver _viewers;
    private readonly IGeographicScopeResolver _scopes;
    private readonly ILocationDirectory _location;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly CommunityAuthorNames _authorNames;

    public CreatePostCommandHandler(
        ICommunityPostRepository posts,
        PostComposer composer,
        ViewerContextResolver viewers,
        IGeographicScopeResolver scopes,
        ILocationDirectory location,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock,
        CommunityAuthorNames authorNames)
    {
        _posts = posts;
        _composer = composer;
        _viewers = viewers;
        _scopes = scopes;
        _location = location;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _authorNames = authorNames;
    }

    public async Task<Result<CommunityPostResponse>> Handle(
        CreatePostCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.NotAuthenticated);
        }

        var payload = request.Request;

        var body = PostBody.Create(payload.Body);

        if (body.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(body.Error);
        }

        var title = PostTitle.CreateOptional(payload.Title);

        if (title.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(title.Error);
        }

        var category = CommunityWireNames.ParseOr(payload.Category, PostCategory.Other);
        var urgency = CommunityWireNames.ParseOr(payload.Urgency, UrgencyLevel.Normal);

        // Emergency is its own permission (16 Sep 2026). The route's policy admits anybody who may
        // post at all; the rule is about urgency, so it is checked where urgency is known.
        if (urgency == UrgencyLevel.Emergency
            && !_currentUser.Permissions.Contains(CommunityPermissions.PublishEmergency, StringComparer.Ordinal))
        {
            return Result.Failure<CommunityPostResponse>(CommunityErrors.EmergencyNotPermitted);
        }

        var audience = BuildAudience(payload);
        var location = await ResolveLocationAsync(payload, cancellationToken);

        var publisher = payload.PublishAsOrganizationId is { } organizationId
            ? PublishingIdentity.ForOrganization(
                PublishingIdentityType.Organization, organizationId, null)
            : PublishingIdentity.Personal();

        var draft = CommunityPost.Draft(
            userId,
            publisher,
            body.Value,
            title.Value,
            category,
            urgency,
            audience,
            location,
            _clock.UtcNow,
            payload.ExpiresInHours,
            payload.AffectedRadiusMetres);

        if (draft.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(draft.Error);
        }

        var post = draft.Value;

        var attachments = PostComposer.ApplyAttachments(post, payload.Attachments);

        if (attachments.IsFailure)
        {
            return Result.Failure<CommunityPostResponse>(attachments.Error);
        }

        if (payload.Poll is { } pollRequest)
        {
            var poll = Poll.Create(
                post.Id,
                pollRequest.Question,
                pollRequest.Options
                    .Select(option => (
                        option.Label,
                        option.ImageMediaId is { } mediaId ? MediaId.From(mediaId) : null))
                    .ToList(),
                pollRequest.AllowsMultiple,
                CommunityWireNames.ParseOr(
                    pollRequest.ResultVisibility, PollResultVisibility.AfterVoting),
                pollRequest.ClosesAt);

            if (poll.IsFailure)
            {
                return Result.Failure<CommunityPostResponse>(poll.Error);
            }

            post.AttachPoll(poll.Value);
        }

        await _posts.AddAsync(post, cancellationToken);

        if (payload.Publish)
        {
            var published = post.Publish(_clock.UtcNow);

            if (published.IsFailure)
            {
                return Result.Failure<CommunityPostResponse>(published.Error);
            }
        }

        await _composer.ProcessBodyAsync(post, userId, isNew: true, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var names = await _authorNames.ResolveAsync([post.AuthorId], cancellationToken);

        return Result.Success(PostMapper.ToResponse(
            post,
            null,
            viewer,
            post.Poll is null ? null : PollMapper.ToResponse(post.Poll, [], _clock.UtcNow),
            names: names));
    }

    private static ContentAudience BuildAudience(Contracts.V1.Requests.CreatePostRequest payload)
    {
        var level = CommunityWireNames.ParseOr(payload.Visibility, VisibilityLevel.Public);

        return level switch
        {
            VisibilityLevel.Custom when payload.VisibilityRuleId is { } ruleId =>
                ContentAudience.ForRule(VisibilityRuleId.From(ruleId)),
            _ => ContentAudience.FromLevel(
                level, payload.AudienceCountryCode, payload.AudienceAreaId)
        };
    }

    /// <summary>
    /// Turns whatever the composer sent into a stored location context, by asking Location.
    /// <para>
    /// A resolution failure is not an error. A post whose city name Location does not recognise
    /// is still a post, and refusing it because a gazetteer is incomplete would make posting
    /// impossible in exactly the underserved places this platform exists for.
    /// </para>
    /// </summary>
    private async Task<PostLocationContext> ResolveLocationAsync(
        Contracts.V1.Requests.CreatePostRequest payload, CancellationToken cancellationToken)
    {
        var scope = await _scopes.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = payload.CityName,
                Latitude = payload.Latitude,
                Longitude = payload.Longitude,
                CountryCode = payload.CountryCode,
                AreaId = payload.AreaId is { } areaId
                    ? SharedKernel.Identity.AreaId.From(areaId)
                    : null,
                UserId = _currentUser.UserId
            },
            cancellationToken);

        var pinned = payload.Latitude is not null && payload.Longitude is not null;

        if (scope.IsFailure)
        {
            return PostLocationContext.Create(
                payload.CountryCode,
                payload.AreaId,
                payload.AreaName,
                payload.CityName,
                payload.Latitude,
                payload.Longitude,
                pointIsExact: pinned);
        }

        var resolved = scope.Value;
        string? areaName = payload.AreaName;
        string? path = resolved.Path?.Value;

        if (resolved.AreaId is { } id)
        {
            var area = await _location.FindAreaAsync(id, cancellationToken);
            areaName ??= area?.Name?.Value;
            path ??= area?.PathValue;
        }

        return PostLocationContext.Create(
            resolved.CountryCode?.Value ?? payload.CountryCode,
            resolved.AreaId?.Value ?? payload.AreaId,
            areaName,
            resolved.CityName ?? payload.CityName,

            // The author's own pin wins over the centre of whatever it resolved to. Only a post with
            // no pin is placed at a centre, and it is recorded as placed there.
            pinned ? payload.Latitude : resolved.Center?.Latitude,
            pinned ? payload.Longitude : resolved.Center?.Longitude,
            path,
            pointIsExact: pinned);
    }
}
