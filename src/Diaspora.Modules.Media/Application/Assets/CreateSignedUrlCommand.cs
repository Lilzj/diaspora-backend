using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Requests;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Assets;

/// <summary>
/// Mints a short-lived link to a private asset.
/// <para>
/// Audited, because for a verification document the question "who obtained a link to this, and
/// when" is one a data-protection request will eventually ask.
/// </para>
/// </summary>
[Auditable]
[RequirePolicy(MediaPolicies.SignedAccess)]
public sealed record CreateSignedUrlCommand : ICommand<SignedUrlResponse>
{
    public required Guid MediaId { get; init; }

    public required CreateSignedUrlRequest Request { get; init; }
}
