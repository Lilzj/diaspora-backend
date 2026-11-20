using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Media.Integration;

/// <summary>
/// The operations other modules perform on B5.
/// <para>
/// <b>B5 published the payload and never published the operation.</b> Every vertical that owns
/// uploads already built a frozen <c>AttachMediaRequest</c> — B5's own V1 contract — and then
/// wrapped it in <c>AttachMediaCommand</c>, which is B5's <em>application</em> layer, and
/// dispatched that. Seven modules did it identically, each in a single <c>*MediaClaimer</c>
/// class, and each of their doc comments says the same thing: that the module consumes B5's
/// frozen contract and edits nothing in B5. They were right about the intent. The envelope was
/// the leak. BARCH-001 / BSOLID-003.
/// </para>
/// <para>
/// Everything named here is published: the request and response types are B5's <c>Contracts.V1</c>,
/// and the owner type and role vocabulary are <c>Diaspora.Contracts.Media.V1</c>. A consumer
/// needs no part of B5's Domain or Application to claim an upload.
/// </para>
/// <para>
/// <b>The pipeline is unchanged.</b> The implementation dispatches the same commands through the
/// same sender, so authorisation, validation, auditing and the transaction behave exactly as
/// they did when each vertical dispatched them itself — this moves who constructs the envelope,
/// not what happens to it.
/// </para>
/// </summary>
public interface IMediaAttachments
{
    /// <summary>
    /// Claims an uploaded asset for an owning entity.
    /// <para>
    /// The call every vertical makes after saving its own record. <b>Not optional:</b> B5 sweeps
    /// assets nothing ever attached, so an unclaimed upload is an image that disappears a day
    /// later, silently, because the upload itself succeeded.
    /// </para>
    /// </summary>
    Task<Result<MediaAssetResponse>> AttachAsync(
        AttachMediaRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims an upload as the caller's avatar or cover and makes it publicly addressable.
    /// </summary>
    /// <param name="role">
    /// <c>MediaReferenceRoles.Avatar</c> or <c>MediaReferenceRoles.Cover</c>; any other value is
    /// refused by B5 rather than by the caller.
    /// </param>
    Task<Result<ProfileImageResponse>> PublishProfileImageAsync(
        Guid mediaId, string role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a profile image private again once the member no longer shows it. A no-op rather
    /// than a failure when the asset is gone or was never theirs.
    /// </summary>
    Task<Result> WithdrawProfileImageAsync(
        Guid mediaId, string role, CancellationToken cancellationToken = default);
}
