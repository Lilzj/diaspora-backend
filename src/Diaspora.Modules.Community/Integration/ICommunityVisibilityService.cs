using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// Audience evaluation, for a module that owns content.
/// <para>
/// A vertical wanting followers-only listings or verified-members-only events should not
/// reimplement the reader-side resolution — the verification lookup, the residency lookup, the
/// follow check, the block check — that B6 already does on every read. It declares a level and
/// asks.
/// </para>
/// </summary>
public interface ICommunityVisibilityService
{
    Task<bool> CanViewAsync(
        ContentReference content,
        Guid authorId,
        ContentAudience audience,
        UserId? viewerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Filters a set down to what the reader may see. The batch form, for a listing page.
    /// </summary>
    Task<IReadOnlySet<string>> FilterVisibleAsync(
        IReadOnlyCollection<(ContentReference Content, Guid AuthorId, ContentAudience Audience)> items,
        UserId? viewerId,
        CancellationToken cancellationToken = default);

    /// <summary>The audience levels this reader qualifies for. What a feed query filters on.</summary>
    Task<IReadOnlyCollection<VisibilityLevel>> ResolveAllowedLevelsAsync(
        UserId? viewerId, CancellationToken cancellationToken = default);
}
