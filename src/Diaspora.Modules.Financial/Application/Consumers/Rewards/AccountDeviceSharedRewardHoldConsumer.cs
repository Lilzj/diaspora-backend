using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Financial.Application.Rewards;

namespace Diaspora.Modules.Financial.Application.Consumers.Rewards;

/// <summary>
/// Holds a member's points for review when Identity says their new device is one other accounts
/// already use (D-13, CS-D5).
/// <para>
/// <b>A system hold, decided by nobody.</b> Points still accrue and vest; nothing converts, and no
/// converted credit is released, until staff lift it from the review queue
/// (<c>GET /operations/rewards/holds</c>). The note is an operator's and never reaches the member.
/// </para>
/// <para>
/// <b>A hold already in force is left alone.</b> A second announcement, a redelivery, or a hold staff
/// placed for their own reason keeps the note it has: the platform never writes over a person. Both
/// rules are <see cref="SystemRewardHolds"/>', which every system hold goes through.
/// </para>
/// </summary>
public sealed class AccountDeviceSharedRewardHoldConsumer
    : IntegrationEventHandler<AccountDeviceSharedIntegrationEvent>
{
    public const string Reason = "Shares a device with another account; awaiting review";

    private readonly SystemRewardHolds _holds;

    public AccountDeviceSharedRewardHoldConsumer(SystemRewardHolds holds) => _holds = holds;

    protected override Task HandleAsync(
        AccountDeviceSharedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _holds.PlaceAsync(integrationEvent.UserId, Reason, cancellationToken);
    }
}
