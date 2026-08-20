using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;

namespace Diaspora.Modules.Jobs.Domain.Postings;

/// <summary>
/// One skill a posting asks for.
/// <para>
/// <b>The key is normalised and the display name is kept beside it.</b> The key is what a
/// filter matches and what the skill catalogue is keyed by; the name is what the employer typed
/// and what the card renders. Without the key, "React", "ReactJS" and "react.js" are three
/// skills; without the name, a posting renders its requirements in slug case.
/// </para>
/// <para>
/// A skill need not exist in the catalogue. An employer naming something the platform has not
/// catalogued gets a posting that works and a candidate who can still find it by free text —
/// refusing the save would make the catalogue a gate on hiring rather than an aid to it. The
/// skill seeder promotes frequently-named uncatalogued skills separately.
/// </para>
/// </summary>
public sealed class JobSkillRequirement
{
    private JobSkillRequirement(
        string skillKey,
        string displayName,
        SkillProficiency minimumProficiency,
        bool isEssential,
        int order)
    {
        SkillKey = skillKey;
        DisplayName = displayName;
        MinimumProficiency = minimumProficiency;
        IsEssential = isEssential;
        Order = order;
    }

    private JobSkillRequirement() { }

    /// <summary>The normalised match key. Lower-case, hyphenated.</summary>
    public string SkillKey { get; private set; } = string.Empty;

    /// <summary>What the employer typed. What a card renders.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    public SkillProficiency MinimumProficiency { get; private set; }

    /// <summary>Whether the posting treats this as a bar or a preference.</summary>
    public bool IsEssential { get; private set; }

    public int Order { get; private set; }

    /// <summary>
    /// Normalises free text to a skill key, or null when nothing usable survives. Shared with
    /// the candidate profile so both sides of a match are keyed identically.
    /// </summary>
    public static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new System.Text.StringBuilder(value.Length);
        var previousWasDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            // Retained deliberately: '+' and '#' are load-bearing in skill names, and folding
            // them away makes "c#" and "c" the same skill.
            if (char.IsAsciiLetterOrDigit(character) || character is '+' or '#')
            {
                builder.Append(character);
                previousWasDash = false;
                continue;
            }

            if (!previousWasDash && builder.Length > 0)
            {
                builder.Append('-');
                previousWasDash = true;
            }
        }

        var key = builder.ToString().Trim('-');

        return key.Length == 0
            ? null
            : key.Length <= JobLimits.MaxSkillKey ? key : key[..JobLimits.MaxSkillKey].TrimEnd('-');
    }

    internal static JobSkillRequirement? Create(
        string? name,
        SkillProficiency minimumProficiency,
        bool isEssential,
        int order)
    {
        var key = NormalizeKey(name);

        if (key is null)
        {
            return null;
        }

        var display = name!.Trim();

        if (display.Length > JobLimits.MaxDisplayName)
        {
            display = display[..JobLimits.MaxDisplayName].TrimEnd();
        }

        return new JobSkillRequirement(key, display, minimumProficiency, isEssential, order);
    }
}
