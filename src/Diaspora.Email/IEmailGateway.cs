namespace Diaspora.Email;

/// <summary>
/// One email vendor's wire.
/// </summary>
/// <remarks>
/// Mirrors <c>ISmsGateway</c>, and for the same reason: the gateway is the only place that has
/// read the vendor's own error and is therefore the only place that can say whether a failure is
/// worth retrying. It throws that decision rather than returning it, so the resilience executor
/// classifies on the exception type and nothing between here and there has to re-derive it.
/// </remarks>
public interface IEmailGateway
{
    /// <summary>A value of <see cref="EmailProviderNames"/>.</summary>
    string Provider { get; }

    /// <summary>Whether this gateway has the credentials it needs.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Hands one message to the vendor.
    /// </summary>
    /// <exception cref="EmailTransientException">The vendor may accept this later.</exception>
    /// <exception cref="EmailPermanentException">The vendor will never accept this.</exception>
    Task<EmailDispatch> SendAsync(OutboundEmail message, CancellationToken cancellationToken = default);
}

/// <summary>
/// A message as a gateway needs it: addresses resolved, body rendered, nothing to look up.
/// </summary>
/// <remarks>
/// Deliberately not <c>EmailMessage</c>. That type belongs to Notifications and carries a
/// notification id, a category key and an unsubscribe token — none of which a vendor has any
/// business seeing. Translating at the boundary keeps the vendor ignorant of the platform, which
/// is the same reason <c>SmsMessage</c> is a destination and a body and nothing else.
/// </remarks>
public sealed record OutboundEmail
{
    public required IReadOnlyList<string> Recipients { get; init; }

    public required string Subject { get; init; }

    /// <summary>The plain-text part. Always present.</summary>
    /// <remarks>
    /// Required rather than optional, and that is the point. A message sent as HTML alone reads as
    /// empty to a text-only client and to a screen reader in plain mode, and scores badly with
    /// every spam filter that looks for a multipart body. The text part is the message; the HTML
    /// part is a presentation of it.
    /// </remarks>
    public required string TextBody { get; init; }

    /// <summary>The HTML part, when the message has one.</summary>
    /// <remarks>
    /// Null is a perfectly good value: a transactional message with nothing to lay out is better
    /// sent as text alone than wrapped in a shell that adds only weight.
    /// </remarks>
    public string? HtmlBody { get; init; }

    public required string FromAddress { get; init; }

    public required string FromName { get; init; }

    /// <summary>
    /// The value for a <c>List-Unsubscribe</c> header, when the message carries one.
    /// </summary>
    /// <remarks>
    /// Sent as a header rather than as body text because that is what mail clients and spam
    /// filters read. A bulk sender without it is a bulk sender that lands in spam.
    /// </remarks>
    public string? UnsubscribeUrl { get; init; }
}

/// <summary>What the vendor said when it accepted the message.</summary>
public sealed record EmailDispatch(string Provider, string? ProviderMessageId);

public abstract class EmailGatewayException : Exception
{
    protected EmailGatewayException(string provider, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
    }

    public string Provider { get; }
}

/// <summary>The vendor might accept this message on a later attempt.</summary>
public sealed class EmailTransientException : EmailGatewayException
{
    public EmailTransientException(string provider, string message, Exception? innerException = null)
        : base(provider, message, innerException)
    {
    }
}

/// <summary>
/// The vendor will never accept this message.
/// </summary>
/// <remarks>
/// Retrying a permanent refusal spends the retry budget on an outcome that cannot change, and —
/// for an unverified sending domain or a malformed address — hides a configuration fault behind
/// what looks like an outage.
/// </remarks>
public sealed class EmailPermanentException : EmailGatewayException
{
    public EmailPermanentException(string provider, string message, Exception? innerException = null)
        : base(provider, message, innerException)
    {
    }
}
