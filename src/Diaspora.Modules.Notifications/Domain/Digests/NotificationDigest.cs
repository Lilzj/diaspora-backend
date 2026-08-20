using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Errors;
using Diaspora.Modules.Notifications.Domain.Events;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Digests;

/// <summary>
/// One member's collected messages for one window.
/// <para>
/// <b>Assembled channel-independently, which is the brief's requirement and also the only shape that
/// works.</b> A digest is a list of things that happened; whether it becomes an email, a single push
/// or a card at the top of the inbox is decided afterwards by the same preference logic every other
/// message goes through. Building it per channel would mean building it three times and having the
/// three disagree about what "yesterday" meant.
/// </para>
/// <para>
/// <b>An empty window still produces a record.</b> <see cref="DigestStatus.Empty"/> exists because a
/// member asking "why do I never get my daily summary" deserves an answer, and "there was nothing to
/// summarise on the 3rd, 4th and 5th" is one. It costs a row a day per digest-enabled member and
/// buys the difference between a support conversation and a bug hunt.
/// </para>
/// <para>
/// <b>Category and country digests are the same aggregate.</b> A "new guides for Canada" digest is a
/// digest whose scope carries a country code; a learning digest is one whose scope carries a
/// category. Modelling them as separate types would triple the sweep, the query surface and the
/// contract for no behavioural difference.
/// </para>
/// </summary>
public sealed class NotificationDigest : AggregateRoot<NotificationDigestId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<DigestEntry> _entries = [];

    private NotificationDigest(
        NotificationDigestId id,
        UserId recipientUserId,
        DigestFrequency frequency,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset generatedAt) : base(id)
    {
        RecipientUserId = recipientUserId;
        Frequency = frequency;
        WindowStart = windowStart;
        WindowEnd = windowEnd;
        GeneratedAt = generatedAt;
        Status = DigestStatus.Generated;
        CreatedAt = generatedAt;
    }

    private NotificationDigest() { }

    public UserId RecipientUserId { get; private set; } = default!;

    public DigestFrequency Frequency { get; private set; }

    /// <summary>Present when the digest covers one subject area rather than everything.</summary>
    public string? CategoryKey { get; private set; }

    /// <summary>Present when the digest covers one country's knowledge directory.</summary>
    public string? CountryCode { get; private set; }

    public DigestStatus Status { get; private set; }

    public DateTimeOffset WindowStart { get; private set; }

    public DateTimeOffset WindowEnd { get; private set; }

    public DateTimeOffset GeneratedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? OpenedAt { get; private set; }

    /// <summary>The language it was assembled in. Frozen, like a notification's content.</summary>
    public string Language { get; private set; } = LocaleFallbackChain.DefaultLanguage;

    /// <summary>
    /// How many messages the window actually held.
    /// <para>
    /// Not the same as <c>Entries.Count</c>: a window holding two hundred reactions shows the first
    /// fifty and says so, because a digest that reprints everything is the thing it exists to
    /// replace.
    /// </para>
    /// </summary>
    public int TotalCount { get; private set; }

    public int Revision { get; private set; }

    public IReadOnlyList<DigestEntry> Entries =>
        _entries.OrderByDescending(entry => entry.OccurredAt).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    public bool IsEmpty => Status == DigestStatus.Empty || _entries.Count == 0;

    public static Result<NotificationDigest> Assemble(
        NotificationDigestId id,
        UserId recipientUserId,
        DigestFrequency frequency,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset generatedAt,
        IEnumerable<DigestEntry> entries,
        string? language = null,
        string? categoryKey = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(recipientUserId);
        ArgumentNullException.ThrowIfNull(entries);

        if (windowEnd <= windowStart)
        {
            return Result.Failure<NotificationDigest>(NotificationErrors.PreferenceInvalid);
        }

        var collected = entries.ToList();

        var digest = new NotificationDigest(
            id, recipientUserId, frequency, windowStart, windowEnd, generatedAt)
        {
            CategoryKey = string.IsNullOrWhiteSpace(categoryKey)
                ? null
                : categoryKey.Trim().ToLowerInvariant(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            Language = string.IsNullOrWhiteSpace(language)
                ? LocaleFallbackChain.DefaultLanguage
                : LocaleFallbackChain.Normalize(language),
            TotalCount = collected.Count
        };

        digest._entries.AddRange(collected.Take(NotificationLimits.MaxDigestEntries));

        if (digest._entries.Count == 0)
        {
            digest.Status = DigestStatus.Empty;
        }

        digest.RaiseDomainEvent(new DigestGeneratedDomainEvent
        {
            DigestId = id,
            RecipientUserId = recipientUserId,
            Frequency = frequency,
            CategoryKey = digest.CategoryKey,
            CountryCode = digest.CountryCode,
            ItemCount = digest._entries.Count,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            GeneratedAt = generatedAt
        });

        return Result.Success(digest);
    }

    /// <summary>Handed to at least one channel.</summary>
    public Result MarkSent(DateTimeOffset now)
    {
        if (IsEmpty)
        {
            return Result.Failure(NotificationErrors.DigestEmpty);
        }

        if (Status == DigestStatus.Generated)
        {
            Status = DigestStatus.Sent;
            SentAt = now;

            Touch();
        }

        return Result.Success();
    }

    /// <summary>The member read it. Idempotent, like every other engagement fact in this module.</summary>
    public Result MarkOpened(DateTimeOffset now)
    {
        if (OpenedAt.HasValue)
        {
            return Result.Success();
        }

        OpenedAt = now;
        Status = DigestStatus.Opened;

        Touch();

        return Result.Success();
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

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        Touch();
    }

    private void Touch() => Revision++;
}
