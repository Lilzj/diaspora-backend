namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Everything needed to open a draft notice.
/// <para>
/// <b>Deliberately the smallest set the aggregate will accept.</b> An editor transcribing a circular
/// starts with a title, a body and who issued it; dates, references, attachments and related links
/// arrive over the next ten minutes as they read the rest of the page. A create that demanded all of
/// them would be a form nobody can submit halfway through the job they are actually doing.
/// </para>
/// </summary>
public sealed record CreateOfficialNoticeRequest
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>A key from the notice category catalogue.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>The body that issued it. Must issue for the same country.</summary>
    public required Guid AuthorityId { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Content { get; init; }

    /// <summary>
    /// PascalCase. Absent takes the category's default, which is the right answer nearly always.
    /// </summary>
    public string? Severity { get; init; }
}

/// <summary>
/// Everything about a notice an editor may rewrite.
/// <para>
/// <b>One request rather than eight, and it is a considered choice.</b> The aggregate exposes
/// content, classification, dates, source, region and four collections as separate operations,
/// because each has its own rule about when it is allowed. A single editing screen holds all of them
/// and saves once; splitting the API to match the aggregate would make that screen issue eight
/// requests, any of which can fail, leaving the notice in a state the editor did not ask for.
/// </para>
/// <para>
/// <b>Every field is optional and absent means "leave it".</b> A collection supplied is replaced
/// wholesale; a collection absent is untouched. That distinction is what lets a translation screen
/// save localizations without having to send the body back.
/// </para>
/// </summary>
public sealed record UpdateOfficialNoticeRequest
{
    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Content { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary>PascalCase.</summary>
    public string? Severity { get; init; }

    /// <summary>Higher pins higher on a country page. Absent leaves it.</summary>
    public int? Priority { get; init; }

    /// <summary>Move the notice to a different issuing body. Invalidates any verification.</summary>
    public Guid? AuthorityId { get; init; }

    /// <summary>The sub-national area, or an empty string to clear it.</summary>
    public string? Region { get; init; }

    // ── Sourcing ────────────────────────────────────────────────────────────────────────────

    public string? ReferenceNumber { get; init; }

    /// <summary>Absolute http or https. Checked for shape here and for policy by the validators.</summary>
    public string? SourceUrl { get; init; }

    // ── Dates ───────────────────────────────────────────────────────────────────────────────

    /// <summary>When the authority issued it. Cannot be in the future.</summary>
    public DateTimeOffset? PublicationDate { get; init; }

    public DateTimeOffset? EffectiveFrom { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Whether the three date fields are being set at all.
    /// <para>
    /// Needed because null is a meaningful value for every one of them — "this rule never expires" is
    /// a real answer, and indistinguishable from "I am not editing the dates" without this flag.
    /// </para>
    /// </summary>
    public bool SetDates { get; init; }

    // ── Collections. Supplied means replace; absent means leave. ────────────────────────────

    /// <summary>Languages the authority itself published in. BCP-47 tags.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    public IReadOnlyList<NoticeLocalizationInput>? Localizations { get; init; }

    public IReadOnlyList<NoticeAttachmentInput>? Attachments { get; init; }

    public IReadOnlyList<NoticeReferenceInput>? References { get; init; }

    /// <summary>The revision the editor was looking at. Absent skips the check.</summary>
    public int? ExpectedRevision { get; init; }
}

/// <summary>
/// One translation of a notice.
/// <para>
/// All three parts are required, and the aggregate refuses a partial one. A translated heading over
/// an untranslated instruction is worse than no translation: a reader picks it because the heading is
/// in their language, then cannot read the part that tells them what to do.
/// </para>
/// </summary>
public sealed record NoticeLocalizationInput
{
    public required string Locale { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string Content { get; init; }
}

/// <summary>One media reference. An identifier, never a URL.</summary>
public sealed record NoticeAttachmentInput
{
    public required Guid MediaId { get; init; }

    /// <summary>The accessible description of the asset.</summary>
    public string? Caption { get; init; }
}

/// <summary>One related item — a guide, an event, a mission.</summary>
public sealed record NoticeReferenceInput
{
    /// <summary>PascalCase — <c>Guide</c>, <c>LearningPath</c>, <c>Event</c>.</summary>
    public required string Kind { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Why the editor linked it.</summary>
    public string? Label { get; init; }
}

/// <summary>A verifier's decision on a submitted notice.</summary>
public sealed record VerifyOfficialNoticeRequest
{
    /// <summary>Whether the source held up.</summary>
    public required bool Approved { get; init; }

    /// <summary>Why. Shown to editors, never to readers.</summary>
    public string? Note { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>Which notice replaces this one.</summary>
public sealed record SupersedeOfficialNoticeRequest
{
    /// <summary>The replacement. Must be published, and must not be this notice.</summary>
    public required Guid ReplacementNoticeId { get; init; }

    public int? ExpectedRevision { get; init; }
}

/// <summary>
/// One thing a reader did with a notice.
/// <para>
/// <b>A command rather than a side effect of the read, and that is deliberate.</b> A GET that
/// incremented a counter would count every prefetch, every crawler and every retry — and would make
/// the detail endpoint un-cacheable. The client says explicitly that somebody looked at this.
/// </para>
/// </summary>
public sealed record RecordNoticeActivityRequest
{
    /// <summary>One of <c>view</c>, <c>click-through</c>, <c>completion</c>, <c>guide</c>.</summary>
    public required string Activity { get; init; }

    /// <summary>
    /// Where the reader came from — <c>notification</c>, <c>recommendation</c>, <c>dashboard</c>,
    /// <c>search</c>. Counted, never trusted for anything else.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>The guide they crossed into, for a cross-navigation signal.</summary>
    public Guid? GuideId { get; init; }

    /// <summary>The reader's locale, when the client sends one. Never an identity.</summary>
    public string? Locale { get; init; }
}
