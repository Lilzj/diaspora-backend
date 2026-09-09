using Diaspora.Domain.Identity.Repositories;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Infrastructure.Services;

/// <summary>
/// Answers <see cref="IMemberProfileService"/> from the identity store.
/// <para>
/// <b>This is the implementation B13 was waiting for.</b> Its
/// <c>IConversationParticipantDirectory</c> shipped with an identifier-only default and nothing
/// ever replaced it, so every conversation on the platform came back with a counterparty carrying
/// no name and the client rendered every thread as "Unknown".
/// </para>
/// <para>
/// It lives here because identity's users live here. A module reaching down for them would invert
/// the layering; the kernel port is the sanctioned direction, and it is the same one
/// <c>AccountStandingPolicies</c> takes to <see cref="ITrustService"/>.
/// </para>
/// </summary>
public sealed class MemberProfileService : IMemberProfileService
{
    private readonly IUserRepository _users;

    public MemberProfileService(IUserRepository users)
    {
        _users = users;
    }

    public bool IsAvailable => true;

    public async Task<IReadOnlyDictionary<Guid, MemberProfile>> ResolveAsync(
        IReadOnlyCollection<UserId> userIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        // One round trip. A page of twenty conversations must not be twenty queries.
        var users = await _users.GetByIdsAsync(userIds, cancellationToken);

        // Identifiers with no member are simply absent. "This is not a person" is a fact a caller
        // needs — it is what lets a conversation addressed to something that is not one be
        // refused — and a row saying "Unknown" would hide it.
        return users.ToDictionary(
            user => user.Id.Value,
            user => new MemberProfile
            {
                UserId = user.Id.Value,
                DisplayName = user.FullName.Value
            });
    }
}
