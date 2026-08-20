using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Hashtags;

/// <summary>
/// One topic, platform-wide.
/// <para>
/// A row per normalised slug, holding the display form somebody first typed. That split is the
/// point of the aggregate: counts aggregate on <c>lagostraffic</c> while members see
/// <c>LagosTraffic</c>, so one trend does not become four because four people used four
/// capitalisations.
/// </para>
/// <para>
/// <b>Trending is a stored window, not a live query.</b> Counting uses over the last day across
/// every post on the platform, on every render of the discover screen, is the query that takes a
/// social database down. Instead the aggregate carries a recent-use counter that a scheduled
/// sweep rolls over, so trending is an indexed ordering rather than an aggregation.
/// </para>
/// </summary>
public sealed class Hashtag : AggregateRoot<HashtagId>, IAuditableEntity
{
    private Hashtag(HashtagId id, string slug, string displayText, DateTimeOffset createdAt)
        : base(id)
    {
        Slug = slug;
        DisplayText = displayText;
        Status = HashtagStatus.Active;
        FirstUsedAt = createdAt;
        LastUsedAt = createdAt;
    }

    private Hashtag() { }

    /// <summary>The canonical form. Unique platform-wide.</summary>
    public string Slug { get; private set; } = string.Empty;

    /// <summary>The capitalisation the first member used. Display only.</summary>
    public string DisplayText { get; private set; } = string.Empty;

    public HashtagStatus Status { get; private set; }

    /// <summary>Total uses, ever. What an autocomplete ranks on.</summary>
    public int UsageCount { get; private set; }

    /// <summary>Uses inside the current trending window. Reset by the sweep.</summary>
    public int RecentUsageCount { get; private set; }

    /// <summary>
    /// The country this tag is trending in, when it is concentrated in one.
    /// <para>
    /// A single column rather than a per-country table, because a tag that is genuinely
    /// cross-border is not a country trend at all — it is a global one, and this being null is
    /// how that is expressed.
    /// </para>
    /// </summary>
    public string? PrimaryCountryCode { get; private set; }

    public DateTimeOffset FirstUsedAt { get; private set; }

    public DateTimeOffset LastUsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public bool IsSurfaceable => Status == HashtagStatus.Active;

    public static Hashtag Create(HashtagSlug slug, string displayText, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(slug);

        return new Hashtag(HashtagId.New(), slug.Value, displayText.Trim(), now);
    }

    public void RecordUse(DateTimeOffset now, string? countryCode)
    {
        UsageCount++;
        RecentUsageCount++;
        LastUsedAt = now;

        // First writer wins. A tag that has been used in Nigeria a thousand times does not
        // become a Ghanaian trend because one Ghanaian used it.
        PrimaryCountryCode ??= countryCode?.ToUpperInvariant();
    }

    public void RecordRemoval() => UsageCount = Math.Max(0, UsageCount - 1);

    /// <summary>Rolls the trending window. Called by the sweep, never by a request.</summary>
    public void RollWindow() => RecentUsageCount = 0;

    public void SetStatus(HashtagStatus status) => Status = status;

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
