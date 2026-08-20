namespace Diaspora.Modules.Integrations.Extensibility.Providers;

/// <summary>
/// Verifies that a callback came from the provider it claims to.
/// <para>
/// <b>Registered per provider, and the absence of one is a refusal rather than a pass.</b> A
/// callback for a provider with no verifier is answered 404 and no row is written — because a table
/// an unauthenticated caller can write to is a table an unauthenticated caller can fill, and
/// "nobody registered a verifier" must never become "no verification was needed".
/// </para>
/// <para>
/// <b>The raw bytes are what is verified.</b> Model binding deserialises and re-serialises, and a
/// signature verified over a re-serialised object is a signature verified over something the
/// provider never sent — an integration that works until a property order changes. The route reads
/// the body once as text, caps it, and hands it here unaltered.
/// </para>
/// </summary>
public interface IIntegrationWebhookVerifier
{
    /// <summary>The provider whose callbacks this verifies. Matched against the route segment.</summary>
    string Provider { get; }

    /// <summary>
    /// Whether the deployment has the secret this verifier needs.
    /// <para>
    /// A verifier registered without its secret must report false, and the route must refuse — the
    /// alternative is a verifier that cannot verify and therefore accepts, which is worse than no
    /// verifier at all because it looks like one.
    /// </para>
    /// </summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies and interprets one callback.
    /// <para>
    /// Verification and interpretation are one call rather than two, because the second must never
    /// happen without the first and a two-call interface is a two-call interface somebody eventually
    /// uses half of.
    /// </para>
    /// </summary>
    Task<IntegrationWebhookVerification> VerifyAsync(
        IntegrationWebhookEnvelope envelope, CancellationToken cancellationToken = default);
}

/// <summary>Exactly what arrived.</summary>
/// <param name="Provider">The route segment, normalised.</param>
/// <param name="RawBody">The bytes as text, unaltered. See the interface summary.</param>
/// <param name="Headers">
/// All of them, flattened. Which header carries the signature is the provider's decision and
/// therefore the adapter's knowledge. A repeated header is joined rather than dropped — a signature
/// split across two values would otherwise verify against half of itself.
/// </param>
/// <param name="ReceivedAt">The platform's clock, for the replay window.</param>
public sealed record IntegrationWebhookEnvelope(
    string Provider,
    string RawBody,
    IReadOnlyDictionary<string, string> Headers,
    DateTimeOffset ReceivedAt);

/// <summary>What a verifier concluded.</summary>
public sealed record IntegrationWebhookVerification
{
    /// <summary>
    /// Whether the signature checked out.
    /// <para>
    /// False is loud: the route answers 400 and the counter is alerted on, because a signature that
    /// does not verify is either a rotated secret nobody updated or somebody probing.
    /// </para>
    /// </summary>
    public required bool IsVerified { get; init; }

    /// <summary>
    /// The provider's own event identifier.
    /// <para>
    /// The replay key. Null is accepted and the platform hashes the body instead, which is weaker
    /// and better than nothing.
    /// </para>
    /// </summary>
    public string? EventId { get; init; }

    /// <summary>
    /// What happened, in this platform's vocabulary.
    /// <para>
    /// The adapter maps the provider's word into one of
    /// <see cref="IntegrationWebhookEventKinds"/>. An unrecognised kind is recorded and ignored
    /// rather than acted on — §14: "do not process arbitrary webhook payloads merely because they
    /// contain a familiar event name".
    /// </para>
    /// </summary>
    public string? EventKind { get; init; }

    /// <summary>
    /// Which account or resource the callback is about. The join key onto a connection.
    /// </summary>
    public string? ExternalReference { get; init; }

    /// <summary>
    /// Whether the payload replays one already outside the acceptable window.
    /// <para>
    /// Separate from <see cref="IsVerified"/> because a correctly-signed replay is a different
    /// finding from a bad signature: the first means somebody captured a real callback, and it is
    /// the one worth telling an operator about.
    /// </para>
    /// </summary>
    public bool IsStale { get; init; }

    public static IntegrationWebhookVerification Rejected() => new() { IsVerified = false };

    public static IntegrationWebhookVerification Stale() =>
        new() { IsVerified = true, IsStale = true };
}

/// <summary>
/// The callback kinds this module acts on.
/// <para>
/// A closed set, deliberately small. Everything a provider can say that is not on this list is
/// recorded as <see cref="Diaspora.Modules.Integrations.Domain.Enums.IntegrationWebhookOutcome.Ignored"/>
/// — visible, and inert.
/// </para>
/// </summary>
public static class IntegrationWebhookEventKinds
{
    /// <summary>
    /// The member removed Diaspora from their provider account.
    /// <para>
    /// The one the register cares most about: it is what stops the centre "showing
    /// <c>CONNECTED</c> for a grant removed at Google a week ago".
    /// </para>
    /// </summary>
    public const string GrantRevoked = "grant.revoked";

    /// <summary>The provider says the grant no longer covers what it used to.</summary>
    public const string GrantChanged = "grant.changed";

    /// <summary>Something changed in a watched calendar. A prompt to sync, never content.</summary>
    public const string CalendarChanged = "calendar.changed";

    /// <summary>A watch channel is about to lapse and needs renewing.</summary>
    public const string WatchExpiring = "watch.expiring";

    public static bool IsKnown(string? kind) => kind switch
    {
        GrantRevoked or GrantChanged or CalendarChanged or WatchExpiring => true,
        _ => false
    };
}
