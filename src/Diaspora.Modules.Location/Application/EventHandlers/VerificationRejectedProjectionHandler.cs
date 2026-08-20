using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Records a residency Trust has refused.
/// <para>
/// The refusal is attributed to a country by reading <c>TargetCountry</c> off the event —
/// added by Trust in <b>B3.0.1</b> for exactly this consumer. Before it existed this handler
/// deliberately recorded nothing: inferring the country from the member's most recent
/// submission would be wrong the first time someone had two in flight, and wrong invisibly.
/// </para>
/// <para>
/// A payload serialised before B3.0.1 carries no country, so it still records nothing. That is
/// the same conservative behaviour as before rather than a new failure mode, and it is why the
/// property is read rather than required.
/// </para>
/// </summary>
public sealed class VerificationRejectedProjectionHandler
    : IntegrationEventHandler<VerificationRejectedIntegrationEvent>
{
    private const string CountryVerificationType = "Country";

    private readonly ResidencyProjector _projector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VerificationRejectedProjectionHandler(
        ResidencyProjector projector, IUnitOfWork unitOfWork, IClock clock)
    {
        _projector = projector;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        VerificationRejectedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
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
            ResidencyVerificationState.Rejected,
            integrationEvent.VerificationId,
            integrationEvent.RejectedAt,
            _clock.UtcNow,
            expiresAt: null,
            integrationEvent.ReasonCode,
            cancellationToken);

        if (applied)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
