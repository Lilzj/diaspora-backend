using Diaspora.Modules.Integrations.Domain.Enums;

namespace Diaspora.Modules.Integrations.Extensibility.Providers;

/// <summary>
/// What an outbound provider call produced.
/// <para>
/// <b>This type is the enforcement of §49, and it is the reason no adapter on this platform can
/// return a fake success.</b> There is exactly one way to construct a successful outcome and it
/// requires a value; every other constructor requires a
/// <see cref="Diaspora.Modules.Integrations.Domain.Enums.IntegrationFailureKind"/>. An adapter that
/// catches an exception and wants to carry on has nowhere to put "it probably worked" — the closest
/// available answer is <see cref="IntegrationFailureKind.Unavailable"/>, which is the true one.
/// </para>
/// <para>
/// <b>Not <c>Result&lt;T&gt;</c>.</b> The platform's result type carries an <c>Error</c> with a code
/// and a description, which is the right shape for a decision this platform made. A provider
/// failure is a decision somebody else made, and the two facts a caller needs about it — is it worth
/// retrying, and does the member have to do something — are not derivable from an error code. They
/// are derivable from <see cref="FailureKind"/>, which is why it is a distinct type. The application
/// layer maps one to the other in exactly one place.
/// </para>
/// </summary>
/// <typeparam name="T">What the call returns when it works.</typeparam>
public readonly record struct IntegrationOutcome<T>
{
    private IntegrationOutcome(
        bool succeeded,
        T? value,
        IntegrationFailureKind? failureKind,
        string? message,
        TimeSpan? retryAfter,
        string? providerCode)
    {
        Succeeded = succeeded;
        Value = value;
        FailureKind = failureKind;
        Message = message;
        RetryAfter = retryAfter;
        ProviderCode = providerCode;
    }

    public bool Succeeded { get; }

    /// <summary>Only meaningful when <see cref="Succeeded"/>.</summary>
    public T? Value { get; }

    /// <summary>Why not. Null exactly when <see cref="Succeeded"/>.</summary>
    public IntegrationFailureKind? FailureKind { get; }

    /// <summary>
    /// Member-facing, in plain language.
    /// <para>
    /// The adapter writes this, because the adapter is the only thing that knows what the provider
    /// said. It must not contain a provider payload, a URL with a token in it, or a stack.
    /// </para>
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// The provider's own <c>Retry-After</c>, honoured rather than guessed.
    /// <para>
    /// Only ever set alongside <see cref="IntegrationFailureKind.RateLimited"/>. A platform that
    /// invented its own backoff here would be arguing with a provider about the provider's quota.
    /// </para>
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// The provider's error code, for the operator.
    /// <para>
    /// Reaches telemetry and the connection's failure record and never a member. It is the string
    /// that turns "the calendar write failed" into something searchable in a vendor's documentation.
    /// </para>
    /// </summary>
    public string? ProviderCode { get; }

    public static IntegrationOutcome<T> Success(T value) =>
        new(true, value, null, null, null, null);

    public static IntegrationOutcome<T> Failure(
        IntegrationFailureKind kind,
        string message,
        TimeSpan? retryAfter = null,
        string? providerCode = null) =>
        new(false, default, kind, message, kind is IntegrationFailureKind.RateLimited ? retryAfter : null, providerCode);

    /// <summary>
    /// The answer a deployment with no adapter gives.
    /// <para>
    /// A named factory because it is the single most common non-success on this surface and because
    /// naming it makes the grep that finds every not-configured path a one-liner.
    /// </para>
    /// </summary>
    public static IntegrationOutcome<T> NotConfigured(string capability) =>
        Failure(
            IntegrationFailureKind.NotConfigured,
            $"This deployment has no provider configured for {capability}.");

    /// <summary>The answer an adapter gives for something its provider genuinely cannot do.</summary>
    public static IntegrationOutcome<T> Unsupported(string capability) =>
        Failure(
            IntegrationFailureKind.Unsupported,
            $"This provider does not support {capability}.");

    /// <summary>Re-shapes a failure for a different value type, keeping every detail.</summary>
    public IntegrationOutcome<TOther> CarryFailure<TOther>() =>
        Succeeded
            ? throw new InvalidOperationException("A successful outcome carries no failure.")
            : IntegrationOutcome<TOther>.Failure(
                FailureKind!.Value, Message ?? string.Empty, RetryAfter, ProviderCode);
}

/// <summary>Nothing, for calls that succeed without producing a value.</summary>
public sealed record IntegrationVoid
{
    public static readonly IntegrationVoid Instance = new();
}
