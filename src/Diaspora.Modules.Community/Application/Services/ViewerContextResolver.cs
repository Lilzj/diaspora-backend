using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Modules.Location.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Assembles the reader's context once per request.
/// <para>
/// <b>This class is where B6 consumes the other modules and refuses to reimplement them.</b>
/// Verification comes from the kernel's <c>ITrustService</c>, which Trust owns. Residency comes
/// from Location's <c>IResidencyDirectory</c>. The follow graph is B6's own. Nothing here
/// computes a trust level, decides what verified means, or works out where a member is — it
/// asks the module that owns each question and caches the answers for the duration of one
/// request.
/// </para>
/// <para>
/// Every cross-module call is defensive. A reader whose residency lookup fails should see a
/// <em>narrower</em> feed, not an error page: an empty verified-country list means
/// country-scoped content is withheld, which is the safe direction to fail in.
/// </para>
/// </summary>
public sealed class ViewerContextResolver
{
    private readonly ICurrentUser _currentUser;
    private readonly ITrustService _trust;
    private readonly IResidencyDirectory _residency;
    private readonly IFollowRepository _follows;
    private readonly IGeographicScopeResolver _scopes;
    private readonly ILogger<ViewerContextResolver> _logger;

    private ViewerContext? _cached;

    public ViewerContextResolver(
        ICurrentUser currentUser,
        ITrustService trust,
        IResidencyDirectory residency,
        IFollowRepository follows,
        IGeographicScopeResolver scopes,
        ILogger<ViewerContextResolver> logger)
    {
        _currentUser = currentUser;
        _trust = trust;
        _residency = residency;
        _follows = follows;
        _scopes = scopes;
        _logger = logger;
    }

    public async Task<ViewerContext> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            _cached = ViewerContext.Anonymous;

            return _cached;
        }

        var trustLevel = await SafeAsync(
            () => _trust.GetTrustLevelAsync(userId, cancellationToken),
            Diaspora.Platform.Services.Models.TrustLevel.None,
            "trust level");

        var residencies = await SafeAsync(
            () => _residency.GetResidenciesAsync(userId, cancellationToken),
            (IReadOnlyList<ResidencySummary>)[],
            "residency");

        var followed = await SafeAsync(
            () => _follows.ListTargetsAsync(
                userId, FollowTargetType.Member, includeMuted: true, cancellationToken),
            (IReadOnlyList<Guid>)[],
            "follow graph");

        var current = residencies.FirstOrDefault(residency => residency.IsCurrentResidence);

        // The reader's own saved location, as Location stores it: its point and its country.
        // Asked with nothing but the reader, so the resolver answers from the profile alone.
        var active = await SafeAsync(
            async () =>
            {
                var scope = await _scopes.ResolveForDiscoveryAsync(
                    new GeographicScopeRequest { UserId = userId }, cancellationToken);

                return scope.IsSuccess ? scope.Value : GeographicScope.Unbounded;
            },
            GeographicScope.Unbounded,
            "active location");

        _cached = new ViewerContext
        {
            UserId = userId,
            IsVerified = trustLevel >= Diaspora.Platform.Services.Models.TrustLevel.Verified,
            TrustLevel = (int)trustLevel,
            Tier = ResolveTier(),
            VerifiedCountries = residencies
                .Where(residency => residency.IsVerified)
                .Select(residency => residency.CountryCode)
                .ToList(),
            CountryCode = current?.CountryCode ?? active.CountryCode?.Value,
            Latitude = active.Center?.Latitude,
            Longitude = active.Center?.Longitude,
            FollowedMemberIds = followed.ToHashSet(),

            // Empty until Identity publishes a block-list contract. See ViewerContext.
            BlockedMemberIds = []
        };

        return _cached;
    }

    /// <summary>
    /// The audience levels this reader qualifies for.
    /// <para>
    /// Computed once and pushed into every feed query, so a feed provider cannot get it wrong
    /// and two feeds cannot disagree. Followers-only and custom content is deliberately absent:
    /// neither can be decided from the reader alone — one needs the author, the other needs a
    /// stored rule — so both are excluded from the coarse query filter and evaluated per item.
    /// </para>
    /// </summary>
    public static IReadOnlyCollection<VisibilityLevel> AllowedLevels(ViewerContext viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);

        var levels = new List<VisibilityLevel> { VisibilityLevel.Public };

        if (!viewer.IsAuthenticated)
        {
            return levels;
        }

        levels.Add(VisibilityLevel.Members);

        if (viewer.IsVerified)
        {
            levels.Add(VisibilityLevel.VerifiedMembers);
        }

        if (viewer.CountryCode is not null || viewer.VerifiedCountries.Count > 0)
        {
            levels.Add(VisibilityLevel.Country);
        }

        // Neighbourhood content is measured per item against the reader's active location (or,
        // for content with no pin, their country), so it is admitted to the coarse filter for any
        // signed-in reader and narrowed by the feed query and the per-item check.
        levels.Add(VisibilityLevel.Neighborhood);

        levels.Add(VisibilityLevel.Followers);

        return levels;
    }

    private int ResolveTier() =>
        _currentUser.Claims.TryGetValue(
            Diaspora.Contracts.Authorization.PlatformClaimTypes.AccessTier, out var raw)
        && Enum.TryParse<Diaspora.Contracts.Authorization.AccessTier>(
            raw, ignoreCase: true, out var tier)
            ? (int)tier
            : 0;

    private async Task<T> SafeAsync<T>(Func<Task<T>> call, T fallback, string what)
    {
        try
        {
            return await call();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A cross-module read must narrow the feed, never fail the request.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Resolving {What} for the current reader failed. Falling back to the narrower answer.",
                what);

            return fallback;
        }
#pragma warning restore CA1031
    }
}
