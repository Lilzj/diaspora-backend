using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Management;

/// <summary>
/// Moves a provider through its lifecycle.
/// <para>
/// <b>Two transitions are staff-only, and the check is here rather than on the endpoint.</b>
/// A suspension and its reversal are decisions made <em>about</em> a provider by somebody who is
/// not part of it; the endpoint's policy gates who may call the route at all, and this decides
/// which transitions each caller may ask for. Splitting them would mean either a second route
/// for staff or a manager who can lift their own suspension.
/// </para>
/// </summary>
public sealed class ChangeProviderStatusCommandHandler
    : ICommandHandler<ChangeProviderStatusCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IClock _clock;

    public ChangeProviderStatusCommandHandler(
        IProviderRepository providers,
        IUnitOfWork unitOfWork,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IClock clock)
    {
        _providers = providers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<Result<ProviderResponse>> Handle(
        ChangeProviderStatusCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = await _providers.GetAsync(
            ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var status = ProviderWireNames.ParseStatus(command.Request.Status);

        if (status.IsFailure)
        {
            return Result.Failure<ProviderResponse>(status.Error);
        }

        var caller = _authorization.RequireRole(provider, ProviderRole.Manager);

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        var now = _clock.UtcNow;
        var reason = command.Request.Reason;

        var moved = status.Value switch
        {
            ProviderStatus.Active when provider.Status == ProviderStatus.Suspended =>
                RequireStaff(() => provider.Reinstate(now, caller.Value)),

            ProviderStatus.Active => Publish(provider, now, caller.Value),

            ProviderStatus.Paused => provider.Pause(now, reason, caller.Value),

            ProviderStatus.Closed => provider.Close(now, reason, caller.Value),

            ProviderStatus.Suspended =>
                RequireStaff(() => provider.Suspend(now, reason ?? "Suspended by staff.", caller.Value)),

            ProviderStatus.Archived => provider.Archive(now, reason, caller.Value),

            // Draft and PendingReview are not reachable by request. A provider does not go back
            // to being a draft once it has been seen; unpublishing is what Paused is for.
            _ => Result.Failure(ProviderErrors.EnumUnknown)
        };

        if (moved.IsFailure)
        {
            return Result.Failure<ProviderResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: true, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }

    private static Result Publish(Provider provider, DateTimeOffset now, SharedKernel.Identity.UserId caller)
    {
        var wasUnpublished = provider.PublishedAt is null;

        var published = provider.Publish(now, caller);

        if (published.IsSuccess && wasUnpublished)
        {
            ProviderTelemetry.ProvidersPublished.Add(
                1,
                new KeyValuePair<string, object?>("type", provider.Type.ToString()),
                new KeyValuePair<string, object?>("category", provider.CategoryId.Value));
        }

        return published;
    }

    private Result RequireStaff(Func<Result> action) =>
        _authorization.IsStaff ? action() : Result.Failure(ProviderErrors.Forbidden);
}
