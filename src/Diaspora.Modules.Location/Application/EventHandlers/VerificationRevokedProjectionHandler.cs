using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Records a residency verification Trust has withdrawn for cause.
/// <para>
/// Same two routes to the country as the expiry handler: Trust's <c>TargetCountry</c> from
/// <b>B3.0.1</b> first, then identifier matching for payloads published before it.
/// </para>
/// <para>
/// Revocation is the one transition worth logging at information level: a withdrawn residency
/// removes a verified badge from a member's profile, and "why did my badge disappear" deserves
/// an answer in the logs.
/// </para>
/// </summary>
public sealed class VerificationRevokedProjectionHandler
    : IntegrationEventHandler<VerificationRevokedIntegrationEvent>
{
    private const string CountryVerificationType = "Country";

    private readonly ResidencyProjector _projector;
    private readonly IVerifiedResidencyRepository _residencies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<VerificationRevokedProjectionHandler> _logger;

    public VerificationRevokedProjectionHandler(
        ResidencyProjector projector,
        IVerifiedResidencyRepository residencies,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<VerificationRevokedProjectionHandler> logger)
    {
        _projector = projector;
        _residencies = residencies;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var country = await ApplyByCountryAsync(integrationEvent, cancellationToken)
                      ?? await ApplyByVerificationIdAsync(integrationEvent, cancellationToken);

        if (country is null)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Residency verification in {Country} revoked for {UserId}: {Reason}.",
            country, integrationEvent.UserId, integrationEvent.Reason);
    }

    /// <summary>The B3.0.1 path. Returns the country when the projection changed.</summary>
    private async Task<string?> ApplyByCountryAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (!string.Equals(
                integrationEvent.VerificationType, CountryVerificationType, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(integrationEvent.TargetCountry))
        {
            return null;
        }

        var applied = await _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.TargetCountry,
            ResidencyVerificationState.Revoked,
            integrationEvent.VerificationId,
            integrationEvent.RevokedAt,
            _clock.UtcNow,
            expiresAt: null,
            integrationEvent.Reason,
            cancellationToken);

        return applied ? integrationEvent.TargetCountry : null;
    }

    /// <summary>The pre-B3.0.1 path: match the row that recorded this verification id.</summary>
    private async Task<string?> ApplyByVerificationIdAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var residencies = await _residencies.ListByUserAsync(
            UserId.From(integrationEvent.UserId), cancellationToken);

        var match = residencies.FirstOrDefault(
            residency => residency.VerificationId == integrationEvent.VerificationId);

        if (match is null)
        {
            return null;
        }

        var tracked = await _residencies.GetAsync(
            match.UserId, match.CountryCode, cancellationToken);

        if (tracked is null)
        {
            return null;
        }

        var changed = tracked.Apply(
            ResidencyVerificationState.Revoked,
            integrationEvent.VerificationId,
            integrationEvent.RevokedAt,
            _clock.UtcNow,
            expiresAt: null,
            integrationEvent.Reason);

        return changed ? tracked.CountryCode.Value : null;
    }
}
