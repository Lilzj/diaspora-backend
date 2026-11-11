using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Referencing;

/// <summary>
/// Replaces the providers a guide points readers at.
/// <para>
/// Wholesale, like tags, because the editor's control is a list the author rearranges and saves.
/// Each identifier is checked against B7's directory before it is written — a guide must not
/// recommend a ghost, and a reference to a provider that never existed is a dead end a reader
/// discovers at the worst possible moment.
/// </para>
/// <para>
/// Only the identifier and the author's editorial text are stored. Name, slug, badge and logo
/// are hydrated from B7 at read time, so a provider that renames itself or loses its
/// verification is never misrepresented by an old guide.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.EditOwn)]
[Auditable]
public sealed record SetGuideProviderReferencesCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required SetGuideProviderReferencesRequest Request { get; init; }
}
