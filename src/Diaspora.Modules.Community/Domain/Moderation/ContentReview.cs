using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Moderation;

/// <summary>
/// The result of an automated look at a piece of content.
/// <para>
/// B6 ships <b>no reviewer</b>. That is the same decision B5 made about virus scanning and for
/// the same reason: a fake that always answers clean is worse than nothing, because it makes an
/// unmoderated platform look moderated. With no provider registered every review is recorded as
/// <see cref="ContentReviewStatus.Unavailable"/>, which is honest, visible in a metric, and
/// impossible to mistake for a pass.
/// </para>
/// <para>
/// The provider name and its version are stored on the row. An appeal against an automated
/// decision is unanswerable without knowing which model made it, and "the classifier" is not an
/// answer eighteen months later.
/// </para>
/// </summary>
public sealed class ContentReview : AggregateRoot<ContentReviewId>, IAuditableEntity
{
    public const int MaxProviderLength = 64;

    private ContentReview(
        ContentReviewId id,
        ContentReference content,
        ContentReviewStatus status,
        string provider,
        string? providerVersion,
        double confidence,
        string flags,
        DateTimeOffset reviewedAt) : base(id)
    {
        Content = content;
        Status = status;
        Provider = provider;
        ProviderVersion = providerVersion;
        Confidence = confidence;
        Flags = flags;
        ReviewedAt = reviewedAt;
    }

    private ContentReview() { }

    public ContentReference Content { get; private set; } = default!;

    public ContentReviewStatus Status { get; private set; }

    /// <summary>Which provider answered. <c>none</c> when no provider was registered.</summary>
    public string Provider { get; private set; } = string.Empty;

    public string? ProviderVersion { get; private set; }

    /// <summary>Zero to one. Meaningless unless the status is flagged or rejected.</summary>
    public double Confidence { get; private set; }

    /// <summary>Comma-joined <see cref="ContentReviewFlag"/> names.</summary>
    public string Flags { get; private set; } = string.Empty;

    public DateTimeOffset ReviewedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public bool NeedsHumanReview =>
        Status is ContentReviewStatus.Flagged or ContentReviewStatus.Rejected;

    public IReadOnlyList<ContentReviewFlag> ParsedFlags =>
        Flags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name =>
                Enum.TryParse<ContentReviewFlag>(name, ignoreCase: true, out var flag)
                    ? (ContentReviewFlag?)flag
                    : null)
            .Where(flag => flag is not null)
            .Select(flag => flag!.Value)
            .ToList()
            .AsReadOnly();

    public static ContentReview Record(
        ContentReference content,
        ContentReviewStatus status,
        string provider,
        string? providerVersion,
        double confidence,
        IReadOnlyCollection<ContentReviewFlag> flags,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(flags);

        var name = (provider ?? "none").Trim();

        return new ContentReview(
            ContentReviewId.New(),
            content,
            status,
            name.Length > MaxProviderLength ? name[..MaxProviderLength] : name,
            providerVersion?.Trim(),
            Math.Clamp(confidence, 0d, 1d),
            string.Join(',', flags.Select(flag => flag.ToString())),
            now);
    }

    /// <summary>The honest answer when no provider is registered. Never a pass.</summary>
    public static ContentReview Unavailable(ContentReference content, DateTimeOffset now) =>
        Record(content, ContentReviewStatus.Unavailable, "none", null, 0d, [], now);

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
