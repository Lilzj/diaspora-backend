using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Curation;

/// <summary>
/// Removes a guide from one country's curated shelf.
/// <para>
/// Says nothing about the guide itself: it remains published, discoverable and exactly as good as it
/// was. Unfeaturing is a decision about what a country page leads with, not a judgement about
/// content.
/// </para>
/// </summary>
[Authorize(Permission = KnowledgePermissions.Curate)]
[Auditable]
public sealed record UnfeatureKnowledgeGuideCommand : ICommand
{
    public required string CountryCode { get; init; }

    public required Guid GuideId { get; init; }

    /// <summary>The shelf it sits on, or null for the country's own. Shelves are separate.</summary>
    public string? KnowledgeCategoryKey { get; init; }
}
