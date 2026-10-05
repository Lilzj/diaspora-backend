using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Integrations.Domain.Errors;

/// <summary>
/// The refusals this module returns.
/// <para>
/// One place, so a code and the sentence beside it cannot drift, and so that the mapping from a
/// provider failure to a platform error exists exactly once — <see cref="FromProvider"/> below. Six
/// handlers each mapping <c>RateLimited</c> to whatever felt right is how one surface comes to answer
/// 503 for a quota and another 500.
/// </para>
/// </summary>
public static class IntegrationErrors
{
    public static Error NotAuthenticated() => Error.Unauthorized(
        IntegrationsErrorCodes.NotAuthenticated,
        "You need to be signed in to manage integrations.");

    public static Error ProviderUnknown(string provider) => Error.Validation(
        IntegrationsErrorCodes.ProviderUnknown,
        $"'{provider}' is not a provider this platform knows about.");

    public static Error ProviderNotConfigured(string capability) => Error.Validation(
        IntegrationsErrorCodes.ProviderNotConfigured,
        $"This deployment has no provider configured for {capability}.");

    public static Error CapabilityUnsupported(string capability) => Error.Validation(
        IntegrationsErrorCodes.CapabilityUnsupported,
        $"The configured provider does not support {capability}.");

    public static Error ConnectionNotFound(string provider) => Error.NotFound(
        IntegrationsErrorCodes.ConnectionNotFound,
        $"You have no connection to {provider}.");

    public static Error ConnectionNotUsable(string provider) => Error.Validation(
        IntegrationsErrorCodes.ConnectionNotUsable,
        $"Your {provider} connection is not currently usable.");

    public static Error ReauthorizationRequired(string provider) => Error.Unauthorized(
        IntegrationsErrorCodes.ReauthorizationRequired,
        $"Your {provider} authorization has ended. Connect it again to carry on.");

    /// <summary>
    /// The state value matched nothing.
    /// <para>
    /// Deliberately the same sentence as <see cref="AuthorizationStateExpired"/> would give a member,
    /// and a different code. The codes differ so telemetry can tell "somebody is posting callbacks we
    /// never issued" from "somebody left the consent screen open too long"; the sentences do not,
    /// because a caller who does not hold a valid state should not learn which of the two they hit.
    /// </para>
    /// </summary>
    public static Error AuthorizationStateUnknown() => Error.Validation(
        IntegrationsErrorCodes.AuthorizationStateUnknown,
        "That authorization could not be completed. Start again from the connection screen.");

    public static Error AuthorizationStateExpired() => Error.Validation(
        IntegrationsErrorCodes.AuthorizationStateExpired,
        "That authorization could not be completed. Start again from the connection screen.");

    /// <summary>
    /// A code that has already been redeemed.
    /// <para>
    /// A 409 rather than a 400, because it is the register's requirement: "A replayed code MUST
    /// return 409, not a second connection." The status is what tells a client that the first
    /// attempt worked.
    /// </para>
    /// </summary>
    public static Error AuthorizationCodeReplayed() => Error.Conflict(
        IntegrationsErrorCodes.AuthorizationCodeReplayed,
        "That authorization has already been completed.");

    public static Error RedirectUriNotAllowed() => Error.Validation(
        IntegrationsErrorCodes.RedirectUriNotAllowed,
        "That return address is not one this deployment accepts.");

    public static Error IntentUnsupported(string intent) => Error.Validation(
        IntegrationsErrorCodes.IntentUnsupported,
        $"This deployment does not support '{intent}' authorization.");

    /// <summary>
    /// The external account belongs to somebody else.
    /// <para>
    /// "The 409 is a security control, not a niceness — without it, linking is an account merge
    /// anybody can trigger." The sentence names no other member, because who holds the account is
    /// not information the caller is entitled to.
    /// </para>
    /// </summary>
    public static Error ExternalAccountAlreadyLinked() => Error.Conflict(
        IntegrationsErrorCodes.ExternalAccountAlreadyLinked,
        "That account is already connected to a different Diaspora account.");

    public static Error TimeZoneRequired() => Error.Validation(
        IntegrationsErrorCodes.TimeZoneRequired,
        "A calendar entry needs an IANA time zone. Without one the entry would claim the wrong "
        + "moment for anybody in a different zone.");

    public static Error TimeZoneUnknown(string timeZone) => Error.Validation(
        IntegrationsErrorCodes.TimeZoneUnknown,
        $"'{timeZone}' is not a time zone this platform recognises.");

    public static Error CalendarNotFound(string calendarId) => Error.NotFound(
        IntegrationsErrorCodes.CalendarNotFound,
        "That calendar is not one your connected account can reach.");

