using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Discovery;

/// <summary>
/// Reads the caller's own providers.
/// <para>
/// <b>Nothing new in persistence.</b> <see cref="IProviderRepository.ListForMemberAsync"/> already
/// existed, documented as *"Every provider a member holds any role on. The 'My Listings' screen."*
/// No route had ever called it, so the client had no way to find the provider it owns — which is
/// what made <c>GET /providers/me/reviews</c> a gap in the first place. The capability was one
/// endpoint away from a repository method written for exactly this.
/// </para>
/// <para>
/// <b>Any role, not just owner.</b> A member managing a family business's page should find it here;
/// the repository method already decided that and this does not narrow it.
/// </para>
/// </summary>
public sealed class ListMyProvidersQueryHandler
    : IQueryHandler<ListMyProvidersQuery, IReadOnlyList<ProviderSummaryResponse>>
{
    /// <summary>The per-owner ceiling is small, so this is a generous bound rather than a page.</summary>
    private const int DefaultLimit = 25;
    private const int MaxLimit = 100;

    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _context;

    public ListMyProvidersQueryHandler(
        IProviderRepository providers,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder context)
    {
        _providers = providers;
        _authorization = authorization;
        _context = context;
    }

    public async Task<Result<IReadOnlyList<ProviderSummaryResponse>>> Handle(
        ListMyProvidersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ProviderSummaryResponse>>(caller.Error);
        }

        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);

        var providers = await _providers.ListForMemberAsync(caller.Value, limit, cancellationToken);

        // An empty list is the answer for a member who runs nothing, and it is a *different*
        // answer from the 200-with-zero-items that `?ownerId=` used to give — that one meant the
        // filter was dropped. This one means what it says.
        if (providers.Count == 0)
        {
            return Result.Success<IReadOnlyList<ProviderSummaryResponse>>([]);
        }

        var context = await _context.BuildAsync(
            providers, languageCode: query.LanguageCode, cancellationToken: cancellationToken);

        return Result.Success<IReadOnlyList<ProviderSummaryResponse>>(
            [.. providers.Select(provider => ProviderMapper.ToSummary(provider, context))]);
    }
}
