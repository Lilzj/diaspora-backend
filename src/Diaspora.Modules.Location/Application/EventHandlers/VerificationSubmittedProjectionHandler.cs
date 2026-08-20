using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Location.Application.EventHandlers;

/// <summary>
/// Records that a residency claim is with the Trust Engine.
/// <para>
/// Worth projecting rather than waiting for the decision, because "under review" is a state
/// the member's own timeline should show. A country that reads as plainly unverified while
/// its evidence is being reviewed invites the member to submit again — which is exactly the
/// duplicate Trust then has to reject.
/// </para>
/// </summary>
public sealed class VerificationSubmittedProjectionHandler
    : IntegrationEventHandler<VerificationSubmittedIntegrationEvent>
{
    private const string CountryVerificationType = "Country";

    private readonly ResidencyProjector _projector;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VerificationSubmittedProjectionHandler(
        ResidencyProjector projector, IUnitOfWork unitOfWork, IClock clock)
    {
        _projector = projector;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        VerificationSubmittedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
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
            ResidencyVerificationState.UnderReview,
            integrationEvent.VerificationId,
            integrationEvent.SubmittedAt,
            _clock.UtcNow,
            expiresAt: null,
            reason: null,
            cancellationToken);

        if (applied)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
