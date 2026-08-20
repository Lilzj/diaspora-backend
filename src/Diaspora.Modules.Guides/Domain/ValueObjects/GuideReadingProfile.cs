using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.ValueObjects;

/// <summary>
/// What a reader should expect before opening the guide.
/// <para>
/// Reading time is computed at publication from the word count, never entered by the author —
/// an entered number flatters, a computed one is comparable across guides, and comparability is
/// the number's whole job. Completion time is different: how long the <em>process</em> takes
/// (weeks, for a visa) is something only the author knows, so it stays optional and manual.
/// </para>
/// </summary>
public sealed class GuideReadingProfile : ValueObject
{
    private GuideReadingProfile(
        GuideDifficulty difficulty, int readingTimeMinutes, int? estimatedCompletionMinutes)
    {
        Difficulty = difficulty;
        ReadingTimeMinutes = readingTimeMinutes;
        EstimatedCompletionMinutes = estimatedCompletionMinutes;
    }

    private GuideReadingProfile() { }

    /// <summary>What every new guide starts with, before anything is published.</summary>
    public static GuideReadingProfile Default => new(GuideDifficulty.Beginner, 1, null);

    public GuideDifficulty Difficulty { get; private set; }

    /// <summary>Minutes to read the guide, computed at publish. Never below one.</summary>
    public int ReadingTimeMinutes { get; private set; } = 1;

    /// <summary>Minutes to complete the process the guide describes, when the author says.</summary>
    public int? EstimatedCompletionMinutes { get; private set; }

    public static Result<GuideReadingProfile> Create(
        GuideDifficulty difficulty, int readingTimeMinutes, int? estimatedCompletionMinutes = null)
    {
        if (readingTimeMinutes < 1 || estimatedCompletionMinutes is < 1)
        {
            return Result.Failure<GuideReadingProfile>(GuideErrors.ReadingProfileInvalid);
        }

        return Result.Success(
            new GuideReadingProfile(difficulty, readingTimeMinutes, estimatedCompletionMinutes));
    }

    /// <summary>The same profile with a recomputed reading time. Used at publish.</summary>
    public GuideReadingProfile WithReadingTime(int readingTimeMinutes) =>
        new(Difficulty, Math.Max(1, readingTimeMinutes), EstimatedCompletionMinutes);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Difficulty;
        yield return ReadingTimeMinutes;
        yield return EstimatedCompletionMinutes;
    }
}
