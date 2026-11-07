using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Extensibility.Content;

/// <summary>
/// How a module makes its content commentable, reactable, reportable and bookmarkable.
/// <para>
/// <b>This is the seam the brief is really asking for.</b> Guides (B8), Community Updates (B9)
/// and Community Life (B13) are told to consume Community rather than implement their own social
/// features, and this interface is the whole of what that costs them: one class that answers
/// "does this exist, who wrote it, what is it called, who can see it".
/// </para>
/// <para>
/// Community owns <see cref="CommunityContentType.Post"/> and
/// <see cref="CommunityContentType.Comment"/> itself. Every other type is unclaimed until a
/// module claims it, and social writes against an unclaimed type are refused rather than
/// silently accepted — accepting a comment on content nobody owns produces a row that can never
/// be rendered, moderated or notified about.
/// </para>
/// <para>
/// Two modules claiming the same content type fails startup, for the reason every other registry
/// on this platform does: two owners of one type means a race decides which one answers, and it
/// will decide differently in production than in test.
/// </para>
/// </summary>
public interface ICommunityContentSource
{
    /// <summary>The one content type this source owns.</summary>
    CommunityContentType ContentType { get; }

    /// <summary>
    /// Describes one item, or null when it does not exist.
    /// <para>
    /// Null rather than throwing: content disappearing between a client reading a feed and
    /// acting on it is the normal case, not an exceptional one.
    /// </para>
    /// </summary>
    Task<ContentDescriptor?> DescribeAsync(Guid contentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Describes many items at once.
    /// <para>
    /// Not optional, and not a convenience. Every batched read in Community — a bookmark list
    /// spanning six content types, a moderation queue, a mentions screen — resolves descriptors
    /// in bulk, and a source that only implemented the singular form would turn one page into
    /// twenty round trips through somebody else's module.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ContentDescriptor>> DescribeManyAsync(
        IReadOnlyCollection<Guid> contentIds, CancellationToken cancellationToken = default);

    // ApplyModerationAsync is gone (7 Nov 2026). Community's staff decision route called it to act
    // on another module's content with no reason and no notice, and the Guides override put the
    // author down as the moderator. A module that wants staff to act on its content implements
    // Diaspora.Contracts.Moderation.V1.IModerationTarget, and staff decide on the Trust case.
}
