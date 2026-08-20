using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What an existing household is looking for in whoever takes the spare room.
/// <para>
/// <b>Only meaningful on shared accommodation, and the aggregate refuses it anywhere else.</b> A
/// household choosing who they will share a kitchen with is an ordinary and largely lawful thing;
/// the same restriction on a whole flat is housing discrimination in most of the jurisdictions
/// this platform serves. The distinction is enforced rather than documented, in
/// <c>HousingListing.SetRoommatePreferences</c>.
/// </para>
/// <para>
/// <b>Nationality preference is stored as a flag the platform can turn off, and it defaults to
/// off.</b> The brief asks for it as future policy-configurable, and this is what that means in
/// practice: the field exists so a deployment operating where such a preference is lawful and
/// customary can enable it, and every other deployment silently drops it. Housing does not decide
/// which those are — <c>HousingOptions.AllowNationalityPreference</c> does, and it is false unless
/// an operator turns it on deliberately.
/// </para>
/// <para>
/// <b>Languages are what actually help.</b> "Speaks Yoruba" is the single most useful thing on
/// this whole type for the members this platform exists for, and it is a preference nobody is
/// harmed by.
/// </para>
/// </summary>
public sealed class RoommatePreferences : ValueObject
{
    private readonly List<string> _languages = [];

    private RoommatePreferences(
        GenderPreference genderPreference,
        RoommateOccupation occupation,
        int? minimumAge,
        int? maximumAge,
        SmokingPolicy smokingPolicy,
        PetPolicy petPolicy,
        NoiseTolerance noiseTolerance,
        string? nationalityPreference,
        IEnumerable<string> languages)
    {
        GenderPreference = genderPreference;
        Occupation = occupation;
        MinimumAge = minimumAge;
        MaximumAge = maximumAge;
        SmokingPolicy = smokingPolicy;
        PetPolicy = petPolicy;
        NoiseTolerance = noiseTolerance;
        NationalityPreference = nationalityPreference;
        _languages = [.. languages];
    }

    private RoommatePreferences() { }

    public GenderPreference GenderPreference { get; private set; }

    public RoommateOccupation Occupation { get; private set; }

    public int? MinimumAge { get; private set; }

    public int? MaximumAge { get; private set; }

    public SmokingPolicy SmokingPolicy { get; private set; }

    public PetPolicy PetPolicy { get; private set; }

    public NoiseTolerance NoiseTolerance { get; private set; }

    /// <summary>
    /// A nationality the household would prefer. Null on every deployment that has not
    /// deliberately enabled it — see the class summary.
    /// </summary>
    public string? NationalityPreference { get; private set; }

    /// <summary>
    /// Languages spoken in the household, comma-joined for storage.
    /// <para>
    /// A single delimited column rather than an owned collection, and deliberately: this is a
    /// short list read whole and never queried by element, and a second table for it would add a
    /// join to every listing read to hold at most ten short strings.
    /// </para>
    /// </summary>
    public string? LanguagesCsv { get; private set; }

    /// <summary>The languages, parsed. Not mapped — projected from <see cref="LanguagesCsv"/>.</summary>
    public IReadOnlyList<string> Languages =>
        _languages.Count > 0
            ? _languages
            : LanguagesCsv is { Length: > 0 } csv
                ? csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : [];

    /// <summary>Whether the household expressed any preference at all.</summary>
    public bool IsEmpty =>
        GenderPreference == GenderPreference.Any
        && Occupation == RoommateOccupation.Any
        && MinimumAge is null
        && MaximumAge is null
        && SmokingPolicy == SmokingPolicy.Unspecified
        && PetPolicy == PetPolicy.Unspecified
        && NoiseTolerance == NoiseTolerance.Unspecified
        && NationalityPreference is null
        && Languages.Count == 0;

