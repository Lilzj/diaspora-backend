using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.Modules.Recommendations.Infrastructure.Explainability;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Application.Services;

/// <summary>
/// Records what a surface served when the surface is not ranked by the engine.
/// <para>
/// <b>The monetization and community surfaces rank from facts another module owns</b>, so they do not
/// go through <c>IRecommendationEngine</c>; but what they served must still be a
/// <see cref="RecommendationSet"/>. That record is what lets <c>POST /recommendations/dismiss</c>
/// attribute a dismissal to the strategy and the reason that produced the card, and what the
/// analytics measure acceptance against — the same write-on-a-read-path the engine makes, for the
/// same reason.
/// </para>
/// </summary>
public sealed class ServedSetRecorder
{
    /// <summary>Longest reason sentence the set's explanation column holds.</summary>
    private const int MaxReasonLength = 512;

    private readonly IRecommendationSetRepository _sets;
    private readonly IGuidGenerator _guids;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<RecommendationOptions> _options;

    public ServedSetRecorder(
        IRecommendationSetRepository sets,
        IGuidGenerator guids,
        IUnitOfWork unitOfWork,
        IOptions<RecommendationOptions> options)
    {
        _sets = sets;
        _guids = guids;
        _unitOfWork = unitOfWork;
        _options = options;
    }

    /// <summary>
    /// Saves the page and returns its identifier, or null when the deployment does not persist
    /// generated sets or there was nothing to serve.
    /// </summary>
    public async Task<RecommendationSetId?> RecordAsync(
        UserId userId,
        RecommendationSurface surface,
        RecommendationTargetType targetType,
        string strategyKey,
        IReadOnlyList<ServedItem> items,
        int candidateCount,
        int excludedCount,
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0 || !_options.Value.PersistGeneratedSets)
        {
            return null;
        }

        var setId = RecommendationSetId.From(_guids.NewSequentialGuid());

        var set = RecommendationSet.Generate(
            setId,
            userId,
            surface,
            items.Select((item, position) => RecommendationItem.Create(
                _guids.NewSequentialGuid(),
                TargetReference.Create(targetType, item.TargetId),
                position,
                item.Score,
                strategyKey,
                Explain(item))),
            asOf,
            asOf.Add(_options.Value.SetLifetime),
            isColdStart: false,
            candidateCount,
            excludedCount,
            generationMilliseconds: 0,
            targetType);

        _sets.Add(set);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return setId;
    }

    /// <summary>The stored explanation: the code, its resource key, and the sentence served.</summary>
    public static RecommendationExplanation Explain(ServedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var text = item.ReasonText.Length > MaxReasonLength
            ? item.ReasonText[..MaxReasonLength]
            : item.ReasonText;

        return RecommendationExplanation.Create(
            item.ReasonCode,
            RecommendationReasonCatalog.ResourceKeyFor(item.ReasonCode),
            text,
            ToConfidence(item.Score));
    }

    public static RecommendationConfidence ToConfidence(decimal score) => score switch
    {
        >= 0.7m => RecommendationConfidence.High,
        >= 0.45m => RecommendationConfidence.Medium,
        _ => RecommendationConfidence.Low
    };
}

/// <summary>One served card, as the set records it.</summary>
public sealed record ServedItem(
    Guid TargetId, decimal Score, RecommendationReasonCode ReasonCode, string ReasonText);
