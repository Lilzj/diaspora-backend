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
/// Removes one thing a provider offers.
/// <para>
/// A hard removal rather than a soft one, because <c>ProviderService</c> already carries
/// <c>IsAvailable</c> for the reversible case: a restaurant that has run out of a dish marks it
/// unavailable, and only a restaurant that has stopped selling it deletes it. Keeping a
/// tombstone for the second would put a row in every editor that nobody can act on.
/// </para>
/// <para>
/// The aggregate resequences the remaining services and recalculates the service count and
/// profile completion, so the display order does not develop gaps.
/// </para>
/// </summary>
public sealed class RemoveProviderServiceCommandHandler
    : ICommandHandler<RemoveProviderServiceCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;

    public RemoveProviderServiceCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
    }

    public async Task<Result<ProviderResponse>> Handle(
        RemoveProviderServiceCommand command, CancellationToken cancellationToken)
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

        var removed = provider.RemoveService(
            ProviderServiceId.From(command.ServiceId), caller.Value);

        if (removed.IsFailure)
        {
            return Result.Failure<ProviderResponse>(removed.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