    /// <summary>
    /// Whether the only thing expressed is that the household should be a family.
    /// <para>
    /// <b>The one preference on this type that describes the letting rather than a roommate, and
    /// therefore the one a whole property may carry.</b> "Family let" is an ordinary condition on a
    /// whole flat in every market this platform serves; "no smokers over 40 who speak Yoruba" is a
    /// statement about somebody you would be sharing a kitchen with, and means nothing on a
    /// property nobody is sharing. <c>HousingListing.SetRoommatePreferences</c> draws the line
    /// here.
    /// </para>
    /// </summary>
    public bool IsHouseholdCompositionOnly =>
        GenderPreference is GenderPreference.Any or GenderPreference.FamilyOnly
        && Occupation == RoommateOccupation.Any
        && MinimumAge is null
        && MaximumAge is null
        && SmokingPolicy == SmokingPolicy.Unspecified
        && PetPolicy == PetPolicy.Unspecified
        && NoiseTolerance == NoiseTolerance.Unspecified
        && NationalityPreference is null
        && Languages.Count == 0;

    /// <summary>
    /// No preference at all. A fresh instance per access: this is an owned type, and a shared
    /// static one makes EF write nulls into a second aggregate holding it.
    /// </summary>
    public static RoommatePreferences None =>
        new(GenderPreference.Any,
            RoommateOccupation.Any,
            null,
            null,
            SmokingPolicy.Unspecified,
            PetPolicy.Unspecified,
            NoiseTolerance.Unspecified,
            null,
            []);

    /// <param name="allowNationalityPreference">
    /// The operator's decision, passed in rather than read here so the domain stays free of
    /// configuration. False on every deployment that has not deliberately enabled it, and a
    /// supplied nationality is then dropped rather than refused — a landlord should not get a
    /// validation error for a field their client showed them.
    /// </param>
    public static Result<RoommatePreferences> Create(
        GenderPreference genderPreference = GenderPreference.Any,
        RoommateOccupation occupation = RoommateOccupation.Any,
        int? minimumAge = null,
        int? maximumAge = null,
        SmokingPolicy smokingPolicy = SmokingPolicy.Unspecified,
        PetPolicy petPolicy = PetPolicy.Unspecified,
        NoiseTolerance noiseTolerance = NoiseTolerance.Unspecified,
        string? nationalityPreference = null,
        IReadOnlyCollection<string>? languages = null,
        bool allowNationalityPreference = false)
    {
        if (minimumAge is { } floor
            && (floor < HousingLimits.MinRoommateAge || floor > HousingLimits.MaxRoommateAge))
        {
            return Result.Failure<RoommatePreferences>(HousingErrors.OccupancyInvalid);
        }

        if (maximumAge is { } ceiling
            && (ceiling < HousingLimits.MinRoommateAge || ceiling > HousingLimits.MaxRoommateAge))
        {
            return Result.Failure<RoommatePreferences>(HousingErrors.OccupancyInvalid);
        }

        if (minimumAge is { } from && maximumAge is { } until && until < from)
        {
            return Result.Failure<RoommatePreferences>(HousingErrors.OccupancyInvalid);
        }

        var spoken = Normalize(languages);

        if (spoken.Count > HousingLimits.MaxRoommateLanguages)
        {
            return Result.Failure<RoommatePreferences>(HousingErrors.RoommateLanguageLimit);
        }

        var nationality = allowNationalityPreference
            ? Clamp(nationalityPreference, HousingLimits.MaxDisplayName)
            : null;

        var preferences = new RoommatePreferences(
            genderPreference,
            occupation,
            minimumAge,
            maximumAge,
            smokingPolicy,
            petPolicy,
            noiseTolerance,
            nationality,
            spoken)
        {
            LanguagesCsv = spoken.Count == 0 ? null : string.Join(',', spoken)
        };

        return Result.Success(preferences);
    }

    private static List<string> Normalize(IReadOnlyCollection<string>? languages)
    {
        if (languages is null || languages.Count == 0)
        {
            return [];
        }

        return [.. languages
            .Select(language => Clamp(language, HousingLimits.MaxLanguageName))
            .OfType<string>()
            // A comma would split one language into two on the way back out.
            .Select(language => language.Replace(',', ' ').Trim())
            .Where(language => language.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? trimmed[..maximum] : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return GenderPreference;
        yield return Occupation;
        yield return MinimumAge;
        yield return MaximumAge;
        yield return SmokingPolicy;
        yield return PetPolicy;
        yield return NoiseTolerance;
        yield return NationalityPreference;
        yield return LanguagesCsv;
    }
}
