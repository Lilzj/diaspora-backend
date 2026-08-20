using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Audiences;

/// <summary>
/// Who should be told about something that names no individual.
/// <para>
/// <b>The seam that broadcast notifications need and that no published contract can supply.</b> "A
/// new immigration guide for Canada" concerns everybody living in or moving to Canada, and neither
/// Guides nor the Knowledge Directory knows who those people are — nor should they. Identity does,
/// and does not publish it, which is the correct decision about a list of who lives where.
/// </para>
/// <para>
/// So this module declares the question and ships an implementation that answers "nobody". A country
/// audience arrives later as either an Identity-backed adapter or, better, a subscription model
/// where members opt into a country's updates — and when it does, the Knowledge consumers, the
/// rules, the templates and the digest all already work.
/// </para>
/// <para>
/// Resolving an audience is bounded by contract: an implementation returns at most
/// <paramref name="limit"/> members and the caller pages. A broadcast that fans out to a hundred
/// thousand inboxes inside one consumed event would hold a transaction open across the whole of it.
/// </para>
/// </summary>
public interface INotificationAudienceResolver
{
    /// <summary>
    /// Members interested in a country's updates, or an empty list when nothing can answer.
    /// </summary>
    Task<IReadOnlyList<UserId>> ResolveCountryAudienceAsync(
        string countryCode,
        string categoryKey,
        int limit,
        CancellationToken cancellationToken = default);
}
