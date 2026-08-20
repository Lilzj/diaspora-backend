using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Discovery;

/// <summary>
/// Reads one provider.
/// <para>
/// <b>A provider the caller may not see returns "not found", not "forbidden".</b> The difference
/// matters: a 403 confirms that a provider with that slug exists, which is exactly what somebody
/// probing an unlisted profile wants to learn. A 404 tells them nothing.
/// </para>
/// </summary>
public sealed class GetProviderQueryHandler : IQueryHandler<GetProviderQuery, ProviderResponse>
{
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;

    public GetProviderQueryHandler(
        IProviderRepository providers,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper)
    {
        _providers = providers;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
    }

    public async Task<Result<ProviderResponse>> Handle(
        GetProviderQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var provider = await LoadAsync(query, cancellationToken);

        if (provider is null || !CanSee(provider))
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var context = await _contexts.BuildAsync(
            [provider],
            languageCode: query.LanguageCode,
            includeViewerReviews: true,
            cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }

    private async Task<Provider?> LoadAsync(GetProviderQuery query, CancellationToken cancellationToken)
    {
        if (query.ProviderId is { } providerId)
        {
            return await _providers.GetForReadAsync(ProviderId.From(providerId), cancellationToken);
        }

        var slug = ProviderSlug.Create(query.Slug);

        return slug.IsFailure
            ? null
            : await _providers.GetBySlugAsync(slug.Value, cancellationToken);
    }

    /// <summary>
    /// Whether this caller may read this provider.
    /// <para>
    /// <b>An unlisted provider is readable by direct link</b> — that is the whole point of
    /// unlisted, and it is how a provider shares a profile while it fixes its photographs. A
    /// private one, a draft and an archived one are visible only to their own people and to
    /// staff.
    /// </para>
    /// </summary>
    private bool CanSee(Provider provider)
    {
        if (_authorization.CanSeeUnpublished(provider))
        {
            return true;
        }

        if (provider.IsDeleted || provider.Visibility.Level == ProviderVisibilityLevel.Private)
        {
            return false;
        }

        if (provider.Status is ProviderStatus.Draft or ProviderStatus.Archived or ProviderStatus.Suspended)
        {
            return false;
        }

        // Members-only, country-scoped and area-scoped providers are all readable by any
        // signed-in member on a direct read. Narrowing further belongs in discovery, where the
        // viewer's own geography is resolved; applying it here would make a shared link fail for
        // the friend it was shared with.
        return provider.Visibility.Level == ProviderVisibilityLevel.Public
            || _authorization.CallerId is not null;
    }
}
