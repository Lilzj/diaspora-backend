using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Shares;

/// <summary>
/// A record that content was passed along.
/// <para>
/// Unlike every other social record here, a share is <b>not unique per member</b>: sharing the
/// same post to two friends is two shares, and sharing it again next week is a third. There is
/// no upsert and no idempotency, because the event being recorded is an act rather than a state.
/// </para>
/// <para>
/// A quote share carries the identifier of the post it produced, which is what lets the original
/// render its quotes and the quote render its original without either owning the other.
/// </para>
/// </summary>
public sealed class Share : AggregateRoot<ShareId>, IAuditableEntity
{
    private Share(
        ShareId id,
        ContentReference content,
        UserId sharedBy,
        ShareTarget target,
        DateTimeOffset sharedAt) : base(id)
    {
        Content = content;
        SharedBy = sharedBy;
        Target = target;
        SharedAt = sharedAt;
    }

    private Share() { }

    public ContentReference Content { get; private set; } = default!;

    public UserId SharedBy { get; private set; } = default!;

    public ShareTarget Target { get; private set; } = default!;

    /// <summary>Set for a quote share: the post the quote created.</summary>
    public CommunityPostId? QuotePostId { get; private set; }

    /// <summary>Optional commentary the sharer added.</summary>
    public string? Message { get; private set; }

    public DateTimeOffset SharedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static Share Record(
        ContentReference content,
        UserId sharedBy,
        ShareTarget target,
        DateTimeOffset now,
        string? message = null,
        CommunityPostId? quotePostId = null,
        Guid? contentAuthorId = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sharedBy);
        ArgumentNullException.ThrowIfNull(target);

        var share = new Share(ShareId.New(), content, sharedBy, target, now)
        {
            Message = message?.Trim(),
            QuotePostId = quotePostId
        };

        share.RaiseDomainEvent(new ShareRecordedDomainEvent
        {
            ShareId = share.Id,
            Content = content,
            SharedBy = sharedBy,
            Kind = target.Kind,
            ContentAuthorId = contentAuthorId,
            RecipientId = target.RecipientId
        });

        return share;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
