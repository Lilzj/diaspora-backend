using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Application.Consumers.Identity;

/// <summary>
/// Opens a financial identity for a new member.
/// <para>
/// <b>Opened <c>Pending</c>, which is the interesting part.</b> A pending account holds wallets that
/// accept credit and refuse every debit — so a welcome bonus or a referral reward can be granted to
/// somebody the day they register, and none of it can leave until Trust says who they are. Waiting
/// for verification to create the account instead would mean a grant during onboarding had nowhere
/// to land.
/// </para>
/// <para>
/// Idempotent: a redelivered registration finds the account and returns it.
/// </para>
/// </summary>
public sealed class UserRegisteredConsumer : IntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly FinancialIdentityService _identities;
    private readonly IUnitOfWork _unitOfWork;

    public UserRegisteredConsumer(FinancialIdentityService identities, IUnitOfWork unitOfWork)
    {
        _identities = identities;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _identities.GetOrCreateAsync(
            integrationEvent.UserId,
            FinancialAccountType.Personal,
            currency: null,
            displayName: integrationEvent.FullName,
            countryCode: integrationEvent.CountryOfResidence,
            cancellationToken: cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
