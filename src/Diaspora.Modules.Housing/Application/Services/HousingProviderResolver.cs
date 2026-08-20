using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// The one class in B11 that talks to Providers.
/// <para>
/// <b>Linking a provider is checked, never trusted.</b> A member who could name any provider
/// identifier could attach their listings to somebody else's letting agency and inherit its
/// reputation — which on a housing platform is how a fraudster borrows the credibility that
/// persuades a newcomer to send a deposit. The check that the caller may actually act for the
/// provider is the whole reason this class exists.
/// </para>
/// <para>
/// <b>The check is <c>HasRoleAsync</c>, not an owner comparison.</b> B7 already models who may act
/// for a provider and at what authority, and an agency legitimately has staff who are not its
/// owner. Reimplementing role comparison here is exactly what B7's contract summary warns five
/// modules against doing differently.
/// </para>
/// <para>
/// <b>Housing stores the identifier and a cached name, never a copy of the business.</b> The
/// provider's services, opening hours, verification and own listings are B7's; duplicating any of
/// them here would create a second truth that drifts.
/// </para>
/// <para>
/// The dependency is optional. A deployment without the Providers module has no agencies to link,
/// so the directory is injected nullable and a link attempt degrades to a refusal rather than a
/// crash — the same shape B10 uses for Trust.
/// </para>
/// </summary>
public sealed class HousingProviderResolver
{
    /// <summary>
    /// The authority needed to let property as a provider. Manager rather than Owner, because an
    /// agency's lettings manager is precisely the person who lists properties, and requiring the
    /// owner would make the feature unusable for the businesses it exists for.
    /// </summary>
    private const ProviderRole RequiredRole = ProviderRole.Manager;

    private readonly IProviderDirectory? _providers;
    private readonly ILogger<HousingProviderResolver> _logger;

    public HousingProviderResolver(
        ILogger<HousingProviderResolver> logger, IProviderDirectory? providers = null)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>Whether this deployment has a provider directory at all.</summary>
    public bool IsAvailable => _providers is not null;

    /// <summary>
    /// Links a landlord profile to a B7 provider the caller may act for.
    /// <para>
    /// A provider that does not exist and one the caller may not act for answer with the same
    /// error, deliberately: distinguishing them would let a caller enumerate which provider
    /// identifiers are real.
    /// </para>
    /// </summary>
    public async Task<Result> LinkAsync(
        LandlordProfile landlord,
        Guid providerId,
        UserId callerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlord);
        ArgumentNullException.ThrowIfNull(callerId);

        if (providerId == Guid.Empty)
        {
            return landlord.LinkProvider(null, baseRevision: null);
        }

        if (_providers is null)
        {
            return Result.Failure(HousingErrors.LandlordProviderUnknown);
        }

        try
        {
            var permitted = await _providers.HasRoleAsync(
                callerId, ProviderId.From(providerId), RequiredRole, cancellationToken);

            if (!permitted)
            {
                return Result.Failure(HousingErrors.LandlordProviderUnknown);
            }

            return landlord.LinkProvider(providerId, baseRevision: null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A Providers outage must not crash a registration. See the summary.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Resolving provider {ProviderId} failed. The landlord profile was not linked.",
                providerId);

            return Result.Failure(HousingErrors.LandlordProviderUnknown);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Resolves provider identifiers into the references a listing caches.
    /// <para>
    /// Batched through B7's own N+1 guard. Unknown identifiers are dropped rather than refused: a
    /// provider retired between a landlord drafting a listing and publishing it is an ordinary
    /// event, and refusing the whole write over it would lose the other twenty fields they had just
    /// filled in.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<HousingProviderReference>> ResolveAsync(
        IReadOnlyList<Guid>? providerIds, CancellationToken cancellationToken = default)
    {
        if (providerIds is null || providerIds.Count == 0 || _providers is null)
        {
            return [];
        }

        var keys = providerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(ProviderId.From)
            .ToList();

        if (keys.Count == 0)
        {
            return [];
        }

        try
        {
            var found = await _providers.FindManyAsync(keys, cancellationToken);

            return
            [
                .. found.Values.Select(provider => HousingProviderReference.Create(
                    provider.ProviderId, provider.Name, provider.CategoryName))
            ];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Unresolvable providers must not fail a listing write.
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Resolving provider references failed.");

            return [];
        }
#pragma warning restore CA1031
    }
}
