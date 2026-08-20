namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Writes or revises a review.
/// <para>
/// The client's write-review screen exactly: a one-to-five star selector and an optional body it
/// caps at five hundred characters. <see cref="ServiceId"/> is added, so a provider can see which
/// of its offerings people rate well — which one aggregate score can never say.
/// </para>
/// </summary>
public sealed record WriteProviderReviewRequest
{
    /// <summary>One to five.</summary>
    public required int Rating { get; init; }

    public string? Title { get; init; }

    /// <summary>Optional, capped at the client's own five hundred characters.</summary>
    public string? Body { get; init; }

    /// <summary>The service reviewed, when the member names one.</summary>
    public Guid? ServiceId { get; init; }
}
