using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// One post on a campaign's own timeline.
/// <para>
/// <b>Its own aggregate rather than a collection on the campaign.</b> Updates accumulate for as long
/// as a campaign runs and are read a page at a time; an owned collection would load every one of
/// them every time anybody read the campaign, which is the shape of a query that is fine in
/// development and is the slowest thing on the platform two years in.
/// </para>
/// <para>
/// <b>Media is a reference and never a URL.</b> B5's rule. The client resolves a reference through
/// Media exactly as it does for a cause, a listing and a guide; this module has never composed a URL
/// and does not start here.
/// </para>
/// <para>
/// <b>Deleting one is soft, and it is the only softly-deleted thing in this module.</b> Everything
/// else here is a financial fact that stays true. An update is somebody's writing about their own
/// campaign, and an organizer who posted the wrong photograph is entitled to take it down —
/// but a supporter who was notified about it is entitled to have the platform still know it
/// existed, which is why it is a flag rather than a delete.
/// </para>
/// </summary>
public sealed class CampaignUpdate : AggregateRoot<CampaignUpdateId>, IAuditableEntity
{
    private CampaignUpdate(
        CampaignUpdateId id,
        CampaignId campaignId,
        Guid authorUserId,
        string authorName,
        string title,
        string body,
        DateTimeOffset publishedAt) : base(id)
    {
        CampaignId = campaignId;
        AuthorUserId = authorUserId;
        AuthorName = authorName;
        Title = title;
        Body = body;
        PublishedAt = publishedAt;
        CreatedAt = publishedAt;
    }

    private CampaignUpdate() { }

    public CampaignId CampaignId { get; private set; } = default!;

    public Guid AuthorUserId { get; private set; }

    /// <summary>Denormalised from the campaign's organizer, so an update reads without a join.</summary>
    public string AuthorName { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>Comma-separated Media references. Never URLs.</summary>
    public string MediaReferences { get; private set; } = string.Empty;

    public DateTimeOffset PublishedAt { get; private set; }

    /// <summary>Set when the organizer withdraws the update. The row survives.</summary>
    public DateTimeOffset? WithdrawnAt { get; private set; }

    public bool IsWithdrawn => WithdrawnAt is not null;

    /// <summary>B15's idempotency primitive. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public IReadOnlyList<string> Media =>
    [
        .. MediaReferences.Split(
            ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ];

    public static Result<CampaignUpdate> Publish(
        CampaignUpdateId id,
        Campaign campaign,
        Guid authorUserId,
        string title,
        string body,
        DateTimeOffset now,
        IEnumerable<string>? mediaReferences = null,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(campaign);

        // A draft campaign nobody can see cannot post to nobody. The state machine's own answer to
        // "is this campaign a public thing yet" is the one used, rather than a second list here.
        if (!CampaignTransitions.IsPubliclyVisible(campaign.Status))
        {
            return Result.Failure<CampaignUpdate>(Error.Conflict(
                FinancialErrorCodes.CampaignTransitionInvalid,
                $"A {campaign.Status} campaign has no supporters to update."));
        }

        var update = new CampaignUpdate(
            id, campaign.Id, authorUserId, campaign.OrganizerName,
            title.Trim(), body.Trim(), now)
        {
            MediaReferences = mediaReferences is null
                ? string.Empty
                : string.Join(
                    ',',
                    mediaReferences
                        .Where(reference => !string.IsNullOrWhiteSpace(reference))
                        .Select(reference => reference.Trim())
                        .Distinct(StringComparer.Ordinal)),
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim()
        };

        update.RaiseDomainEvent(new CampaignUpdatePublishedDomainEvent
        {
            CampaignId = campaign.Id,
            UpdateId = id,
            AuthorUserId = authorUserId,
            Title = update.Title,
            CauseCode = campaign.CauseCode,
            PublishedAt = now
        });

        return Result.Success(update);
    }

    public void Revise(
        string? title, string? body, IEnumerable<string>? mediaReferences, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            Body = body.Trim();
        }

        if (mediaReferences is not null)
        {
            MediaReferences = string.Join(
                ',',
                mediaReferences
                    .Where(reference => !string.IsNullOrWhiteSpace(reference))
                    .Select(reference => reference.Trim())
                    .Distinct(StringComparer.Ordinal));
        }

        Touch(now);
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (WithdrawnAt is not null)
        {
            return;
        }

        WithdrawnAt = now;
        Touch(now);
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
