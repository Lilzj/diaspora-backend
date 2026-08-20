using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Extensibility.Addressing;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Infrastructure.Addressing;

/// <summary>
/// Resolves no addresses, deliberately.
/// <para>
/// <b>The honest implementation of a question this module must not answer yet.</b> A member's email
/// address belongs to Identity; no published contract carries it, which is B2's decision and a good
/// one. Copying it here to make the email channel look finished would duplicate ownership of the
/// platform's most sensitive contact detail and leave two records to keep in step forever.
/// </para>
/// <para>
/// So the email channel reports <c>NoEndpoint</c>, which is true, and every part of the module above
/// the transport is exercised and correct. An email provider arrives as a sender plus a replacement
/// for this — an adapter Identity is willing to answer — and nothing else changes.
/// </para>
/// </summary>
public sealed class NullRecipientAddressResolver : IRecipientAddressResolver
{
    private static readonly IReadOnlyList<ChannelEndpoint> None = [];

    public Task<IReadOnlyList<ChannelEndpoint>> ResolveAsync(
        UserId userId,
        NotificationChannelKind channel,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(None);
}
