using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// The display names of the members who reviewed a business.
/// <para>
/// <b>The gap this closes (16 Oct 2026).</b> <c>ProviderReviewResponse.ReviewerName</c> existed and
/// the mapper took a name, but no caller passed one, so every review read "A member". The name
/// belongs to Identity and reaches modules through <see cref="IMemberProfileService"/>; this is the
/// one place Providers asks it, once per page. A service that cannot answer leaves the names null.
/// </para>
/// </summary>
public sealed class ProviderReviewerNames
{
    private static readonly IReadOnlyDictionary<Guid, string> None = new Dictionary<Guid, string>();

    private readonly IMemberProfileService _members;

    public ProviderReviewerNames(IMemberProfileService members)
    {
        _members = members;
    }

    /// <summary>The members' display names, keyed by user identifier.</summary>
    public async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IEnumerable<UserId> reviewerIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reviewerIds);

        var distinct = reviewerIds.Distinct().ToList();

        if (distinct.Count == 0 || !_members.IsAvailable)
        {
            return None;
        }

        var profiles = await _members.ResolveAsync(distinct, cancellationToken);

        return profiles.ToDictionary(pair => pair.Key, pair => pair.Value.DisplayName);
    }

    /// <summary>One reviewer's display name, or null when it cannot be resolved.</summary>
    public async Task<string?> NameOfAsync(UserId reviewerId, CancellationToken cancellationToken)
    {
        var names = await ResolveAsync([reviewerId], cancellationToken);

        return names.GetValueOrDefault(reviewerId.Value);
    }
}
