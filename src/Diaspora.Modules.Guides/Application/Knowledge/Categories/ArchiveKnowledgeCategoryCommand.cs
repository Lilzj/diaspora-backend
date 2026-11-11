using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Retires a navigation row from the catalogue.
/// <para>
/// <b>Archiving is the soft delete, and there is deliberately only one "gone" state.</b> Carrying
/// both an active flag and a deleted flag would give two ways for a row to be absent, two queries
/// that disagree about which, and a restore path that fixes one of them.
/// </para>
/// <para>
/// <b>Refused while a country still lists it.</b> Archiving a row that a published directory places
/// would silently empty a line on that country's page — the entry would resolve to nothing and the
/// composer would drop it, leaving a hole nobody could account for. The failure names the countries
/// so the curator knows where to go.
/// </para>
/// </summary>
[RequirePermission(KnowledgePermissions.ManageCategories)]
[Auditable]
public sealed record ArchiveKnowledgeCategoryCommand : ICommand
{
    public required string Key { get; init; }
}
