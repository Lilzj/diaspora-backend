using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// Edits a provider's name, type, category and delivery mode.
/// <para>
/// Requires <see cref="ProviderRole.Editor"/> — the lowest role that may change descriptive
/// content. The slug is deliberately untouched; see <c>ChangeProviderSlugCommand</c> for why a
/// rename must not silently break every link.
/// </para>
/// </summary>
public sealed class UpdateProviderCommandHandler
    : ICommandHandler<UpdateProviderCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ServiceCategoryResolver _categories;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IGuidGenerator _guids;

    public UpdateProviderCommandHandler(
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
        UpdateProviderCommand command, CancellationToken cancellationToken)
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

        var name = ProviderName.Create(request.Name);

        if (name.IsFailure)
        {
            return Result.Failure<ProviderResponse>(name.Error);
        }

        var providerType = ProviderWireNames.ParseProviderType(request.ProviderType);

        if (providerType.IsFailure)
        {
            return Result.Failure<ProviderResponse>(providerType.Error);
        }

        var deliveryMode = ProviderWireNames.ParseDeliveryMode(request.DeliveryMode);

        if (deliveryMode.IsFailure)
        {
            return Result.Failure<ProviderResponse>(deliveryMode.Error);
        }

        var category = await _categories.ResolveForWriteAsync(request.Category, cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<ProviderResponse>(category.Error);
        }

        var updated = provider.Update(
            name.Value, category.Value.Id, providerType.Value, deliveryMode.Value, caller.Value);

        if (updated.IsFailure)
        {
            return Result.Failure<ProviderResponse>(updated.Error);
        }

        // Null tags means "leave them alone"; an empty list means "clear them". A caller editing
        // a name should not have to resend the tags to keep them.
        if (request.Tags is not null)
        {
            var tags = provider.ReplaceTags(
                request.Tags.Select(tag => (ProviderTagId.From(_guids.NewGuid()), tag)),
                caller.Value);

            if (tags.IsFailure)
            {
                return Result.Failure<ProviderResponse>(tags.Error);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
