namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Decide a credit grant waiting for a second Admin (7 Nov 2026). Another Admin approves or rejects;
/// the Admin who asked may only decline, which withdraws it.
/// </summary>
public sealed record DecideCreditGrantRequestRequest
{
    public required bool Approve { get; init; }

    /// <summary>Why it was declined. Required to decline; optional on an approval.</summary>
    public string? Note { get; init; }
}
