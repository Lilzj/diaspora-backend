using System.Text;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Finds a free deep-link handle.
/// <para>
/// Two guides genuinely titled "Open a bank account" is not an error, and neither is a title
/// that transliterates to nothing. Both land here and both come out with a usable handle.
/// </para>
/// <para>
/// <b>The database's unique index is the real guarantee; this is an optimisation.</b> A
/// check-then-insert is a race by construction, so the index still has to exist and still has
/// to be able to refuse. What this avoids is the ordinary case — two people writing similarly
/// titled guides in the same week — turning into a constraint violation and a rolled-back
/// transaction every time.
/// </para>
/// </summary>
public sealed class GuideSlugAllocator
{
    /// <summary>
    /// How many suffixes to try before giving up. Ten is far past any real collision; a
    /// deployment that needed an eleventh "open-a-bank-account" has a different problem.
    /// </summary>
    private const int MaxAttempts = 10;

    /// <summary>What a title that slugifies to nothing falls back to.</summary>
    private const string Fallback = "guide";

    private readonly IGuideRepository _guides;

    public GuideSlugAllocator(IGuideRepository guides) => _guides = guides;

    /// <summary>
    /// Allocates a handle, from the caller's choice or derived from the title.
    /// <para>
    /// A caller-supplied slug that is taken <em>fails</em> rather than being silently suffixed:
    /// somebody who typed a handle wants that handle, and quietly giving them
    /// <c>open-a-bank-account-2</c> is worse than telling them it is gone. A derived slug is
    /// suffixed without comment, because nobody chose it.
    /// </para>
    /// </summary>
    public async Task<Result<GuideSlug>> AllocateAsync(
        string? requestedSlug, GuideTitle title, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(title);

        if (!string.IsNullOrWhiteSpace(requestedSlug))
        {
            var requested = GuideSlug.Create(requestedSlug);

            if (requested.IsFailure)
            {
                return requested;
            }

            return await _guides.SlugExistsAsync(requested.Value, cancellationToken)
                ? Result.Failure<GuideSlug>(GuideErrors.SlugTaken)
                : requested;
        }

        var derived = GuideSlug.Create(Slugify(title.Value));

        if (derived.IsFailure)
        {
            // A validated title always slugifies to something the slug rules accept; this is
            // belt-and-braces for a rule change, not a path with traffic.
            derived = GuideSlug.Create(Fallback);
        }

        var candidate = derived.Value;

        if (!await _guides.SlugExistsAsync(candidate, cancellationToken))
        {
            return Result.Success(candidate);
        }

        for (var suffix = 2; suffix <= MaxAttempts + 1; suffix++)
        {
            var suffixed = candidate.WithSuffix(suffix);

            if (!await _guides.SlugExistsAsync(suffixed, cancellationToken))
            {
                return Result.Success(suffixed);
            }
        }

        return Result.Failure<GuideSlug>(GuideErrors.SlugTaken);
    }

    /// <summary>Lower-cases, hyphenates, collapses runs, and bounds the length.</summary>
    private static string Slugify(string title)
    {
        var builder = new StringBuilder(title.Length);
        var previousWasDash = false;

        foreach (var character in title.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > GuideLimits.MaxSlugLength)
        {
            slug = slug[..GuideLimits.MaxSlugLength].TrimEnd('-');
        }

        return slug.Length >= GuideSlug.MinLength ? slug : Fallback;
    }
}
