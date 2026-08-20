using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Tags;

/// <summary>
/// Replaces the guide's tag set wholesale.
/// <para>
/// Wholesale because that is how the client's chip editor saves: a person adds two and removes
/// one, then saves. A per-tag API would make the common case three round trips with a
/// partial-failure story, and would make "these are the tags" something no single request could
/// ever assert.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.ManageTags)]
[Auditable]
public sealed record SetGuideTagsCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required SetGuideTagsRequest Request { get; init; }
}
