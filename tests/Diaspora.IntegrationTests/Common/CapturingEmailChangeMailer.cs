using System.Collections.Concurrent;
using Diaspora.Application.Identity.Abstractions;

namespace Diaspora.IntegrationTests.Common;

/// <summary>One email the change flow sent, as the test host recorded it.</summary>
public sealed record SentEmailChangeMail(string Address, string Kind, string? Token, string? MaskedNewAddress);

/// <summary>
/// Records the email-change mail instead of sending it, as <see cref="CapturedOtpCodes"/> does for
/// codes. Shared by the collection, so a test reads only the addresses it made up.
/// </summary>
public sealed class CapturedEmailChangeMail
{
    public const string Link = "link";
    public const string Unavailable = "unavailable";
    public const string Requested = "requested";
    public const string Completed = "completed";

    private readonly ConcurrentQueue<SentEmailChangeMail> _sent = new();

    public void Record(SentEmailChangeMail mail) => _sent.Enqueue(mail);

    /// <summary>Everything sent to <paramref name="address"/>, oldest first.</summary>
    public IReadOnlyList<SentEmailChangeMail> To(string address) =>
        [.. _sent.Where(mail => string.Equals(mail.Address, address, StringComparison.OrdinalIgnoreCase))];

    /// <summary>The latest link sent to <paramref name="address"/>.</summary>
    public string LinkFor(string address) =>
        To(address).LastOrDefault(mail => mail.Kind == Link)?.Token
        ?? throw new InvalidOperationException($"No email change link was sent to {address}.");
}

/// <summary>Test stand-in for the email adapter's change mailer.</summary>
public sealed class CapturingEmailChangeMailer : IEmailChangeMailer
{
    private readonly CapturedEmailChangeMail _captured;

    public CapturingEmailChangeMailer(CapturedEmailChangeMail captured)
    {
        _captured = captured;
    }

    public bool IsConfigured => true;

    public Task<bool> SendConfirmationAsync(EmailChangeLinkMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        _captured.Record(new SentEmailChangeMail(message.Address, CapturedEmailChangeMail.Link, message.Token, null));
        return Task.FromResult(true);
    }

    public Task<bool> SendAddressUnavailableAsync(
        string address, string? language, CancellationToken cancellationToken = default)
    {
        _captured.Record(new SentEmailChangeMail(address, CapturedEmailChangeMail.Unavailable, null, null));
        return Task.FromResult(true);
    }

    public Task<bool> SendNoticeAsync(EmailChangeNotice notice, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notice);
        _captured.Record(new SentEmailChangeMail(
            notice.Address,
            notice.Stage == EmailChangeNoticeStage.Requested ? CapturedEmailChangeMail.Requested : CapturedEmailChangeMail.Completed,
            null,
            notice.MaskedNewAddress));
        return Task.FromResult(true);
    }
}
