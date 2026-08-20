using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Records a residency Trust has approved.
/// <para>
/// <b>This handler is the entire integration with B3.</b> Location subscribes to a published
/// contract in <c>Diaspora.Contracts.Trust.V1.Events</c> and references nothing of the Trust
/// module — no service, no repository, no type. That is what makes the boundary real rather
/// than declared: the two modules could be deployed as separate services tomorrow and this
/// file would not change.
/// </para>
/// <para>
/// Only country verifications are projected. A diaspora or identity verification says
/// something true about a member, but it does not say they live anywhere in particular, and
/// recording it as residency would put a verified badge on a country nobody ever claimed.
/// </para>
/// </summary>
public sealed class VerificationApprovedProjectionHandler
    : IntegrationEventHandler<VerificationApprovedIntegrationEvent>
{
    private const string CountryVerificationType = "Country";

    private readonly ResidencyProjector _projector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<VerificationApprovedProjectionHandler> _logger;

    public VerificationApprovedProjectionHandler(
        ResidencyProjector projector,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<VerificationApprovedProjectionHandler> logger)
    {
        _projector = projector;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        VerificationApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (!string.Equals(
                integrationEvent.VerificationType, CountryVerificationType, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(integrationEvent.TargetCountry))
        {
            return;
        }

        var applied = await _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.TargetCountry,
            ResidencyVerificationState.Verified,
            integrationEvent.VerificationId,
            integrationEvent.ApprovedAt,
            _clock.UtcNow,
            integrationEvent.ExpiresAt,
            reason: null,
            cancellationToken);

        if (!applied)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Residency in {Country} verified for {UserId}.",
            integrationEvent.TargetCountry, integrationEvent.UserId);
    }
}
