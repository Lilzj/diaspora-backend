using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Publishing;

/// <summary>
/// Publishes the current draft as the next version.
/// <para>
/// <b><c>[Transactional]</c>, and the reason is the version.</b> Publication writes two
/// aggregates — the guide's own state and the immutable <c>GuideVersion</c> that records what
/// readers were shown — and half of it succeeding is the worst outcome available: a published
/// guide with no version is a history with a hole in it, and a version with no publication is a
/// snapshot of something nobody ever saw.
/// </para>
/// <para>
/// Gated at <see cref="GuidePermissions.Publish"/>, whose tier floor is Verified. Drafting is
/// low-stakes engagement; putting long-lived knowledge out under the platform's name is exactly
/// the progression the access tiers exist to gate.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Publish)]
[Transactional(Reason = "The guide's publication and its immutable version snapshot must commit together.")]
[Auditable]
[Diaspora.Application.Behaviors.PostsInArea(Diaspora.Contracts.Moderation.V1.ModerationAreas.Guides)]
public sealed record PublishGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }

    public required PublishGuideRequest Request { get; init; }
}
