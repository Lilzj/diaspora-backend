using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// The read side of B6, for other modules.
/// <para>
/// <b>This interface is the promise the brief makes on B6's behalf.</b> Guides, Community
/// Updates and Community Life are told to consume Community rather than implement their own
/// social features; this is what consuming means. One call, batched, returning counts and viewer
/// state for a page of their own content.
/// </para>
/// <para>
/// Only the batch form exists. A singular convenience method would be used in a loop by the
/// first caller in a hurry, and the loop would be invisible until a page of twenty took two
/// seconds.
/// </para>
/// </summary>
public interface ICommunitySocialService
{
    Task<IReadOnlyDictionary<string, SocialSummary>> GetSummariesAsync(
        IReadOnlyCollection<ContentReference> contents,
        UserId? viewerId,
        CancellationToken cancellationToken = default);
}
