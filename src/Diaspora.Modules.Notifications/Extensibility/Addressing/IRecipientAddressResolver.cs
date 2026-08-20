using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Addressing;

/// <summary>
/// Where a member can be reached on a channel that has no registration behind it.
/// <para>
/// <b>Email is the case that forced this port to exist, and the answer it gives today is
/// "nowhere".</b> A member's email address belongs to Identity. Copying it into this module would
/// duplicate ownership of the platform's most sensitive contact detail, and would leave two records
/// to keep in step every time somebody changes it. Neither <c>UserRegisteredIntegrationEvent</c> nor
/// any other published contract carries it, which is a deliberate decision by B2 rather than an
/// omission.
/// </para>
/// <para>
/// So the shipped implementation resolves nothing and the email channel honestly reports
/// <c>NoEndpoint</c>. That keeps the entire path above it — preferences, quiet hours, digests,
/// retries, analytics — exercised and correct, and makes acquiring an email provider a two-part
/// change: a sender, and an adapter here that Identity is willing to answer. Inventing a copy of the
/// address now would make the second half of that permanent.
/// </para>
/// </summary>
public interface IRecipientAddressResolver
{
    Task<IReadOnlyList<ChannelEndpoint>> ResolveAsync(
        UserId userId,
        NotificationChannelKind channel,
        CancellationToken cancellationToken = default);
}
