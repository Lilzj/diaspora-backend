using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// The one class in B12 that reads B7's provider directory.
/// <para>
/// <b>This is how the brief's provider requirement is met.</b> A verified provider hosts a
/// conference, a training session or a professional meetup by being named on the event; B7 keeps
/// every fact about the provider itself, and this module stores an identifier and a cached name.
/// </para>
/// <para>
/// <b>Acting on a provider's behalf is B7's question, not this module's.</b> Whether the caller may
/// publish an event as a given provider is answered by <c>IProviderDirectory</c>, which is what
/// stops five verticals disagreeing about what a provider's staff may do.
/// </para>
/// </summary>
public sealed class CommunityLifeProviderResolver
{
    private readonly IProviderDirectory? _providers;

    public CommunityLifeProviderResolver(IProviderDirectory? providers = null) =>
        _providers = providers;

    /// <summary>Provider references for an event. Unknown providers are dropped.</summary>
    public async Task<IReadOnlyList<EventProviderReference>> ResolveAsync(
        IReadOnlyList<Guid>? providerIds, CancellationToken cancellationToken = default)
    {
        if (_providers is null || providerIds is not { Count: > 0 })
        {
            return [];
        }

        var wanted = providerIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Select(ProviderId.From)
            .ToList();

        if (wanted.Count == 0)
        {
            return [];
        }

        var resolved = await _providers.FindManyAsync(wanted, cancellationToken);

        return
        [
            .. resolved.Select(provider =>
                EventProviderReference.Create(
                    provider.Key, provider.Value.Name, role: null))
        ];
    }

    /// <summary>
    /// One provider's display name, for an organization that is also a provider.
    /// <para>
    /// Null when the deployment has no Providers module or the identifier is stale, which the caller
    /// reads as "no provider" rather than as a failure.
    /// </para>
    /// </summary>
    public async Task<string?> NameAsync(
        Guid? providerId, CancellationToken cancellationToken = default)
    {
        if (_providers is null || providerId is not { } id || id == Guid.Empty)
        {
            return null;
        }

        var provider = await _providers.FindAsync(ProviderId.From(id), cancellationToken);

        return provider?.Name;
    }

    /// <summary>
    /// Whether the caller may act on a provider's behalf. B7 decides, not this module.
    /// <para>
    /// <b>Asks whether the caller holds a role on the provider, not merely whether the provider
    /// exists.</b> Existence is not authorization: a check that stopped there would let anybody hang
    /// their organization off somebody else's verified business, inheriting a badge members rely on
    /// before handing over a collection. <c>Editor</c> is the floor — the same role B7 requires to
    /// change a provider's own page.
    /// </para>
    /// <para>
    /// A deployment without B7 loaded has no providers to act for, so the answer is no rather than
    /// an exception.
    /// </para>
    /// </summary>
    public async Task<bool> CanActOnBehalfAsync(
        Guid providerId, UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (_providers is null || providerId == Guid.Empty)
        {
            return false;
        }

        return await _providers.HasRoleAsync(
            userId, ProviderId.From(providerId), ProviderRole.Editor, cancellationToken);
    }
}
