using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;

namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One skill a candidate claims.
/// <para>
/// <b>Keyed identically to <see cref="JobSkillRequirement"/>, through the same normaliser.</b>
/// That is the whole reason both sides carry a key beside the display name: a posting asking for
/// "React.js" and a candidate claiming "reactjs" have to land on the same string or neither will
/// ever find the other. Sharing the function rather than the convention is what keeps it true.
/// </para>
/// <para>
/// The proficiency is the candidate's own claim and is never verified. It is a filter input,
/// not evidence — B9 implements no matching model, and AI candidate matching is out of scope.
/// </para>
/// </summary>
public sealed class CandidateSkill
{
    private CandidateSkill(
        string skillKey, string displayName, SkillProficiency proficiency, int? years, int order)
    {
        SkillKey = skillKey;
        DisplayName = displayName;
        Proficiency = proficiency;
        YearsOfExperience = years;
        Order = order;
    }

    private CandidateSkill() { }

    /// <summary>The normalised match key. Shares its normaliser with the posting side.</summary>
    public string SkillKey { get; private set; } = string.Empty;

    /// <summary>What the candidate typed. What their profile renders.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public SkillProficiency Proficiency { get; private set; }

    public int? YearsOfExperience { get; private set; }

    public int Order { get; private set; }

    internal static CandidateSkill? Create(
        string? name, SkillProficiency proficiency, int? years, int order)
    {
        var key = JobSkillRequirement.NormalizeKey(name);

        if (key is null)
        {
            return null;
        }

        // A negative count is a client bug; eighty years is a typo. Both are dropped rather
        // than refused — the skill itself is still worth recording.
        var sanitised = years is >= 0 and <= 70 ? years : null;

        var display = name!.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            display = display[..JobLimits.MaxDisplayName].TrimEnd();
        }

        return new CandidateSkill(key, display, proficiency, sanitised, order);
    }
}
