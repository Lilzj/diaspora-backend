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
/// Suspends a provider, or lifts its suspension. The staff route.
/// <para>
/// <b>Staff no longer pass the owner check (7 Nov 2026).</b> Until then a moderator suspended a
/// provider through the owner's status route, which let them through because they were staff,
/// and the reason was optional. This is the route for that decision now: gated by
/// <c>providers:administer</c> on the endpoint, held to staff here as
/// <see cref="SetProviderFeaturedCommandHandler"/> is, and refused without a reason.
/// </para>
/// <para>
/// A suspension's reason is stored as the provider's <c>StatusReason</c>, where the owner's team
/// reads it. A reinstatement's reason travels on the status-changed event, because the provider
/// is no longer suspended and has nothing left to explain.
/// </para>
/// </summary>
public sealed class ModerateProviderCommandHandler
    : ICommandHandler<ModerateProviderCommand, ProviderResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReadContextBuilder _contexts;
    private readonly ProviderMapper _mapper;
    private readonly IClock _clock;

    public ModerateProviderCommandHandler(
        IUnitOfWork unitOfWork,
        IProviderRepository providers,
        ProviderAuthorizationService authorization,
        ProviderReadContextBuilder contexts,
        ProviderMapper mapper,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _providers = providers;
        _authorization = authorization;
        _contexts = contexts;
        _mapper = mapper;
        _clock = clock;
    }

    public async Task<Result<ProviderResponse>> Handle(
        ModerateProviderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderResponse>(caller.Error);
        }

        if (!_authorization.IsStaff)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.Forbidden);
        }

        var reason = command.Request.Reason?.Trim();

        if (string.IsNullOrEmpty(reason))
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.ModerationReasonRequired);
        }

        var provider = await _providers.GetAsync(
            ProviderId.From(command.ProviderId), cancellationToken);

        if (provider is null)
        {
            return Result.Failure<ProviderResponse>(ProviderErrors.NotFound);
        }

        var now = _clock.UtcNow;

        var applied = command.Request.Action?.Trim().ToLowerInvariant() switch
        {
            "suspend" => provider.Suspend(now, reason, caller.Value),

            // Reinstating a provider that is not suspended would answer success for nothing, and a
            // moderator would believe they had lifted something.
            "reinstate" => provider.Status == ProviderStatus.Suspended
                ? provider.Reinstate(now, caller.Value, reason)
                : Result.Failure(ProviderErrors.NotSuspended),

            // A word this route cannot read is refused rather than ignored.
            _ => Result.Failure(ProviderErrors.EnumUnknown)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<ProviderResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var context = await _contexts.BuildAsync(
            [provider], includeViewerReviews: false, cancellationToken: cancellationToken);

        return Result.Success(_mapper.ToResponse(provider, context));
    }
}
