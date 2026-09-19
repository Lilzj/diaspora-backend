using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Location.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.Consumers;

/// <summary>
/// Closes a country standing when the member says they have left.
/// <para>
/// <b>The other half of a flow that was built twice and never joined.</b> Location raised a
/// departure event whose own summary said Trust needed to hear it; Trust had
/// <c>CountryStanding.RecordDeparture</c> waiting to act on it. Neither side had a caller, so
/// relocation did nothing: a member who moved from Mauritius to the UK stayed an open, verified
/// resident of Mauritius, with the residence clock still running on a country they had left.
/// </para>
/// <para>
/// <b>Why a declared fact is allowed to close a proven one.</b> Nothing is taken away here. The
/// verification, the badge and every point earned survive a departure untouched — the only
/// consequence is that <c>YearsOfResidence</c> stops at the departure date rather than today,
/// which is the honest reading of a member who says they no longer live somewhere. The reverse
/// direction is not symmetric and must never be: a declaration cannot grant standing, only
/// <see cref="Diaspora.Modules.Trust.Domain.TrustProfile.RecordGrant"/> can.
/// </para>
/// <para>
/// A member with no trust profile, or with no standing in that country, is not an error and
/// creates nothing. Delivery is at-least-once, so the write is idempotent: a second copy of the
/// same departure finds the date already set and saves nothing.
/// </para>
/// </summary>
public sealed class CountryResidenceEndedConsumer
    : IntegrationEventHandler<CountryResidenceEndedIntegrationEvent>
{
    private readonly ITrustProfileRepository _profiles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<CountryResidenceEndedConsumer> _logger;

    public CountryResidenceEndedConsumer(
        ITrustProfileRepository profiles,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<CountryResidenceEndedConsumer> logger)
    {
        _profiles = profiles;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        CountryResidenceEndedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var profile = await _profiles.GetByUserAsync(
            UserId.From(integrationEvent.UserId), cancellationToken);

        if (profile is null)
        {
            return;
        }

        var closed = profile.RecordDeparture(
            integrationEvent.CountryCode, integrationEvent.DepartedOn, _clock.UtcNow);

        if (!closed)
        {
            return;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Standing in {Country} closed for {UserId}: departed {DepartedOn}.",
            integrationEvent.CountryCode,
            integrationEvent.UserId,
            integrationEvent.DepartedOn);
    }
}
