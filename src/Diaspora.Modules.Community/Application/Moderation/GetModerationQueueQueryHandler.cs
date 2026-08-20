using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Moderation;

/// <summary>
/// Builds the queue.
/// <para>
/// Grouped by content rather than listed by report, because a moderator decides about a piece of
/// content once — a post with nine reports is one decision, not nine, and a queue that showed it
/// nine times would be nine chances to decide differently.
/// </para>
/// </summary>
public sealed class GetModerationQueueQueryHandler
    : IQueryHandler<GetModerationQueueQuery, IReadOnlyList<ModerationQueueItemResponse>>
{
    private readonly IReportRepository _reports;
    private readonly IModerationRepository _moderation;
    private readonly ContentReferenceResolver _references;

    public GetModerationQueueQueryHandler(
        IReportRepository reports,
        IModerationRepository moderation,
        ContentReferenceResolver references)
    {
        _reports = reports;
        _moderation = moderation;
        _references = references;
    }

    public async Task<Result<IReadOnlyList<ModerationQueueItemResponse>>> Handle(
        GetModerationQueueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contentType = string.IsNullOrWhiteSpace(request.ContentType)
            ? null
            : CommunityWireNames.ParseContentType(request.ContentType) is { IsSuccess: true } parsed
                ? parsed.Value
                : (CommunityContentType?)null;

        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? null
            : CommunityWireNames.ParseReportReason(request.Reason) is { IsSuccess: true } parsedReason
                ? parsedReason.Value
                : (ReportReasonCode?)null;

        var open = await _reports.ListOpenAsync(
            contentType,
            reason,
            FeedCursor.Decode(request.Cursor),
            Math.Clamp(request.Limit, 1, CommunityLimits.MaxFeedPageSize),
            cancellationToken);

        var grouped = open.Items
            .GroupBy(report => report.Content.Key, StringComparer.Ordinal)
            .ToList();

        var references = grouped
            .Select(group => group.First().Content)
            .ToList();

        var descriptors = references.Count == 0
            ? new Dictionary<string, Extensibility.Content.ContentDescriptor>(StringComparer.Ordinal)
            : await DescribeAsync(references, cancellationToken);

        var reviews = await _moderation.GetLatestReviewsAsync(references, cancellationToken);

        var items = new List<ModerationQueueItemResponse>(grouped.Count);

        foreach (var group in grouped)
        {
            var first = group.First();
            var descriptor = descriptors.GetValueOrDefault(group.Key);
            var review = reviews.GetValueOrDefault(group.Key);

            items.Add(new ModerationQueueItemResponse
            {
                ContentType = CommunityWireNames.ToWire(first.Content.Type),
                ContentId = first.Content.Id,
                Title = descriptor?.Title ?? "(removed)",
                AuthorId = descriptor?.AuthorId ?? first.ContentAuthorId ?? Guid.Empty,
                AuthorName = null,
                AuthorLevel = null,
                AiReview = review is null ? null : ModerationMapper.ToResponse(review),
                ReportCount = group.Count(),
                Reports = group.Select(ModerationMapper.ToResponse).ToList(),
                Status = CommunityWireNames.ToWire(first.Status),
                CreatedAt = group.Min(report => report.ReportedAt)
            });
        }

        return Result.Success<IReadOnlyList<ModerationQueueItemResponse>>(items);
    }

    private async Task<IReadOnlyDictionary<string, Extensibility.Content.ContentDescriptor>>
        DescribeAsync(IReadOnlyList<ContentReference> references, CancellationToken cancellationToken)
    {
        var described =
            new Dictionary<string, Extensibility.Content.ContentDescriptor>(StringComparer.Ordinal);

        foreach (var reference in references)
        {
            var descriptor = await _references.DescribeAsync(reference, cancellationToken);

            if (descriptor is not null)
            {
                described[reference.Key] = descriptor;
            }
        }

        return described;
    }
}
