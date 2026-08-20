namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// A callback as it arrived, before anybody has believed a word of it.
/// <para>
/// <b>The raw body is kept as a string and the signature beside it.</b> Verifying a signature over a
/// re-serialised object is verifying a signature over something the provider never sent — a
/// mistake that produces an integration which works until the day a property order changes.
/// </para>
/// </summary>
public sealed record PaymentWebhookEnvelope
{
    public required string Provider { get; init; }

    /// <summary>Exactly the bytes that arrived, as text. Never re-serialised.</summary>
    public required string RawBody { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }
}
