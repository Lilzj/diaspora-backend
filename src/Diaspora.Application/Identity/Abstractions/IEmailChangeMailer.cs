namespace Diaspora.Application.Identity.Abstractions;

/// <summary>
/// The four emails an address change sends.
/// <list type="bullet">
///   <item><see cref="SendConfirmationAsync"/> — to the <b>new</b> address: the link that makes the change.</item>
///   <item><see cref="SendAddressUnavailableAsync"/> — to the <b>new</b> address, in place of the link,
///   when another account already signs in with it. The person asking is told nothing different;
///   the owner of the mailbox is told someone tried.</item>
///   <item><see cref="SendNoticeAsync"/> with <see cref="EmailChangeNoticeStage.Requested"/> and
///   <see cref="EmailChangeNoticeStage.Completed"/> — to the <b>old</b> address, so a member whose
///   session was taken notices, with the new address masked.</item>
/// </list>
/// <para>
/// Every method answers true when the provider accepted the message and never logs an address or
/// a link.
/// </para>
/// </summary>
public interface IEmailChangeMailer
{
    /// <summary>False until an email transport is configured. The request route answers 503 then.</summary>
    bool IsConfigured { get; }

    Task<bool> SendConfirmationAsync(EmailChangeLinkMessage message, CancellationToken cancellationToken = default);

    Task<bool> SendAddressUnavailableAsync(
        string address, string? language, CancellationToken cancellationToken = default);

    Task<bool> SendNoticeAsync(EmailChangeNotice notice, CancellationToken cancellationToken = default);
}

public sealed record EmailChangeLinkMessage
{
    /// <summary>The new address, which the link is sent to.</summary>
    public required string Address { get; init; }

    /// <summary>The signed token. The mailer builds the link from its own public base URL.</summary>
    public required string Token { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public string? Language { get; init; }
}

public enum EmailChangeNoticeStage
{
    Requested = 0,
    Completed = 1
}

public sealed record EmailChangeNotice
{
    /// <summary>The old address, which the notice is sent to.</summary>
    public required string Address { get; init; }

    /// <summary>The new address with most of it hidden, e.g. <c>j*****@g****.com</c>.</summary>
    public required string MaskedNewAddress { get; init; }

    public required EmailChangeNoticeStage Stage { get; init; }

    /// <summary>When the change link expires. Set on a <see cref="EmailChangeNoticeStage.Requested"/> notice.</summary>
    public DateTimeOffset? LinkExpiresAt { get; init; }

    public string? Language { get; init; }
}
