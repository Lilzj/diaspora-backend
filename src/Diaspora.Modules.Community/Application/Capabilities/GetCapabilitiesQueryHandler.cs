using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Moderation;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Capabilities;

/// <summary>
/// Reports the module's live capabilities.
/// <para>
/// Everything here is read from a registry rather than from a constant, so a deployment that
/// registers a new feed, a new ranking or a new reaction says so without a release. That is the
/// point: the client currently hardcodes four of these lists and has already got three of them
/// out of step with its own types.
/// </para>
/// </summary>
public sealed class GetCapabilitiesQueryHandler
    : IQueryHandler<GetCapabilitiesQuery, CommunityCapabilitiesResponse>
{
    private readonly FeedComposer _feeds;
    private readonly ReactionCatalogLoader _reactions;
    private readonly ContentReferenceResolver _references;
    private readonly ModerationPipeline _moderation;

    public GetCapabilitiesQueryHandler(
        FeedComposer feeds,
        ReactionCatalogLoader reactions,
        ContentReferenceResolver references,
        ModerationPipeline moderation)
    {
        _feeds = feeds;
        _reactions = reactions;
        _references = references;
        _moderation = moderation;
    }

    public async Task<Result<CommunityCapabilitiesResponse>> Handle(
        GetCapabilitiesQuery request, CancellationToken cancellationToken)
    {
        var catalog = await _reactions.GetAsync(cancellationToken);

        return Result.Success(new CommunityCapabilitiesResponse
        {
            Reactions = catalog.Definitions
                .OrderBy(definition => definition.Position)
                .Select(definition => new ReactionDefinitionResponse
                {
                    Key = definition.Key,
                    Label = definition.Label,
                    Glyph = definition.Glyph,
                    Sentiment = CommunityWireNames.ToWire(definition.Sentiment),
                    AppliesTo = definition.AppliesTo.Length == 0
                        ? []
                        : definition.AppliesTo
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(name => name.Trim())
                            .ToList(),
                    Position = definition.Position
                })
                .ToList(),
            Feeds = _feeds.Providers
                .Select(provider => new FeedDescriptorResponse
                {
                    Key = provider.FeedKey,
                    DefaultRanking = provider.DefaultRankingKey,
                    RequiresViewer = !provider.CanServe(new FeedRequest
                    {
                        FeedKey = provider.FeedKey,
                        AllowedVisibility = [VisibilityLevel.Public]
                    })
                })
                .ToList(),
            Rankings = _feeds.RankingKeys,
            ReportReasons = ModerationMapper.Reasons(),
            VisibilityLevels = CommunityWireNames.All<VisibilityLevel>(),
            SupportedContentTypes = _references.SupportedTypes
                .Select(CommunityWireNames.ToWire)
                .ToList(),
            MaxPostLength = PostBody.MaxLength,
            MaxCommentLength = CommentBody.MaxLength,
            MaxPostAttachments = CommunityLimits.MaxPostAttachments,
            MaxPollOptions = CommunityLimits.MaxPollOptions,
            MaxFeedPageSize = CommunityLimits.MaxFeedPageSize,
            MaxRenderedCommentDepth = CommunityLimits.MaxRenderedCommentDepth,
            AutomatedReviewAvailable = _moderation.HasReviewer
        });
    }
}
