using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Versions;

/// <summary>
/// One immutable snapshot of a guide as it stood at a publication.
/// <para>
/// <b>Immutable by construction: there are no mutators.</b> A version is what a reader was
/// shown, and what a reader was shown is a historical fact — a guide that once gave wrong
/// advice must not be able to rewrite the record of having given it. Restoring an old version
/// does not touch this aggregate either; it copies content back onto the guide's draft, and the
/// next publication mints a new version.
/// </para>
/// <para>
/// The content is a serialized snapshot rather than an owned section graph, deliberately:
/// versions are written once, read rarely and diffed occasionally, and a relational copy of
/// every section of every version of every guide would be the largest table in the schema in
/// service of a query nobody runs. The application's snapshot serializer owns the shape.
/// </para>
/// <para>
/// Deliberately <em>not</em> <c>ISoftDeletable</c> — history is permanent, even when the guide
/// is deleted. The pair (guide, version number) is unique.
/// </para>
/// </summary>
public sealed class GuideVersion : AggregateRoot<GuideVersionId>, IAuditableEntity
{
    private GuideVersion(
        GuideVersionId id,
        GuideId guideId,
        int versionNumber,
        string title,
        string? summary,
        string language,
        string categoryKey,
        string contentSnapshot,
        string? revisionNotes,
        UserId editorId,
        DateTimeOffset publishedAt,
        int wordCount,
        int readingTimeMinutes)
        : base(id)
    {
        GuideId = guideId;
        VersionNumber = versionNumber;
        Title = title;
        Summary = summary;
        Language = language;
        CategoryKey = categoryKey;
        ContentSnapshot = contentSnapshot;
        RevisionNotes = revisionNotes;
        EditorId = editorId;
        PublishedAt = publishedAt;
        WordCount = wordCount;
        ReadingTimeMinutes = readingTimeMinutes;
        CreatedAt = publishedAt;
    }

    private GuideVersion() { }

    public GuideId GuideId { get; private set; } = default!;

    public int VersionNumber { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Summary { get; private set; }

    public string Language { get; private set; } = string.Empty;

    public string CategoryKey { get; private set; } = string.Empty;

    /// <summary>The sections as JSON, written by the application's snapshot serializer.</summary>
    public string ContentSnapshot { get; private set; } = string.Empty;

    /// <summary>What the editor said changed, in their own words. The version list renders this.</summary>
    public string? RevisionNotes { get; private set; }

    /// <summary>Who published this version. Usually the author; staff after a moderation edit.</summary>
    public UserId EditorId { get; private set; } = default!;

    public DateTimeOffset PublishedAt { get; private set; }

    public int WordCount { get; private set; }

    public int ReadingTimeMinutes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<GuideVersion> Create(
        GuideVersionId id,
        GuideId guideId,
        int versionNumber,
        GuideTitle title,
        string? summary,
        GuideLanguage language,
        string categoryKey,
        string? contentSnapshot,
        string? revisionNotes,
        UserId editorId,
        DateTimeOffset publishedAt,
        int wordCount,
        int readingTimeMinutes)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(editorId);

        if (versionNumber < 1)
        {
            return Result.Failure<GuideVersion>(GuideErrors.VersionNotFound);
        }

        if (string.IsNullOrWhiteSpace(contentSnapshot))
        {
            return Result.Failure<GuideVersion>(GuideErrors.SnapshotInvalid);
        }

        if (string.IsNullOrWhiteSpace(categoryKey))
        {
            return Result.Failure<GuideVersion>(GuideErrors.CategoryKeyInvalid);
        }

        var notes = revisionNotes?.Trim();

        if (notes is { Length: > GuideLimits.MaxRevisionNotes })
        {
            return Result.Failure<GuideVersion>(GuideErrors.RevisionNotesInvalid);
        }

        return Result.Success(new GuideVersion(
            id,
            guideId,
            versionNumber,
            title.Value,
            string.IsNullOrWhiteSpace(summary) ? null : summary.Trim(),
            language.Code,
            categoryKey,
            contentSnapshot,
            string.IsNullOrEmpty(notes) ? null : notes,
            editorId,
            publishedAt,
            Math.Max(0, wordCount),
            Math.Max(1, readingTimeMinutes)));
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
