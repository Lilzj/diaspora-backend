using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Domain.Presence;

public interface IMemberPresenceRepository
{
    Task<MemberPresence?> FindAsync(UserId userId, CancellationToken cancellationToken = default);

    void Add(MemberPresence presence);
}
