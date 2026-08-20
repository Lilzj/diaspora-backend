namespace Diaspora.Modules.Guides.Domain.Policies;

/// <summary>
/// The ceilings the aggregates enforce.
/// <para>
/// Constants in the domain rather than options in configuration, for the reason B6 and B7
/// record for their own limits: a bound that can be raised per deployment is a bound the
/// domain cannot rely on, and every invariant that depends on it becomes conditional. These
/// are the numbers the aggregate is correct at.
/// </para>
/// <para>
/// Every one is generous. They exist to bound the size of a single aggregate so loading one
/// guide is a predictable amount of work, and to stop a scripted client from writing forty
/// thousand rows before anyone notices — none of them is a business rule dressed as a limit.
/// </para>
/// </summary>
public static class GuideLimits
{
    /// <summary>Sections in one guide. A hundred is past every real how-to by an order of magnitude.</summary>
    public const int MaxSections = 100;

    /// <summary>Blocks in one section.</summary>
    public const int MaxBlocksPerSection = 200;

    /// <summary>Characters in one block's content.</summary>
    public const int MaxBlockLength = 20_000;

    /// <summary>Characters in a guide title.</summary>
    public const int MaxTitle = 200;

    /// <summary>The shortest title that can mean anything.</summary>
    public const int MinTitle = 3;

    /// <summary>Characters in a section title.</summary>
    public const int MaxSectionTitle = 200;

    /// <summary>Characters in a slug.</summary>
    public const int MaxSlugLength = 220;

    /// <summary>Tags on one guide. The client renders these as chips.</summary>
    public const int MaxTags = 10;

    /// <summary>Characters in one tag.</summary>
    public const int MaxTagLength = 40;

    /// <summary>Media references on one guide, across every role.</summary>
    public const int MaxMediaReferences = 50;

    /// <summary>Provider references on one guide.</summary>
    public const int MaxProviderReferences = 20;

    /// <summary>Characters in a provider reference's role label.</summary>
    public const int MaxProviderReferenceRole = 100;

    /// <summary>Characters in a provider reference's note.</summary>
    public const int MaxProviderReferenceNote = 500;

    /// <summary>Characters in a media caption.</summary>
    public const int MaxMediaCaption = 500;

    /// <summary>Auto-save snapshots kept per guide. The handler prunes to the newest.</summary>
    public const int MaxDraftSnapshots = 10;

    /// <summary>Characters in a summary.</summary>
    public const int MaxSummary = 500;

    /// <summary>Characters in a derived excerpt.</summary>
    public const int MaxExcerpt = 300;

    /// <summary>Characters in the revision notes attached to one published version.</summary>
    public const int MaxRevisionNotes = 1_000;

    /// <summary>Characters in one moderation note.</summary>
    public const int MaxModerationNote = 2_000;

    /// <summary>Characters in a suggestion's proposed content.</summary>
    public const int MaxSuggestionContent = MaxBlockLength;

    /// <summary>Characters in a suggestion's rationale or decision note.</summary>
    public const int MaxSuggestionRationale = 2_000;

    /// <summary>
    /// The reading speed the published reading time is computed at. An average adult reads
    /// procedural prose at roughly this rate; the number's job is comparability between guides,
    /// not accuracy for any one reader.
    /// </summary>
    public const int WordsPerMinute = 200;

    /// <summary>Characters in a category key.</summary>
    public const int MaxCategoryKey = 64;

    /// <summary>Characters in a category name.</summary>
    public const int MaxCategoryName = 100;

    /// <summary>Characters in a category description.</summary>
    public const int MaxCategoryDescription = 500;

    /// <summary>Characters in a region or city name copied from Location's catalogue.</summary>
    public const int MaxLocationName = 100;

    /// <summary>Characters in the copied, prefix-matchable area path.</summary>
    public const int MaxAreaPath = 500;

    /// <summary>Characters in an SEO meta title. Search engines truncate past this.</summary>
    public const int MaxSeoTitle = 70;

    /// <summary>Characters in an SEO meta description.</summary>
    public const int MaxSeoDescription = 160;

    /// <summary>Characters in a canonical URL.</summary>
    public const int MaxSeoUrl = 500;

    /// <summary>Characters in the comma-joined SEO keywords.</summary>
    public const int MaxSeoKeywords = 500;

    /// <summary>Versions returned in one listing page.</summary>
    public const int MaxVersionsListed = 50;

    /// <summary>Guides returned in one page of discovery.</summary>
    public const int MaxPageSize = 50;

    public const int DefaultPageSize = 20;
}
