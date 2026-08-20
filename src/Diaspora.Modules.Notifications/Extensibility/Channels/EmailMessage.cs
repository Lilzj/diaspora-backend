namespace Diaspora.Modules.Notifications.Extensibility.Channels;

/// <summary>
/// An email, expressed without any provider's vocabulary.
/// <para>
/// <b>The body is text or Markdown and never HTML.</b> Accepting HTML from a template author means
/// accepting a script tag from a template author, and the sanitiser that would make that safe is a
/// dependency this module does not need in order to say "your interview is at 14:00". An adapter
/// that wants to wrap the Markdown in a house template is free to; that is a rendering decision
/// belonging to whoever owns the house style, not to this record.
/// </para>
/// </summary>
public sealed record EmailMessage
{
    public required IReadOnlyList<ChannelEndpoint> Recipients { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    /// <summary><c>Plain</c> or <c>Markdown</c>.</summary>
    public required string Format { get; init; }

    /// <summary>The BCP-47 tag, for the <c>Content-Language</c> header an adapter may set.</summary>
    public required string Language { get; init; }

    /// <summary>The primary route, which an adapter turns into whatever absolute link it owns.</summary>
    public string? Route { get; init; }

    public required Guid NotificationId { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>
    /// A signed, single-category unsubscribe token for this recipient — B13.3.
    /// <para>
    /// <b>The adapter builds the link; this module refuses to.</b> A URL needs an origin, and this
    /// module has no idea what a deployment's public host is — inventing one is how a platform emails
    /// people links to <c>localhost</c>. The adapter that already owns the house template and the
    /// absolute route composes it, and puts it in the footer and in the <c>List-Unsubscribe</c>
    /// header.
    /// </para>
    /// <para>
    /// <b>Null for a mandatory category, and that is the contract.</b> Security and moderation mail
    /// cannot be switched off, so a footer offering it would be an offer the platform then refuses —
    /// and a member who followed it and stayed subscribed would reasonably conclude the platform
    /// ignores unsubscribes.
    /// </para>
    /// </summary>
    public string? UnsubscribeToken { get; init; }
}
