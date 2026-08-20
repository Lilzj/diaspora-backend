namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// Everything a member can change about how they are contacted.
/// <para>
/// <b>The brief asked for "notification preferences" and "communication preferences" as separate
/// things; they are one settings screen and one record.</b> The distinction is kept where it belongs
/// — in the response's shape — with the channel and category switches at the top and the
/// communication settings grouped under <see cref="Communication"/>. A client that wants only one
/// half reads only one half; nothing has to be saved twice or can be saved inconsistently.
/// </para>
/// <para>
/// <c>push</c> and <c>email</c> are flat booleans at the top level because that is exactly what the
/// existing settings screen stores in <c>AsyncStorage</c> under those names. The screen can move to
/// the server without changing a single key.
/// </para>
/// </summary>
public sealed record NotificationPreferencesResponse
{
    /// <summary>The client's existing key. Always true — the inbox cannot be switched off.</summary>
    public required bool InApp { get; init; }

    /// <summary>The client's existing key.</summary>
    public required bool Push { get; init; }

    /// <summary>The client's existing key.</summary>
    public required bool Email { get; init; }

    public required IReadOnlyList<CategoryPreferenceResponse> Categories { get; init; }

    public required CommunicationPreferencesResponse Communication { get; init; }
}
