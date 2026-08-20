using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Models;

/// <summary>
/// Everything that goes into an access token.
/// <para>
/// Permissions and feature gates are embedded as a snapshot so ordinary requests
/// authorise without a database round trip. The trade-off is staleness: a permission
/// revoked mid-token stays effective until expiry, which is why access tokens are
/// short-lived and why <see cref="SecurityStamp"/> exists as an immediate kill switch.
/// </para>
/// </summary>
public sealed record AccessTokenRequest
{
    public required UserId UserId { get; init; }

    public required SessionId SessionId { get; init; }

    public DeviceId? DeviceId { get; init; }

    public required UserRole Role { get; init; }

    public required AccessState AccessState { get; init; }

    /// <summary>Bumped whenever every existing token must stop working.</summary>
    public required int SecurityStamp { get; init; }

    public required IReadOnlyList<string> Permissions { get; init; }

    /// <summary>Optional; omitted when the feature list would bloat the token.</summary>
    public IReadOnlyList<string>? Features { get; init; }

    public string? CorrelationId { get; init; }
}
