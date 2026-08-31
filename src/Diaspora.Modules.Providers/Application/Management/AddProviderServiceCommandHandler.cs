using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// Adds one thing a provider offers — a dish, a treatment, a consultation.
/// <para>
/// Requires <see cref="ProviderRole.Editor"/>, the same role that may edit any other descriptive
/// content. Listing what you sell is not a more privileged act than renaming the business.
/// </para>
/// <para>
/// The aggregate owns every rule that matters: the per-provider ceiling
/// (<c>ProviderErrors.ServiceLimitReached</c>), the duplicate check against
/// <c>DedupeKey</c> (<c>ServiceDuplicate</c>), the display ordering, the service count on
/// <c>Statistics</c> and the profile-completion recalculation. This handler resolves the wire
/// types and calls it; it does not reimplement any of that.
/// </para>
/// </summary>
public sealed class AddProviderServiceCommandHandler
    : ICommandHandler<AddProviderServiceCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ServiceCategoryResolver _categories;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IGuidGenerator _guids;

    public AddProviderServiceCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ServiceCategoryResolver categories,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IGuidGenerator guids)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _categories = categories;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
        _guids = guids;
    }

    public async Task<Result<ProviderResponse>> Handle(
        AddProviderServiceCommand command, CancellationToken cancellationToken)
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

        var request = command.Request;

        var pricing = PricingRequestMapper.FromRequest(request.Pricing);

        if (pricing.IsFailure)
        {
            return Result.Failure<ProviderResponse>(pricing.Error);
        }

        // Null rather than the provider's own category when the caller sends nothing: the
        // aggregate treats a null category as "same as the provider", and resolving it here
        // would pin a copy that stops following the provider if the provider is recategorised.
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

        // Same reasoning: omitted means "inherit the provider's mode", which `AddService`
        // applies itself. Only an explicitly sent value is parsed.
        ServiceDeliveryMode? deliveryMode = null;

        if (!string.IsNullOrWhiteSpace(request.DeliveryMode))
        {
            var parsed = ProviderWireNames.ParseDeliveryMode(request.DeliveryMode);

            if (parsed.IsFailure)
            {
                return Result.Failure<ProviderResponse>(parsed.Error);
            }

            deliveryMode = parsed.Value;
        }

        var added = provider.AddService(
            ProviderServiceId.From(_guids.NewGuid()),
            request.Name,
            pricing.Value,
            request.Description,
            categoryId,
            deliveryMode,
            request.DurationMinutes,
            caller.Value);

        if (added.IsFailure)
        {
            return Result.Failure<ProviderResponse>(added.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
