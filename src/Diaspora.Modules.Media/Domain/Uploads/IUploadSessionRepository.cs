using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Media.Domain.Uploads;

/// <summary>The upload session's persistence boundary.</summary>
public interface IUploadSessionRepository
{
    /// <summary>
    /// One session with every received part.
    /// <para>
    /// Always with its parts, because every operation on a session reads them: receiving a
    /// part checks for a duplicate, resuming reports which are missing, and completing
    /// reassembles them in order.
    /// </para>
    /// </summary>
    Task<UploadSession?> GetAsync(
        UploadSessionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// A member's open sessions, newest first.
    /// <para>
    /// Two jobs. It is the answer to "what was I uploading" after a reinstall — which the
    /// client cannot answer from <c>AsyncStorage</c>, because the local <c>file://</c> URIs
    /// it saved no longer resolve. And it is the concurrent-upload quota check.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UploadSession>> ListOpenForUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>How many uploads a member currently has open. The quota check.</summary>
    Task<int> CountOpenForUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Sessions past their window. Swept along with their staged parts.</summary>
    Task<IReadOnlyList<UploadSession>> FindExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(UploadSession session, CancellationToken cancellationToken = default);

    void Remove(UploadSession session);
}
