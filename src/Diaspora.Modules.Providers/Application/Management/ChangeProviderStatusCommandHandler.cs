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
/// Moves a provider through its lifecycle, for the provider's own managers.
/// <para>
/// <b>The owner's route, and only the owner's (7 Nov 2026).</b> Publish, pause, close and
/// archive. Suspending and lifting a suspension are decisions made <em>about</em> a provider by
/// somebody who is not part of it, so they are not reachable here by anybody: staff decide the
/// provider's Trust moderation case, with a reason and a note, and the decision is carried out by
/// <c>ProviderModerationTarget</c>. Staff who are not
/// members of the provider are refused by the owner check like any outsider, and a manager who
/// happens to hold a staff role cannot lift their own suspension here.
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
            // Publishing a suspended provider is refused by the aggregate; only staff lift a
            // suspension, by restoring it on its Trust moderation case.
            ProviderStatus.Active => Publish(provider, now, caller.Value),

            ProviderStatus.Paused => provider.Pause(now, reason, caller.Value),

            ProviderStatus.Closed => provider.Close(now, reason, caller.Value),

            // A staff decision, never the owner's. See the class summary.
            ProviderStatus.Suspended => Result.Failure(ProviderErrors.Forbidden),

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
}
