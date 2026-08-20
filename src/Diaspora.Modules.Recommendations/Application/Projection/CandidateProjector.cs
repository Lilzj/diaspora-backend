using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Recommendations.Application.Projection;

/// <summary>
/// The one place a published contract becomes a candidate row.
/// <para>
/// <b>Every consumer routes through this, which is what keeps the projection consistent across
/// eleven verticals.</b> Fifty consumers each writing their own upsert would be fifty places where
/// "null means unchanged" could be got subtly wrong, and the symptom of getting it wrong — a field
/// blanked on an unrelated update — is invisible until somebody notices a vertical ranking badly.
/// </para>
/// <para>
/// <b>It is not a repository and deliberately does not look like one.</b> A repository would tempt a
/// consumer into loading a candidate, mutating it and saving it, which is the shape that admits the
/// merge mistakes above. This offers exactly two operations: project what an event carried, and
/// withdraw.
/// </para>
/// </summary>
public sealed class CandidateProjector
{
    private readonly IRecommendationCandidateRepository _candidates;
    private readonly IGuidGenerator _guids;

    public CandidateProjector(IRecommendationCandidateRepository candidates, IGuidGenerator guids)
    {
        _candidates = candidates;
        _guids = guids;
    }

    /// <summary>
    /// Creates or refreshes a candidate from what an event carried.
    /// <para>
    /// Idempotent, which it has to be: integration event delivery is at-least-once, so the same
    /// publication arrives twice as a matter of course.
    /// </para>
    /// </summary>
    public async Task<RecommendationCandidate> ProjectAsync(
        CandidateProjectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidate = await _candidates.GetOrProjectAsync(
            request.TargetType,
            request.TargetId,
            request.SourceModule,
            request.Title ?? string.Empty,
            cancellationToken);

        candidate.Describe(
            request.Title,
            request.Summary,
            request.Slug,
            request.MediaReference,
            request.CategoryKey,
            request.LanguageCode);

        candidate.Place(
            request.CountryCode, request.RegionName, request.CityName, request.NeighborhoodName);

        candidate.Attribute(request.OwnerUserId, request.IsVerified, request.IsFeatured);

        candidate.Rate(request.PopularityScore, request.QualityScore, request.EngagementCount);

        if (request.Visibility is { } visibility)
        {
            candidate.Restrict(visibility, request.MinimumTrustLevel ?? 0);
        }
        else if (request.MinimumTrustLevel is { } trustLevel)
        {
            candidate.Restrict(candidate.Visibility, trustLevel);
        }

        candidate.Schedule(request.PublishedAt, request.SourceUpdatedAt, request.ExpiresAt);

        if (request.ReplaceAttributes || request.Attributes.Count > 0)
        {
            candidate.SetAttributes(
                request.Attributes
                    .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Key))
                    .DistinctBy(attribute => (attribute.Kind, attribute.Key.Trim().ToLowerInvariant()))
                    .Select(attribute => CandidateAttribute.Create(
                        _guids.NewSequentialGuid(), attribute.Kind, attribute.Key)));
        }

        // A republication brings a withdrawn candidate back. Without this, a job closed and reopened
        // would stay invisible for as long as the row survived the pruning sweep.
        candidate.Reinstate();

        _candidates.Update(candidate);

        RecommendationTelemetry.CandidatesProjected.Add(
            1, new KeyValuePair<string, object?>("target_type", request.TargetType.ToString()));

        return candidate;
    }

    /// <summary>
    /// Records a paid placement the Financial Platform activated over a subject.
    /// <para>
    /// <b>Finds rather than projects, and does nothing when there is no candidate.</b> A placement may
    /// be bought over a subject this module has never projected — B15's subject vocabulary spans ten
    /// types and several of them, a community or an update among them, may not be recommendable at
    /// all. Creating a row here would put a titleless candidate in the pool whose only distinguishing
    /// property is that somebody paid for it, which is the one candidate that must never be invented.
    /// </para>
    /// </summary>
    /// <returns>Whether a candidate existed and was promoted.</returns>
    public async Task<bool> PromoteAsync(
        RecommendationTargetType targetType,
        Guid targetId,
        decimal weight,
        DateTimeOffset? until,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _candidates.FindAsync(targetType, targetId, cancellationToken);

        if (candidate is null)
        {
            return false;
        }

        candidate.Promote(weight, until);

        _candidates.Update(candidate);

        RecommendationTelemetry.CandidatesPromoted.Add(
            1, new KeyValuePair<string, object?>("target_type", targetType.ToString()));

        return true;
    }

    /// <summary>
    /// Ends a paid placement over a subject.
    /// <para>
    /// Silent when there is no candidate, for the same reason <see cref="WithdrawAsync"/> is: there is
    /// nothing to end. Idempotent, which matters more here than elsewhere — B15's sweep and its
    /// subject-retirement consumers can both end one placement, so the same end arrives twice as a
    /// matter of course.
    /// </para>
    /// </summary>
    /// <returns>Whether a candidate existed and had its promotion ended.</returns>
    public async Task<bool> EndPromotionAsync(
        RecommendationTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _candidates.FindAsync(targetType, targetId, cancellationToken);

        if (candidate is null)
        {
            return false;
        }

        candidate.EndPromotion();

        _candidates.Update(candidate);

        RecommendationTelemetry.PromotionsEnded.Add(
            1, new KeyValuePair<string, object?>("target_type", targetType.ToString()));

        return true;
    }

    /// <summary>
    /// Marks a candidate unrecommendable.
    /// <para>
    /// Does nothing when the candidate was never projected, which is the normal case for a module
    /// that publishes a withdrawal for something published before this module existed. Silence is
    /// correct: there is nothing to withdraw.
    /// </para>
    /// </summary>
    public async Task WithdrawAsync(
        RecommendationTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _candidates.FindAsync(targetType, targetId, cancellationToken);

        if (candidate is null)
        {
            return;
        }

        candidate.Withdraw();

        _candidates.Update(candidate);

        RecommendationTelemetry.CandidatesWithdrawn.Add(
            1, new KeyValuePair<string, object?>("target_type", targetType.ToString()));
    }
}
