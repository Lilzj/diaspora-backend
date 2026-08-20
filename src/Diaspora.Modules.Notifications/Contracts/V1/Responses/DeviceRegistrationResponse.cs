namespace Diaspora.Modules.Notifications.Contracts.V1.Responses;

/// <summary>
/// One delivery endpoint.
/// <para>
/// The push token is never echoed back — <c>hasToken</c> says only that one exists, exactly as
/// Identity's own device response carries <c>hasPushToken</c>. A token is a credential for somebody
/// else's service and returning it would put it in every client's log.
/// </para>
/// </summary>
public sealed record DeviceRegistrationResponse
{
    public required Guid Id { get; init; }

    /// <summary><c>Android</c>, <c>IOS</c>, <c>Web</c> or <c>Tablet</c>.</summary>
    public required string Platform { get; init; }

    /// <summary>The channel this endpoint serves. <c>Push</c> today.</summary>
    public required string Channel { get; init; }

    public string? FriendlyName { get; init; }

    public string? DeviceIdentifier { get; init; }

    public string? AppVersion { get; init; }

    public required bool HasToken { get; init; }

    /// <summary>Whether the dispatcher will route to it.</summary>
    public required bool IsActive { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    public required DateTimeOffset LastActiveAt { get; init; }

    public DateTimeOffset? LastDeliveredAt { get; init; }

    /// <summary>Why it stopped being used. Null while it is healthy.</summary>
    public string? SuppressionReason { get; init; }

    /// <summary>
    /// Whether this is the endpoint the request came from — B13.3.
    /// <para>
    /// <b>Derived from the presented token, not from anything the client sent in the request.</b>
    /// The client used to decide this by matching an identifier it generated itself, which worked and
    /// was a guess: a reinstall regenerated it and the app stopped recognising its own row. The
    /// server compares the session's device claim against the one recorded when the endpoint was
    /// registered, so the answer is an assertion.
    /// </para>
    /// <para>
    /// <b>False also means "cannot tell".</b> An endpoint registered before B13.3, or one registered
    /// on a token that named no device, has nothing to compare — and the safe direction matters here,
    /// because this flag is what "sign out my other devices" spares.
    /// </para>
    /// </summary>
    public required bool IsCurrentDevice { get; init; }
}
