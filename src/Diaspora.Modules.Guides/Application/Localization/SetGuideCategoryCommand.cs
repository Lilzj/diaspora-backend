using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Localization;

/// <summary>
/// Moves the guide to another catalogue category.
/// <para>
/// Its own command rather than a field on the editor save, because refiling is what a curator
/// does to somebody else's guide during a taxonomy tidy-up — a whole-document save would make
/// that act carry the risk of overwriting the author's in-flight edit.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.EditOwn)]
[Auditable]
public sealed record SetGuideCategoryCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required SetGuideCategoryRequest Request { get; init; }
}
