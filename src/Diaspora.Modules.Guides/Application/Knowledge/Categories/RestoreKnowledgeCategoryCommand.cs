using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Knowledge.Categories;

/// <summary>
/// Returns an archived navigation row to the catalogue.
/// <para>
/// <b>The one command that must read past the global query filter.</b> Archiving is a soft delete,
/// so every ordinary read hides the row — and a restore has to load exactly the row the archiving
/// hid. The platform has this blind spot on record: without a repository member that ignores the
/// filter, restore can only ever answer "not found", which is the one answer that is certainly wrong
/// for a row the curator can see in their own archive list.
/// </para>
/// </summary>
[RequirePermission(KnowledgePermissions.ManageCategories)]
[Auditable]
public sealed record RestoreKnowledgeCategoryCommand : ICommand<KnowledgeCategoryResponse>
{
    public required string Key { get; init; }
}
