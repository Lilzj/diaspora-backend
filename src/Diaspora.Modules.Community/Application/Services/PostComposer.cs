using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.Modules.Community.Domain.Mentions;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Enrichment;
using Diaspora.Modules.Community.Extensibility.Hashtags;
using Diaspora.Modules.Community.Extensibility.Mentions;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Everything that happens to a post besides being written.
/// <para>
/// Attachments, hashtag extraction and registration, mention resolution, media claiming and
/// enrichment. Gathered here because create and edit both need all of it, and because a post
/// whose hashtags were extracted on create but not on edit is a post whose tags silently stop
/// matching its text.
/// </para>
/// </summary>
public sealed class PostComposer
{
    private readonly HashtagExtractor _hashtags;
    private readonly MentionExtractor _mentions;
    private readonly IHashtagRepository _hashtagRepository;
    private readonly IMentionRepository _mentionRepository;
    private readonly ContentEnricherPipeline _enrichers;
    private readonly MediaAttachmentClaimer _media;
    private readonly IClock _clock;

    public PostComposer(
        HashtagExtractor hashtags,
        MentionExtractor mentions,
        IHashtagRepository hashtagRepository,
        IMentionRepository mentionRepository,
        ContentEnricherPipeline enrichers,
        MediaAttachmentClaimer media,
        IClock clock)
    {
        _hashtags = hashtags;
        _mentions = mentions;
        _hashtagRepository = hashtagRepository;
        _mentionRepository = mentionRepository;
        _enrichers = enrichers;
        _media = media;
        _clock = clock;
    }

    /// <summary>Adds the requested attachments, refusing anything malformed.</summary>
    public static Result ApplyAttachments(
        CommunityPost post, IReadOnlyList<AttachmentRequest>? requests)
    {
        ArgumentNullException.ThrowIfNull(post);

        if (requests is null || requests.Count == 0)
        {
            return Result.Success();
        }

        if (requests.Count > CommunityLimits.MaxPostAttachments)
        {
            return Result.Failure(
                Domain.Errors.CommunityErrors.PostAttachmentLimit(
                    CommunityLimits.MaxPostAttachments));
        }

        foreach (var request in requests)
        {
            var kind = CommunityWireNames.ParseOr(request.Kind, AttachmentKind.Image);

            var attachment = kind == AttachmentKind.Link
                ? BuildLink(request)
                : BuildMedia(request, kind);

            if (attachment is null)
            {
                return Result.Failure(Domain.Errors.CommunityErrors.ContentNotFound);
            }

            var added = post.Attach(attachment);

            if (added.IsFailure)
            {
                return added;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Extracts tags and mentions from the body, registers them, and runs the enrichers.
    /// <para>
    /// Called after the aggregate is valid and inside the same transaction, so nothing an
    /// enricher does can produce a post that would not otherwise have been publishable.
    /// </para>
    /// </summary>
    public async Task ProcessBodyAsync(
        CommunityPost post, UserId authorId, bool isNew, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(authorId);

        post.ClearExtractions();

        await ApplyHashtagsAsync(post, cancellationToken);
        await ApplyMentionsAsync(post, authorId, cancellationToken);

        await _enrichers.RunAsync(
            new ContentEnrichmentContext
            {
                Post = post, IsNewContent = isNew, Now = _clock.UtcNow
            },
            cancellationToken);

        await _media.ClaimAsync(post, cancellationToken);
    }

    private async Task ApplyHashtagsAsync(CommunityPost post, CancellationToken cancellationToken)
    {
        var candidates = _hashtags.Extract(post.Body.Value);

        if (candidates.Count == 0)
        {
            return;
        }

        var slugs = candidates.Select(candidate => candidate.Slug).ToList();
        var existing = (await _hashtagRepository.FindManyAsync(slugs, cancellationToken))
            .ToDictionary(hashtag => hashtag.Slug, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (!existing.TryGetValue(candidate.Slug, out var hashtag))
            {
                var slug = HashtagSlug.Create(candidate.Slug);

                if (slug.IsFailure)
                {
                    continue;
                }

                hashtag = Hashtag.Create(slug.Value, candidate.Raw, _clock.UtcNow);
                await _hashtagRepository.AddAsync(hashtag, cancellationToken);
                existing[candidate.Slug] = hashtag;
            }

            // A blocked tag is dropped from the post rather than failing the publish. Refusing
            // an emergency alert because it carried an unfortunate tag is the wrong trade.
            if (hashtag.Status == HashtagStatus.Blocked)
            {
                continue;
            }

            hashtag.RecordUse(_clock.UtcNow, post.Location.CountryCode);
            post.AddHashtag(hashtag.Id, hashtag.Slug);
        }
    }

    private async Task ApplyMentionsAsync(
        CommunityPost post, UserId authorId, CancellationToken cancellationToken)
    {
        var resolved = await _mentions.ExtractAsync(
            post.Body.Value, CommunityLimits.MaxMentionsPerPost, cancellationToken);

        var previous = await _mentionRepository.ListForContentAsync(post.Reference, cancellationToken);

        // Re-extracted from scratch on every edit, so a mention removed from the body stops
        // being a mention. Leaving stale rows would keep notifying somebody who is no longer
        // named in the text.
        _mentionRepository.RemoveRange(previous);

        foreach (var (candidate, target) in resolved)
        {
            post.AddMention(target, candidate.StartOffset, candidate.Length);

            if (!target.IsResolved)
            {
                continue;
            }

            await _mentionRepository.AddAsync(
                Mention.Create(post.Reference, authorId, target, _clock.UtcNow), cancellationToken);
        }
    }

    private static CommunityAttachment? BuildMedia(AttachmentRequest request, AttachmentKind kind) =>
        request.MediaId is { } mediaId
            ? CommunityAttachment.ForMedia(
                kind, MediaId.From(mediaId), 0, request.Role, request.Caption)
            : null;

    private static CommunityAttachment? BuildLink(AttachmentRequest request) =>
        string.IsNullOrWhiteSpace(request.Url)
            ? null
            : CommunityAttachment.ForLink(LinkPreview.Bare(request.Url), 0);
}
