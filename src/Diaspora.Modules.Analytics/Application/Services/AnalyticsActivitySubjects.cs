using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>What a subject resolved to: the whole platform, one owner's work, or one item.</summary>
public enum AnalyticsActivitySubjectKind
{
    Platform = 1,

    Owner = 2,

    Content = 3
}

/// <summary>
/// A subject the caller may read, resolved to the rows that answer for it.
/// </summary>
/// <param name="Subject">The subject as the response names it.</param>
/// <param name="Kind">Platform, owner or one item.</param>
/// <param name="OwnerUserId">The member whose work it is, for an owner subject.</param>
/// <param name="ContentType">The one kind of content counted, or null for all of an owner's work.</param>
/// <param name="ContentId">The item, for a content subject.</param>
/// <param name="SelfUserId">
/// Whose own activity is not audience: the owner of the work or the item. Their publishing and their
/// own visits are recorded against it, and counting them as somebody who came back would be wrong in
/// the flattering direction. Null for the platform, where everybody is the audience.
/// </param>
public sealed record AnalyticsActivitySubject(
    AnalyticsSubjectResponse Subject,
    AnalyticsActivitySubjectKind Kind,
    Guid? OwnerUserId,
    AnalyticsContentType? ContentType,
    Guid? ContentId,
    Guid? SelfUserId = null)
{
    public AnalyticsActivityScope Scope => new(OwnerUserId, ContentType, ContentId);
}

/// <summary>
/// Resolves a <c>subjectType</c>/<c>subjectId</c> pair for the retention, leaderboard and insight
/// reads — with the access rules every existing owner and item dataset already applies.
/// <para>
/// <b>Role subjects are the caller's own standing.</b> <c>creator</c>, <c>seller</c>,
/// <c>landlord</c>, <c>employer</c> and <c>provider</c> are asked with the caller's own user id or
/// none; a platform reader may name another member's (except <c>provider</c>, whose other ids are
/// provider profiles, read as items). <b>An item is its owner's</b>, through the content dimension,
/// and "not yours" answers exactly as "does not exist". <b>The platform is a platform reader's.</b>
/// </para>
/// </summary>
public sealed class AnalyticsActivitySubjects
{
    private static readonly Dictionary<string, AnalyticsContentType?> RoleSubjects =
        new(StringComparer.Ordinal)
        {
            ["creator"] = null,
            ["seller"] = AnalyticsContentType.Marketplace,
            ["landlord"] = AnalyticsContentType.Housing,
            ["employer"] = AnalyticsContentType.Job,
            ["provider"] = AnalyticsContentType.Provider
        };

    /// <summary>Subjects nothing in the raw store is keyed by, with the reason.</summary>
    private static readonly Dictionary<string, string> Uncountable =
        new(StringComparer.Ordinal)
        {
            ["community"] =
                "Activity is not recorded per community on this deployment; a community's membership figures are at /analytics/communities/{id}.",
            ["country"] =
                "Activity is not recorded per member country on this deployment.",
            ["neighborhood"] =
                "Activity is not recorded per neighbourhood on this deployment.",
            ["campaign"] =
                "A campaign's figures are its giving, at /analytics/campaigns/{id}; visits to a campaign are not recorded."
        };

    private readonly AnalyticsQueryContext _context;

    public AnalyticsActivitySubjects(AnalyticsQueryContext context) => _context = context;

    /// <summary>
    /// The subject, or the refusal the caller should see.
    /// </summary>
    /// <param name="subjectType">The client's subject type; empty means <paramref name="defaultType"/>.</param>
    /// <param name="subjectId">The client's subject id, where the type takes one.</param>
    /// <param name="callerId">The signed-in caller.</param>
    /// <param name="allowItems">Whether one item is a meaningful subject for this read.</param>
    /// <param name="defaultType">The subject type an empty one means.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<Result<AnalyticsActivitySubject>> ResolveAsync(
        string? subjectType,
        string? subjectId,
        Guid callerId,
        bool allowItems,
        string defaultType,
        CancellationToken cancellationToken)
    {
        var type = subjectType?.Trim().ToLowerInvariant() is { Length: > 0 } given ? given : defaultType;
        var id = string.IsNullOrWhiteSpace(subjectId) ? null : subjectId.Trim();

        if (type == "platform")
        {
            return _context.IsPlatformReader
                ? Result.Success(new AnalyticsActivitySubject(
                    new AnalyticsSubjectResponse { Type = "platform", Id = null, Label = null },
                    AnalyticsActivitySubjectKind.Platform,
                    null,
                    null,
                    null))
                : Result.Failure<AnalyticsActivitySubject>(AnalyticsErrors.PlatformReadRequired);
        }

        if (Uncountable.TryGetValue(type, out var reason))
        {
            return Result.Failure<AnalyticsActivitySubject>(AnalyticsErrors.SubjectUnsupportedBecause(reason));
        }

        if (RoleSubjects.TryGetValue(type, out var roleContentType))
        {
            Guid? owner = id is null
                ? callerId
                : Guid.TryParse(id, out var parsed) ? parsed : null;

            if (owner == callerId || (owner is not null && type != "provider" && _context.IsPlatformReader))
            {
                return Result.Success(new AnalyticsActivitySubject(
                    new AnalyticsSubjectResponse
                    {
                        Type = type,
                        Id = type == "creator" && owner == callerId ? null : owner!.Value.ToString(),
                        Label = null
                    },
                    AnalyticsActivitySubjectKind.Owner,
                    owner,
                    roleContentType,
                    null,
                    owner));
            }

            // `provider` is a role and a content type; any other id is one provider's profile.
            if (type != "provider")
            {
                return Result.Failure<AnalyticsActivitySubject>(AnalyticsErrors.SubjectNotFound);
            }
        }

        var contentType = AnalyticsContentTypes.FromRouteSegment(type);

        if (contentType == AnalyticsContentType.Unknown)
        {
            return Result.Failure<AnalyticsActivitySubject>(AnalyticsErrors.SubjectTypeUnsupported);
        }

        if (!allowItems)
        {
            return Result.Failure<AnalyticsActivitySubject>(AnalyticsErrors.SubjectUnsupportedBecause(
                "One item ranks nothing. Ask for the owner's board instead."));
        }

        if (!Guid.TryParse(id, out var contentId))
        {
            return Result.Failure<AnalyticsActivitySubject>(AnalyticsErrors.SubjectNotFound);
        }

        var ownership = await _context
            .RequireContentOwnerAsync(contentType, contentId, cancellationToken)
            .ConfigureAwait(false);

        if (ownership.IsFailure)
        {
            return Result.Failure<AnalyticsActivitySubject>(ownership.Error);
        }

        return Result.Success(new AnalyticsActivitySubject(
            new AnalyticsSubjectResponse
            {
                Type = AnalyticsContentTypes.ToSubjectType(contentType),
                Id = contentId.ToString(),
                Label = null
            },
            AnalyticsActivitySubjectKind.Content,
            null,
            contentType,
            contentId,
            ownership.Value));
    }
}
