using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.Modules.Guides.Integration;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

// The domain's GuideSummary is a value object — the author's own abstract of the guide. The
// published contract of the same name is the flat card consumers read. Both names are right in
// their own layer; the alias states which one this file implements.
using GuideSummary = Diaspora.Modules.Guides.Integration.GuideSummary;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// The read surface other modules use to look guides up.
/// <para>
/// Only published, unhidden, undeleted guides resolve — a consumer holding the id of a draft
/// gets null, exactly as it would for a guide that never existed, because the directory is a
/// public surface and drafts have an audience of one.
/// </para>
/// <para>
/// <c>FindManyAsync</c> resolves per guide today: the repository publishes no batched read,
/// and this module does not reach around it into persistence. The loop is bounded by the
/// caller's list and documented here so the batched repository method, when persistence grows
/// one, has exactly one call site to improve.
/// </para>
/// </summary>
public sealed class GuideDirectoryService : IGuideDirectory
{
    private readonly IGuideRepository _guides;

    public GuideDirectoryService(IGuideRepository guides) => _guides = guides;

    public async Task<GuideSummary?> FindAsync(
        GuideId guideId, CancellationToken cancellationToken = default)
    {
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        return guide is null || !IsResolvable(guide) ? null : ToSummary(guide);
    }

    public async Task<GuideSummary?> FindBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var parsed = GuideSlug.Create(slug);

        if (parsed.IsFailure)
        {
            return null;
        }

        var guide = await _guides.GetBySlugAsync(parsed.Value, cancellationToken);

        return guide is null || !IsResolvable(guide) ? null : ToSummary(guide);
    }

    public async Task<IReadOnlyDictionary<Guid, GuideSummary>> FindManyAsync(
        IReadOnlyCollection<GuideId> guideIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guideIds);

        var found = new Dictionary<Guid, GuideSummary>(guideIds.Count);

        foreach (var guideId in guideIds.Distinct())
        {
            var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

            if (guide is not null && IsResolvable(guide))
            {
                found[guide.Id.Value] = ToSummary(guide);
            }
        }

        return found;
    }

    public async Task<bool> ExistsAsync(
        GuideId guideId, CancellationToken cancellationToken = default)
    {
        var guide = await _guides.GetForReadAsync(guideId, cancellationToken);

        return guide is not null && IsResolvable(guide);
    }

    /// <summary>
    /// What the directory answers for: published, unhidden, undeleted. Unlisted guides
    /// resolve — a consumer holding the id evidently has the link — but private ones do not.
    /// </summary>
    private static bool IsResolvable(Guide guide) =>
        !guide.IsDeleted
        && guide.Status == GuideStatus.Published
        && guide.ModerationState != GuideModerationState.Hidden
        && guide.Visibility != GuideVisibility.Private;

    private static GuideSummary ToSummary(Guide guide) => new()
    {
        GuideId = guide.Id.Value,
        Slug = guide.Slug.Value,
        Title = guide.Title.Value,
        Summary = guide.Summary?.Summary,
        CategoryKey = guide.CategoryKey,
        CountryCode = guide.Location.CountryCode,
        Language = guide.Language.Code,
        AuthorId = guide.AuthorId.Value,
        IsVerified = guide.TrustMark.State == GuideVerificationState.Verified,
        IsFeatured = guide.IsFeatured,
        PublishedAt = guide.PublishedAt,
        HeroMediaId = HeroOf(guide)
    };

    /// <summary>The hero image, falling back to the cover — what an embed card renders.</summary>
    private static Guid? HeroOf(Guide guide)
    {
        var references = guide.MediaReferences;

        return references.FirstOrDefault(reference => reference.Role == GuideMediaRole.Hero)?.MediaId
            ?? references.FirstOrDefault(reference => reference.Role == GuideMediaRole.Cover)?.MediaId;
    }
}
