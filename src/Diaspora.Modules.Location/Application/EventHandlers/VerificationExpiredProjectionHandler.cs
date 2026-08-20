using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Residency;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Records a residency verification that lapsed on its own expiry.
/// <para>
/// Two routes to the country, in order. Trust's <c>TargetCountry</c>, added in <b>B3.0.1</b>,
/// is authoritative and needs no prior state. Failing that — on a payload serialised before
/// B3.0.1 — the lapse is matched to the projection row holding the same
/// <c>VerificationId</c>, which is why
/// <see cref="VerifiedResidency.VerificationId"/> is recorded at approval.
/// </para>
/// <para>
/// The fallback is kept rather than removed because it is the only thing that attributes an
/// in-flight event published before the upgrade, and dropping it would turn a rolling deploy
/// into a window where lapses are silently lost.
/// </para>
/// </summary>
public sealed class VerificationExpiredProjectionHandler
    : IntegrationEventHandler<VerificationExpiredIntegrationEvent>
{
    private const string CountryVerificationType = "Country";

    private readonly ResidencyProjector _projector;
    private readonly IVerifiedResidencyRepository _residencies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VerificationExpiredProjectionHandler(
        ResidencyProjector projector,
        IVerifiedResidencyRepository residencies,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _projector = projector;
        _residencies = residencies;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        VerificationExpiredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var applied = await ApplyByCountryAsync(integrationEvent, cancellationToken)
                      || await ApplyByVerificationIdAsync(integrationEvent, cancellationToken);

        if (applied)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>The B3.0.1 path: the event names the country outright.</summary>
    private async Task<bool> ApplyByCountryAsync(
        VerificationExpiredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        if (!string.Equals(
                integrationEvent.VerificationType, CountryVerificationType, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(integrationEvent.TargetCountry))
        {
            return false;
        }

        return await _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.TargetCountry,
            ResidencyVerificationState.Expired,
            integrationEvent.VerificationId,
            integrationEvent.ExpiredAt,
            _clock.UtcNow,
            expiresAt: integrationEvent.ExpiredAt,
            reason: "expired",
            cancellationToken);
    }

    /// <summary>The pre-B3.0.1 path: match the row that recorded this verification id.</summary>
    private async Task<bool> ApplyByVerificationIdAsync(
        VerificationExpiredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        var residencies = await _residencies.ListByUserAsync(
            UserId.From(integrationEvent.UserId), cancellationToken);

        var match = residencies.FirstOrDefault(
            residency => residency.VerificationId == integrationEvent.VerificationId);

        if (match is null)
        {
            return false;
        }

        // The tracked instance, because the list read is untracked.
        var tracked = await _residencies.GetAsync(
            match.UserId, match.CountryCode, cancellationToken);

        return tracked?.Apply(
            ResidencyVerificationState.Expired,
            integrationEvent.VerificationId,
            integrationEvent.ExpiredAt,
            _clock.UtcNow,
            expiresAt: integrationEvent.ExpiredAt,
            reason: "expired") == true;
    }
}
