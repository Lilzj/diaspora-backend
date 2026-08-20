using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Application.Consumers.Trust;

/// <summary>
/// Activates a member's financial identity when Trust approves them.
/// <para>
/// <b>This is the whole of the Identity and Trust integration, and it is one direction only.</b> The
/// Financial Platform never asks Trust a question; it is told, through a published contract, and
/// acts. Verification is Trust's word to give — B15 references neither module and could not query
/// one if it wanted to.
/// </para>
/// <para>
/// <b>A partial approval does not activate anything.</b> <c>Partial</c> means Trust approved some of
/// what was submitted; money is the surface where "some" is not enough.
/// </para>
/// </summary>
public sealed class VerificationApprovedConsumer
    : IntegrationEventHandler<VerificationApprovedIntegrationEvent>
{
    private readonly IFinancialAccountRepository _accounts;
    private readonly FinancialIdentityService _identities;
    private readonly IUnitOfWork _unitOfWork;

    public VerificationApprovedConsumer(
        IFinancialAccountRepository accounts,
        FinancialIdentityService identities,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _identities = identities;
        _unitOfWork = unitOfWork;
    }

    protected override async Task HandleAsync(
        VerificationApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.Partial)
        {
            return;
        }

        var account = await _accounts.FindByOwnerAsync(
            integrationEvent.UserId, FinancialAccountType.Personal, cancellationToken)
            ?? await _identities.GetOrCreateAsync(
                integrationEvent.UserId,
                FinancialAccountType.Personal,
                countryCode: integrationEvent.TargetCountry,
                cancellationToken: cancellationToken);

        if (account.Status == FinancialAccountStatus.Active)
        {
            return;
        }

        await _identities.ActivateAsync(account, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
