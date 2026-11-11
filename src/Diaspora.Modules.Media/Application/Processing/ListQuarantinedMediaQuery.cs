using Diaspora.Application.Abstractions;
using Diaspora.Modules.Media.Contracts.V1.Responses;

namespace Diaspora.Modules.Media.Application.Processing;

/// <summary>
/// Every file a scanner refused or a moderator withheld, oldest first.
/// <para>
/// <b>The queue that releasing a quarantine has always needed and never had.</b> The release and
/// requeue routes both take a media identifier, and nothing in the platform produced one: a
/// moderator could act on a quarantined file only if somebody handed them its id, which meant in
/// practice that nothing was ever released. A control nobody can reach is not a control.
/// </para>
/// <para>
/// Oldest first, because a file sitting in quarantine is a member's upload that silently did not
/// appear, and the longest wait is the one nobody has looked at.
/// </para>
/// </summary>
[RequirePermission(MediaPermissions.Administer)]
public sealed record ListQuarantinedMediaQuery : IQuery<IReadOnlyList<QuarantinedMediaResponse>>
{
    public int? Limit { get; init; }
}
