using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Computes reading time from a word count, at the platform's one fixed speed.
/// <para>
/// Static, because there is no state and no judgement: the number's whole job is comparability
/// across guides, and comparability dies the moment two call sites can disagree about the
/// divisor. The aggregate applies the same arithmetic at publish; this exists for previews and
/// projections, and a test pins the two to each other.
/// </para>
/// </summary>
public static class GuideReadingTimeCalculator
{
    /// <summary>Minutes to read a given word count. Never below one — "0 min read" is a lie.</summary>
    public static int CalculateMinutes(int wordCount) =>
        Math.Max(1, wordCount / GuideLimits.WordsPerMinute);

    /// <summary>The guide's current draft, measured. What a preview shows before publication.</summary>
    public static int CalculateMinutes(Guide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        return CalculateMinutes(guide.WordCount);
    }
}
