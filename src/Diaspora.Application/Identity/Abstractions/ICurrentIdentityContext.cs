using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// The identity-specific slice of the current request's caller.
/// <para>
/// The platform kernel's <c>ICurrentUser</c> stops at the user id and role. Identity also
/// needs the session and device the request arrived on — for "sign out this device",
/// "revoke every other session", and for marking which row in the session list is the one
/// you are reading it from. Exposing them as typed properties keeps handlers out of raw
/// claim dictionaries.
/// </para>
/// </summary>
public interface ICurrentIdentityContext
{
    bool IsAuthenticated { get; }

    UserId? UserId { get; }

    SessionId? SessionId { get; }

    DeviceId? DeviceId { get; }

    /// <summary>The access state asserted by the presented token, if any.</summary>
    string? AccessState { get; }
}
