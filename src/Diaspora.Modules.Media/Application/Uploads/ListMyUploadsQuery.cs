using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Uploads;

/// <summary>
/// The caller's open uploads.
/// <para>
/// The answer to "what was I uploading?" after a reinstall — which the client cannot answer
/// for itself, because the local <c>file://</c> URIs its composers saved to
/// <c>AsyncStorage</c> no longer resolve on a fresh install.
/// </para>
/// </summary>
[Authorize(Policy = MediaPolicies.Read)]
public sealed record ListMyUploadsQuery : IQuery<IReadOnlyList<UploadSessionResponse>>;
