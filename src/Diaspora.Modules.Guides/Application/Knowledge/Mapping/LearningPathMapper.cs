using Diaspora.Modules.Guides.Application.Knowledge.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.LearningPaths;
using Diaspora.Modules.Guides.Integration;

namespace Diaspora.Modules.Guides.Application.Knowledge.Mapping;

/// <summary>
/// Turns a curated learning path into the shapes the wire and the consuming modules read.
/// <para>
/// The full response takes a resolved card lookup rather than resolving guides itself, so a country
/// page rendering four paths resolves their guides once across all four rather than four times.
/// </para>
/// </summary>
public static class LearningPathMapper
{
    /// <summary>
    /// The full path, with each step's guide resolved where it still exists.
    /// <para>
    /// A step whose guide has been unpublished keeps its place with a null card. Dropping the step
    /// would leave a reader with a six-step sequence numbered 1 to 7, which is a worse answer than
    /// a step that says what to do and has no link yet.
    /// </para>
    /// </summary>
    public static LearningPathResponse ToResponse(
        LearningPath path, IReadOnlyDictionary<Guid, GuideCardResponse> cards)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(cards);

        return new LearningPathResponse
        {
            LearningPathId = path.Id.Value,
            Key = path.Key,
            CountryCode = path.CountryCode,
            Title = path.Title,
            Description = path.Description,
            Language = path.Language,
            Status = KnowledgeWireNames.ToWire(path.Status),
            StepCount = path.Steps.Count,
            RequiredStepCount = path.RequiredStepCount,
            PublishedAt = path.PublishedAt,
            Revision = path.Revision,
            Steps =
            [
                .. path.Steps
                    .OrderBy(step => step.Position)
                    .Select(step => new LearningPathStepResponse
                    {
                        StepId = step.StepId,
                        Position = step.Position,
                        Title = step.Title,
                        Description = step.Description,
                        IsOptional = step.IsOptional,
                        KnowledgeCategoryKey = step.KnowledgeCategoryKey,
                        Guide = step.GuideId is { } guideId
                            && cards.TryGetValue(guideId, out var card)
                                ? card
                                : null
                    })
            ]
        };
    }

    public static LearningPathSummaryResponse ToSummary(LearningPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new LearningPathSummaryResponse
        {
            LearningPathId = path.Id.Value,
            Key = path.Key,
            CountryCode = path.CountryCode,
            Title = path.Title,
            Description = path.Description,
            Language = path.Language,
            Status = KnowledgeWireNames.ToWire(path.Status),
            StepCount = path.Steps.Count,
            RequiredStepCount = path.RequiredStepCount
        };
    }

    /// <summary>The consuming-module shape. Titles and lengths, never steps.</summary>
    public static LearningPathDescriptor ToDescriptor(LearningPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new LearningPathDescriptor
        {
            LearningPathId = path.Id.Value,
            Key = path.Key,
            CountryCode = path.CountryCode,
            Title = path.Title,
            Description = path.Description,
            Language = path.Language,
            StepCount = path.Steps.Count,
            RequiredStepCount = path.RequiredStepCount
        };
    }

    /// <summary>
    /// The guide identifiers a path's steps name, for the batched resolution.
    /// <para>
    /// Exposed rather than derived inside <see cref="ToResponse"/> because the caller needs them
    /// <em>before</em> mapping: it collects identifiers from every path, resolves once, and then
    /// maps. Deriving them during mapping would be resolving during mapping, which is the N+1 this
    /// design exists to prevent.
    /// </para>
    /// </summary>
    public static IReadOnlyList<Guid> GuideIdsOf(LearningPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return [.. path.Steps.Select(step => step.GuideId).OfType<Guid>().Distinct()];
    }
}
