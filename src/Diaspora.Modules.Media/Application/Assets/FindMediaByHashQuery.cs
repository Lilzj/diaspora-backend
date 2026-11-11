using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Finds every asset whose bytes have a given SHA-256 digest.
/// <para>
/// The server-side answer to the client's <c>TrustEngine.detectImageReuse</c>, which
/// fingerprints by URL string and is defeated by any re-upload. Media answers the question and
/// forms no opinion: whether three re-uses constitutes fraud is Trust's judgement, and putting
/// that policy here would be a fraud rule living in a storage module.
/// </para>
/// </summary>
[RequirePolicy(MediaPolicies.Read)]
public sealed record FindMediaByHashQuery : IQuery<IReadOnlyList<MediaAssetResponse>>
{
    public required string ContentHash { get; init; }
}
