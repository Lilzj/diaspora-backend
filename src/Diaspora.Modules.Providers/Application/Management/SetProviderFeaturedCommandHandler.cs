using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// Sets or clears a provider's featured flag.
/// <para>
/// <b>Only a provider members can actually reach may be featured.</b> The carousel is a promise
/// that these are worth looking at, and a suspended or unpublished business in it is either a
/// dead link or an advertisement for something the platform took down. Featuring is therefore
/// refused unless the provider is active; removing one never is, because a provider that has
/// just been suspended must be removable from the carousel by the same decision.
/// </para>
/// <para>
/// Staff-only, and the check sits here alongside the reachability rule rather than on the route,
/// for the reason the status handler gives: the endpoint's policy decides who may call it, the
/// handler decides what they may ask for.
/// </para>
/// </summary>
public sealed class SetProviderFeaturedCommandHandler
    : ICommandHandler<SetProviderFeaturedCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;

    public SetProviderFeaturedCommandHandler(
        IUnitOfWork unitOfWork,
        IProviderRepository providers,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _providers = providers;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
    }

    public async Task<Result<ProviderResponse>> Handle(
        SetProviderFeaturedCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_authorization.IsStaff)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.Forbidden);
        }

        var provider = await _providers.GetAsync(
            ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        // Featuring something members cannot open would put a dead link on the carousel.
        if (command.Request.IsFeatured && provider.Status != ProviderStatus.Active)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFeaturable);
        }

        provider.SetFeatured(command.Request.IsFeatured);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: false, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
