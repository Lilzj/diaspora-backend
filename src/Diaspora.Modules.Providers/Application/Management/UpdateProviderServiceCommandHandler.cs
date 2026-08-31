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
/// Edits one thing a provider offers.
/// <para>
/// Unlike <see cref="AddProviderServiceCommandHandler"/>, the domain's <c>UpdateService</c> takes
/// pricing and delivery mode as non-optional: an edit states the whole service, so an omitted
/// price means "unspecified" rather than "leave whatever was there". That is the safer reading
/// for money — a partial edit that silently keeps a stale price is how a member ends up
/// advertising last year's rate — and it matches how the client's editor works, which submits
/// the whole row.
/// </para>
/// <para>
/// The delivery mode falls back to the provider's own when the caller omits it, which is the
/// same default <c>AddService</c> applies.
/// </para>
/// </summary>
public sealed class UpdateProviderServiceCommandHandler
    : ICommandHandler<UpdateProviderServiceCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ServiceCategoryResolver _categories;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;

    public UpdateProviderServiceCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ServiceCategoryResolver categories,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _categories = categories;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
    }

    public async Task<Result<ProviderResponse>> Handle(
        UpdateProviderServiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(
            ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var caller = _authorization.RequireRole(provider, ProviderRole.Editor);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var serviceId = ProviderServiceId.From(command.ServiceId);

        // Checked before the wire types are resolved so a request naming a service that is not
        // this provider's fails as `ServiceNotFound` rather than as whichever field happened to
        // be malformed. Two different causes must not share one message.
        if (provider.FindService(serviceId) is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.ServiceNotFound);
        }

        var request = command.Request;

        var pricing = PricingRequestMapper.FromRequest(request.Pricing);

        if (pricing.IsFailure)
        {
            return Result.Failure<ProviderResponse>(pricing.Error);
        }

        ServiceCategoryId? categoryId = null;

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var category = await _categories.ResolveForWriteAsync(request.Category, cancellationToken);

            if (category.IsFailure)
            {
                return Result.Failure<ProviderResponse>(category.Error);
            }

            categoryId = category.Value.Id;
        }

        var deliveryMode = provider.DeliveryMode;

        if (!string.IsNullOrWhiteSpace(request.DeliveryMode))
        {
            var parsed = ProviderWireNames.ParseDeliveryMode(request.DeliveryMode);

            if (parsed.IsFailure)
            {
                return Result.Failure<ProviderResponse>(parsed.Error);
            }

            deliveryMode = parsed.Value;
        }

        var updated = provider.UpdateService(
            serviceId,
            request.Name,
            pricing.Value,
            request.Description,
            categoryId,
            deliveryMode,
            request.DurationMinutes,
            caller.Value);

        if (updated.IsFailure)
        {
            return Result.Failure<ProviderResponse>(updated.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
