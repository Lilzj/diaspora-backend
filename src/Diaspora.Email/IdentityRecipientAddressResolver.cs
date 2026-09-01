using Diaspora.Domain.Identity.Repositories;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Addressing;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Email;

/// <summary>
/// Answers "where can this member be reached by email?" by asking Identity.
/// </summary>
/// <remarks>
/// <para>
/// <b>The second half of acquiring an email provider.</b> Notifications documents this exactly:
/// a sender, and an adapter here that Identity is willing to answer. Without it the transport was
/// configured, the templates rendered, the preferences and quiet hours were evaluated, the message
/// was composed — and the channel reported <c>NoEndpoint</c> because nothing knew the address.
/// Every notification email failed for that reason and no other.
/// </para>
/// <para>
/// <b>Why it lives in the adapter and not in Notifications.</b> A member's email address belongs
/// to Identity, and no published contract carries it — deliberately, so the platform's most
/// sensitive contact detail has one owner rather than a copy in every module that wants to write
/// to it. Notifications therefore declares a port and ships a null implementation. This assembly
/// is already the place where "this deployment can send email" is expressed, so it is the honest
/// place for "and this is how it finds the address". Nothing is copied and nothing is cached: the
/// address is read at send time, so a member who corrects it is reached at the corrected one.
/// </para>
/// <para>
/// <b>Email only.</b> Asked about any other channel it answers nothing, because it knows nothing
/// about them. A push token is a device registration and belongs to the registry that owns
/// devices; answering with an email address there would be worse than answering with nothing.
/// </para>
/// </remarks>
public sealed class IdentityRecipientAddressResolver : IRecipientAddressResolver
{
    private static readonly IReadOnlyList<ChannelEndpoint> None = [];

    private readonly IUserRepository _users;

    public IdentityRecipientAddressResolver(IUserRepository users)
    {
        ArgumentNullException.ThrowIfNull(users);

        _users = users;
    }

    public async Task<IReadOnlyList<ChannelEndpoint>> ResolveAsync(
        UserId userId,
        NotificationChannelKind channel,
        CancellationToken cancellationToken = default)
    {
        if (channel is not NotificationChannelKind.Email)
        {
            return None;
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);

        // No account, or an account with no address on it. Both are ordinary: an account created
        // before email became the sign-in credential may still carry none, and the channel's
        // `NoEndpoint` is the truthful outcome rather than a failure to paper over.
        if (user?.Email is not { } email || string.IsNullOrWhiteSpace(email.Value))
        {
            return None;
        }

        // No `EndpointId`. That identifier means "a registration this module can retire", and an
        // address read from Identity is not one — retiring it would mean this module deleting a
        // member's email address because a provider bounced a message once.
        return [new ChannelEndpoint { Address = email.Value }];
    }
}
