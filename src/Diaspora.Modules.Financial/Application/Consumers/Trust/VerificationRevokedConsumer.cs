using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Consumers.Trust;

/// <summary>
/// Restricts a financial identity when Trust withdraws a verification.
/// <para>
/// <b>Restricted, not closed, and the difference is the point.</b> A restricted account keeps every
/// balance, still accepts refunds, and cannot spend. Closing it would strand money belonging to
/// somebody whose verification may well be reinstated next week.
/// </para>
/// </summary>
public sealed class VerificationRevokedConsumer
    : IntegrationEventHandler<VerificationRevokedIntegrationEvent>
{
    private readonly IFinancialAccountRepository _accounts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public VerificationRevokedConsumer(
        IFinancialAccountRepository accounts, IUnitOfWork unitOfWork, IClock clock)
    {
        _accounts = accounts;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var account = await _accounts.FindByOwnerAsync(
            integrationEvent.UserId, FinancialAccountType.Personal, cancellationToken);

        if (account is null || account.Status == FinancialAccountStatus.Restricted)
        {
            return;
        }

        account.Restrict(
            integrationEvent.Reason ?? "A verification was withdrawn.", _clock.UtcNow);

        _accounts.Update(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
