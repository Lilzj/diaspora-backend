using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Opens a new guide as a draft, owned by its author.
/// <para>
/// Creation is cheap on purpose. Knowledge is written over sessions, so the command asks for a
/// title, a category, a language and a jurisdiction and nothing else; structure, tags, media and
/// references all arrive through later edits against the same aggregate.
/// </para>
/// <para>
/// Not <c>[Transactional]</c>: the handler writes one aggregate and saves once, which EF already
/// makes atomic. Gated at <see cref="GuidePermissions.Create"/>, whose tier floor is Limited —
/// drafting is low-stakes engagement, and publishing is where the Verified floor bites.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Create)]
[Auditable]
public sealed record CreateGuideCommand : ICommand<GuideResponse>
{
    public required CreateGuideRequest Request { get; init; }
}
