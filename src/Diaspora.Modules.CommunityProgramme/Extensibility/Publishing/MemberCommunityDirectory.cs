using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Extensibility.Publishing;

/// <summary>
/// The communities a member belongs to, for Community Life's "events from my communities"
/// (4 Nov 2026). A current membership only; see <see cref="IMemberCommunityDirectory"/>.
/// </summary>
public sealed class MemberCommunityDirectory : IMemberCommunityDirectory
{
    private readonly ICommunityMembershipRepository _memberships;

    public MemberCommunityDirectory(ICommunityMembershipRepository memberships) => _memberships = memberships;

    public async Task<IReadOnlyList<Guid>> CommunitiesOfAsync(
        Guid memberId, CancellationToken cancellationToken = default)
    {
        if (memberId == Guid.Empty)
        {
            return [];
        }

        var memberships = await _memberships.FindForMemberAsync(UserId.From(memberId), null, cancellationToken);

        return [.. memberships.Where(pair => pair.Value.IsCurrentMember).Select(pair => pair.Key.Value)];
    }
}
