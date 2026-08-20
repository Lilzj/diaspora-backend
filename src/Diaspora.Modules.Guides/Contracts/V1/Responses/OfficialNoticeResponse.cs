namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One official notice in full.
/// <para>
/// The card plus everything a detail screen needs: the body, the sourcing, the attachments, the
/// related items and the translations that exist. It embeds the card rather than repeating its
/// twenty fields, so a client that has already rendered a list can update in place without a second
/// mapping.
/// </para>
/// </summary>
public sealed record OfficialNoticeResponse
{
    /// <summary>Everything a list already showed. Embedded rather than repeated.</summary>
    public required OfficialNoticeCardResponse Card { get; init; }

    /// <summary>The full body, already localized to the caller's locale.</summary>
    public required string Content { get; init; }

    /// <summary>The authority's own reference — a circular, gazette or file number.</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>The authority's own page. Absolute http or https.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>The languages the authority itself published in. Not the ones we hold.</summary>
    public IReadOnlyList<string> Languages { get; init; } = [];

    /// <summary>
    /// The locales this platform holds a translation for.
    /// <para>
    /// The tags only, never the translations themselves: a client renders one locale and shipping the
    /// other twenty would multiply the response by twenty for a language picker that needs a list of
    /// codes.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> AvailableLocales { get; init; } = [];

    /// <summary>The locale actually served, after the exact→language→default fallback.</summary>
    public required string ResolvedLocale { get; init; }

    public IReadOnlyList<NoticeAttachmentResponse> Attachments { get; init; } = [];

    public IReadOnlyList<NoticeReferenceResponse> References { get; init; } = [];

    /// <summary>Why a verifier decided what they decided. Editors only; null for a reader.</summary>
    public string? VerificationNote { get; init; }

    /// <summary>The concurrency token to send back on an edit. Editors only.</summary>
    public int? Revision { get; init; }
}
