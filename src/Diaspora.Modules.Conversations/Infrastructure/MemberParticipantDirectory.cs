using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Conversations.Infrastructure;

/// <summary>
/// Names the people in a conversation, from the kernel's member port.
/// <para>
/// <b>B13 shipped the seam and nobody ever filled it.</b>
/// <see cref="IdentifierOnlyParticipantDirectory"/> is registered by this module as a default that
/// answers with identifiers and no names, and no module replaced it — so every conversation on the
/// platform came back with a counterparty carrying a null <c>displayName</c>, and the client, which
/// reads that field, rendered every thread as <em>"Unknown"</em> above a context card naming the
/// subject. Not organization threads. Every thread, including a member messaging a seller about a
/// listing they were about to buy.
/// </para>
/// <para>
/// <b>Why this lives here and reads a port.</b> The same reasoning written out on
/// <c>AccountStandingPolicies</c>: identity's users sit below the modules, so Conversations reads
/// <see cref="IMemberProfileService"/> and gains no project reference to anything. Identity states
/// who somebody is; this module decides what a conversation does with that.
/// </para>
/// <para>
/// <b><see cref="IsAvailable"/> follows the port.</b> It is not a boast about this class existing:
/// a caller distinguishing "not a member" from "cannot tell" needs the answer to be about the
/// source, and a deployment whose member service is the placeholder is still one that cannot tell.
/// </para>
/// </summary>
public sealed class MemberParticipantDirectory : IConversationParticipantDirectory
{
    private readonly IMemberProfileService _members;

    public MemberParticipantDirectory(IMemberProfileService members)
    {
        _members = members;
    }

    public bool IsAvailable => _members.IsAvailable;

    public async Task<IReadOnlyDictionary<Guid, ConversationParticipantProfile>> ResolveAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var members = await _members.ResolveAsync(userIds, cancellationToken);

        // Only what the port knows. The trust fields on the profile belong to Trust and stay unset
        // rather than being defaulted here — a participant shown as unverified because nobody
        // asked is a different thing from one shown as unverified because they are.
        return members.ToDictionary(
            entry => entry.Key,
            entry => new ConversationParticipantProfile
            {
                UserId = entry.Key,
                DisplayName = entry.Value.DisplayName,
                AvatarUrl = entry.Value.AvatarUrl
            });
    }
}
