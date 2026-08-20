namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// One country's knowledge, described in terms every consuming module can read.
/// <para>
/// <b>Flat primitives, for the same reason <see cref="GuideSummary"/> is flat.</b> A Jobs screen
/// that wants to show "new to Mauritius? start here" must be able to render this without
/// referencing B8.1's domain — the moment it had to, every knowledge-adjacent module would be
/// coupled to this module's internals rather than to its contract.
/// </para>
/// </summary>
public sealed record CountryKnowledgeSummary
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The country's name, localized where the caller asked for a locale.</summary>
    public required string DisplayName { get; init; }

    public string? Summary { get; init; }

    /// <summary>The language most guides here are written in.</summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>Whether readers can see this directory at all.</summary>
    public required bool IsPublished { get; init; }

    /// <summary>Published guides for this country.</summary>
    public required int GuideCount { get; init; }

    /// <summary>Navigation rows this country lists, hidden ones excluded.</summary>
    public required int CategoryCount { get; init; }

    /// <summary>Published onboarding routes for this country.</summary>
    public required int LearningPathCount { get; init; }
}