    public static Error CalendarReadOnly() => Error.Forbidden(
        IntegrationsErrorCodes.CalendarReadOnly,
        "That calendar does not allow changes.");

    public static Error CalendarEventNotFound() => Error.NotFound(
        IntegrationsErrorCodes.CalendarEventNotFound,
        "There is no entry recorded for that event in that calendar.");

    public static Error SyncDirectionUnsupported(string direction) => Error.Validation(
        IntegrationsErrorCodes.SyncDirectionUnsupported,
        $"The configured provider cannot synchronise in the '{direction}' direction.");

    public static Error WebhookProviderUnknown() => Error.NotFound(
        IntegrationsErrorCodes.WebhookProviderUnknown,
        "No callback verifier is registered for that provider.");

    public static Error WebhookSignatureInvalid() => Error.Validation(
        IntegrationsErrorCodes.WebhookSignatureInvalid,
        "That callback's signature did not verify.");

    /// <summary>
    /// Turns a provider failure into a platform error.
    /// <para>
    /// <b>The single mapping, and the reason <see cref="IntegrationOutcome{T}"/> is a distinct type
    /// from <c>Result</c>.</b> Each arm chooses a status class deliberately:
    /// </para>
    /// <list type="bullet">
    /// <item><c>NotConfigured</c> and <c>Unsupported</c> are 400s: the request was well formed and
    /// this deployment cannot serve it, which is a fact about the deployment the client renders
    /// rather than an outage.</item>
    /// <item><c>Unauthorized</c> is a 401 with a code the client maps to <c>ExpiredCredential</c>,
    /// which is what drives "reconnect" rather than "try again".</item>
    /// <item><c>RateLimited</c> is a 429 carrying the provider's own delay.</item>
    /// <item><c>Unavailable</c> is a 503 — retryable — and <c>ProviderError</c> is a 502, which is
    /// not. §22 forbids collapsing these into one 500, and the difference is exactly whether a
    /// client should offer a retry button.</item>
    /// </list>
    /// </summary>
    public static Error FromProvider<T>(IntegrationOutcome<T> outcome, string capability)
    {
        if (outcome.Succeeded)
        {
            throw new InvalidOperationException("A successful outcome is not an error.");
        }

        var message = string.IsNullOrWhiteSpace(outcome.Message)
            ? $"The provider could not complete {capability}."
            : outcome.Message;

        return outcome.FailureKind switch
        {
            IntegrationFailureKind.NotConfigured =>
                Error.Validation(IntegrationsErrorCodes.ProviderNotConfigured, message),

            IntegrationFailureKind.Unsupported =>
                Error.Validation(IntegrationsErrorCodes.CapabilityUnsupported, message),

            IntegrationFailureKind.Unauthorized =>
                Error.Unauthorized(IntegrationsErrorCodes.ReauthorizationRequired, message),

            IntegrationFailureKind.InvalidRequest =>
                Error.Validation(IntegrationsErrorCodes.ProviderRejected, message),

            // The provider's own delay travels in the message, because it is the only field the
            // platform's error contract has. §21 requires `Retry-After` to be respected rather than
            // guessed at, and a delay the gateway captured and the mapping dropped would be
            // respected only up to here — the client would go back to inventing a countdown, which
            // is exactly what the register objects to on the OTP surface for the same reason.
            IntegrationFailureKind.RateLimited =>
                Error.RateLimited(
                    IntegrationsErrorCodes.ProviderRateLimited, WithRetryAfter(message, outcome)),

            IntegrationFailureKind.Unavailable =>
                Error.ServiceUnavailable(IntegrationsErrorCodes.ProviderUnavailable, message),

            _ => Error.Unexpected(IntegrationsErrorCodes.ProviderRejected, message)
        };
    }

    /// <summary>
    /// Appends the provider's own retry delay, when it gave one.
    /// <para>
    /// Rounded up to whole seconds, because a member reads it: "try again in 31 seconds" is a
    /// countdown and "try again in 30.4 seconds" is a number somebody has to think about. Absent
    /// when the provider said nothing — this platform does not invent a delay on a provider's
    /// behalf, and a guessed one would be worse than none because a client would render it as a
    /// promise.
    /// </para>
    /// </summary>
    private static string WithRetryAfter<T>(string message, IntegrationOutcome<T> outcome)
    {
        if (outcome.RetryAfter is not { TotalSeconds: > 0 } delay)
        {
            return message;
        }

        var seconds = (int)Math.Ceiling(delay.TotalSeconds);
        var suffix = $" Try again in {seconds} second{(seconds == 1 ? string.Empty : "s")}.";

        return message.TrimEnd().EndsWith('.') ? message.TrimEnd() + suffix : message + "." + suffix;
    }
}
