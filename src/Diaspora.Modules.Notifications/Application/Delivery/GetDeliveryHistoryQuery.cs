using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Delivery;

/// <summary>
/// What every channel did with one message, and every attempt it took.
/// <para>
/// Staff-facing, and it returns no words. A delivery record answers "did it go", which is a question
/// an operator may ask about anybody; the message itself is only ever the recipient's.
/// </para>
/// </summary>
[Authorize(Permission = NotificationPermissions.DeliveryRead)]
public sealed record GetDeliveryHistoryQuery : IQuery<NotificationDetailResponse>
{
    public required Guid NotificationId { get; init; }
}
