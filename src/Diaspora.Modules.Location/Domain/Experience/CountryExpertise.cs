using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Experience;

/// <summary>
/// A subject a member claims to know well in a particular country — housing, visas,
/// schooling, healthcare.
/// <para>
/// The frontend already collects these: <c>expertiseAreas: string[]</c> on the country
/// verification wizard and on <c>CountryVerificationSummary</c>. They arrive as free strings
/// and stay free strings, deliberately. A closed enum of expertise areas would have to be
/// guessed today and would be wrong for the first country that has a category nobody in this
/// repository has thought of; the topic taxonomy belongs to whichever module eventually owns
/// content categories, not to geography.
/// </para>
/// <para>
/// What this type adds over the bare string is normalisation and a confidence level, so
/// "Housing", "housing" and " Housing " are one claim rather than three.
/// </para>
/// </summary>
public sealed class CountryExpertise : Entity<CountryExpertiseId>
{
    public const int MaxAreaLength = 60;

    public CountryExperienceId ExperienceId { get; private set; } = default!;

    /// <summary>The member's own capitalisation, preserved. Compared case-insensitively.</summary>
    public string Area { get; private set; } = default!;

    public FamiliarityLevel Confidence { get; private set; }

    public DateTimeOffset DeclaredAt { get; private set; }

    private CountryExpertise() { }

    private CountryExpertise(
        CountryExpertiseId id,
        CountryExperienceId experienceId,
        string area,
        FamiliarityLevel confidence,
        DateTimeOffset declaredAt) : base(id)
    {
        ExperienceId = experienceId;
        Area = area;
        Confidence = confidence;
        DeclaredAt = declaredAt;
    }

    internal static CountryExpertise Create(
        CountryExperienceId experienceId,
        string area,
        FamiliarityLevel confidence,
        DateTimeOffset declaredAt)
    {
        ArgumentNullException.ThrowIfNull(experienceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(area);

        var trimmed = area.Trim();

        return new CountryExpertise(
            CountryExpertiseId.New(),
            experienceId,
            trimmed.Length <= MaxAreaLength ? trimmed : trimmed[..MaxAreaLength],
            confidence,
            declaredAt);
    }

    internal void SetConfidence(FamiliarityLevel confidence) => Confidence = confidence;

    internal bool IsSameArea(string area) =>
        string.Equals(Area, area?.Trim(), StringComparison.OrdinalIgnoreCase);
}
