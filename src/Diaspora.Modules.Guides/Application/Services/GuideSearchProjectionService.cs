using System.Text;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Extensibility.Search;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Builds the documents a search engine indexes, and feeds them to whichever engine is
/// registered. B8 builds them and stops — nothing that tokenises, scores or embeds lives
/// anywhere in this module.
/// <para>
/// Called after the guide write has committed, from the publish, update, archive and delete
/// paths. <b>Failures degrade to a stale index entry, never to a rolled-back write</b>: the
/// search index is a projection of the write model, and a projection that could veto its
/// source would have the dependency backwards.
/// </para>
/// <para>
/// The popularity score is computed here, once, so every engine ranks from the same number —
/// two engines deriving their own popularity from the same counters would disagree exactly
/// where ranking is decided.
/// </para>
/// </summary>
public sealed class GuideSearchProjectionService
{
    private readonly IGuideSearchEngine _engine;
    private readonly ILogger<GuideSearchProjectionService> _logger;

    public GuideSearchProjectionService(
        IGuideSearchEngine engine, ILogger<GuideSearchProjectionService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>
    /// Brings the index in line with the guide: discoverable guides are upserted, everything
    /// else — drafts, archived, hidden, deleted, private — is removed.
    /// </summary>
    public async Task ProjectAsync(Guide guide, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guide);

        try
        {
            if (guide.IsDiscoverable)
            {
                await _engine.IndexAsync(BuildDocument(guide), cancellationToken);
            }
            else
            {
                await _engine.RemoveAsync(guide.Id.Value, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A projection failure is a stale index entry, never a failed write.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Projecting guide {GuideId} into the search index failed. The entry is stale.",
                guide.Id.Value);
        }
#pragma warning restore CA1031
    }

    /// <summary>Removes a guide from the index by identifier — the hard-delete path.</summary>
    public async Task RemoveAsync(Guid guideId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _engine.RemoveAsync(guideId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Removing guide {GuideId} from the search index failed. The entry is stale.",
                guideId);
        }
#pragma warning restore CA1031
    }

    /// <summary>The flat document an engine ingests, denormalised from the aggregate.</summary>
    public static GuideSearchDocument BuildDocument(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        return new GuideSearchDocument
        {
            GuideId = guide.Id.Value,
            Slug = guide.Slug.Value,
            Title = guide.Title.Value,
            Summary = guide.Summary?.Summary,
            BodyText = BodyTextOf(guide),
            Tags = guide.Tags.Select(tag => tag.Slug).ToList(),
            CategoryKey = guide.CategoryKey,
            CountryCode = guide.Location.CountryCode,
            RegionName = guide.Location.RegionName,
            CityName = guide.Location.CityName,
            Language = guide.Language.Code,
            AuthorId = guide.AuthorId.Value,
            IsVerified = guide.TrustMark.State == GuideVerificationState.Verified,
            IsFeatured = guide.IsFeatured,
            PopularityScore = PopularityOf(guide),
            PublishedAt = guide.PublishedAt,
            UpdatedAt = guide.ModifiedAt
        };
    }

    /// <summary>
    /// The engagement number an engine ranks by. Helpful votes count most (the deliberate
    /// signal), bookmarks next (a stronger commitment than a view), views on a logarithm so a
    /// viral week does not drown a year of usefulness. Deterministic and undecayed — recency
    /// is the ranking strategy's job, not the document's.
    /// </summary>
    public static double PopularityOf(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        var engagement = guide.Engagement;

        return (engagement.HelpfulCount * 4d)
            + (engagement.BookmarkCount * 2d)
            + Math.Log10(Math.Max(0, engagement.Views) + 1d);
    }

    /// <summary>
    /// The concatenated prose, in reading order. The same kinds the word count reads —
    /// captions, table payloads and code are not what a member searching "work permit" means.
    /// </summary>
    private static string BodyTextOf(Guide guide)
    {
        var builder = new StringBuilder();

        foreach (var section in guide.Sections)
        {
            builder.Append(section.Title).Append('\n');

            foreach (var block in section.Blocks)
            {
                var isProse = block.Kind
                    is GuideBlockKind.Markdown
                    or GuideBlockKind.RichText
                    or GuideBlockKind.Callout
                    or GuideBlockKind.Warning
                    or GuideBlockKind.Tip;

                if (isProse && !string.IsNullOrWhiteSpace(block.Content))
                {
                    builder.Append(block.Content).Append('\n');
                }
            }
        }

        return builder.ToString();
    }
}
