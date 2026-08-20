using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What it is like to live somewhere, in prose.
/// <para>
/// <b>These are living summaries and not facts, which is why they are one value object rather
/// than six columns on the profile.</b> They are written by editors, revised as places change,
/// and read together — a member scanning a neighbourhood page reads the safety paragraph in the
/// light of the transport one. Replacing them as a unit means a profile is never half-updated,
/// and means the "last reviewed" stamp on the profile is honest about all six at once.
/// </para>
/// <para>
/// <b>Every field is optional and the empty narrative is a legitimate state.</b> Most
/// neighbourhoods will have a profile long before anybody writes about them — the profile exists
/// to hang insights and listings off, and demanding six paragraphs to create one would mean the
/// platform has profiles only for the places somebody already cared about.
/// </para>
/// <para>
/// B4 also carries a free-text <c>Overview</c> on the area itself, and the two do not collide.
/// B4's is a sentence about the <em>place</em> rendered on the location hub; this is a relocation
/// brief about <em>living there</em>. Housing reads B4's and never writes it.
/// </para>
/// </summary>
public sealed class NeighborhoodNarrative : ValueObject
{
    private NeighborhoodNarrative(
        string? description,
        string? overview,
        string? safetySummary,
        string? transportSummary,
        string? costOfLivingSummary,
        string? communitySummary,
        string? climateNotes)
    {
        Description = description;
        Overview = overview;
        SafetySummary = safetySummary;
        TransportSummary = transportSummary;
        CostOfLivingSummary = costOfLivingSummary;
        CommunitySummary = communitySummary;
        ClimateNotes = climateNotes;
    }

    private NeighborhoodNarrative() { }

    /// <summary>One or two sentences. What a card and a search result render.</summary>
    public string? Description { get; private set; }

    /// <summary>The long read. What the neighbourhood page opens with.</summary>
    public string? Overview { get; private set; }

    /// <summary>
    /// How safe it feels and in what way. Prose rather than a score, because "fine in the day,
    /// walk the main road at night" is the answer and no number carries it.
    /// </summary>
    public string? SafetySummary { get; private set; }

    public string? TransportSummary { get; private set; }

    public string? CostOfLivingSummary { get; private set; }

    /// <summary>
    /// Who lives there and what community exists. The paragraph a diaspora member reads first,
    /// and the reason this module is worth building.
    /// </summary>
    public string? CommunitySummary { get; private set; }

    public string? ClimateNotes { get; private set; }

    /// <summary>Whether anybody has written anything at all.</summary>
    public bool IsEmpty =>
        Description is null
        && Overview is null
        && SafetySummary is null
        && TransportSummary is null
        && CostOfLivingSummary is null
        && CommunitySummary is null
        && ClimateNotes is null;

    /// <summary>How many of the seven summaries have been written. Drives the completeness figure.</summary>
    public int WrittenCount =>
        new[]
        {
            Description, Overview, SafetySummary, TransportSummary,
            CostOfLivingSummary, CommunitySummary, ClimateNotes
        }.Count(summary => summary is not null);

    /// <summary>
    /// Nothing written yet. A fresh instance per access: this is an owned type, and a shared
    /// static one makes EF write nulls into a second aggregate holding it.
    /// </summary>
    public static NeighborhoodNarrative Empty =>
        new(null, null, null, null, null, null, null);

    public static Result<NeighborhoodNarrative> Create(
        string? description = null,
        string? overview = null,
        string? safetySummary = null,
        string? transportSummary = null,
        string? costOfLivingSummary = null,
        string? communitySummary = null,
        string? climateNotes = null)
    {
        var trimmedDescription = Clamp(description, HousingLimits.MaxNeighborhoodSummary);
        var trimmedOverview = Clamp(overview, HousingLimits.MaxNeighborhoodOverview);
        var trimmedSafety = Clamp(safetySummary, HousingLimits.MaxNeighborhoodSummary);
        var trimmedTransport = Clamp(transportSummary, HousingLimits.MaxNeighborhoodSummary);
        var trimmedCost = Clamp(costOfLivingSummary, HousingLimits.MaxNeighborhoodSummary);
        var trimmedCommunity = Clamp(communitySummary, HousingLimits.MaxNeighborhoodSummary);
        var trimmedClimate = Clamp(climateNotes, HousingLimits.MaxNeighborhoodSummary);

        // Over-length is refused rather than truncated. An editor's paragraph cut mid-sentence is
        // published prose nobody wrote, and it would be published under the platform's name.
        if (TooLong(description, trimmedDescription)
            || TooLong(overview, trimmedOverview)
            || TooLong(safetySummary, trimmedSafety)
            || TooLong(transportSummary, trimmedTransport)
            || TooLong(costOfLivingSummary, trimmedCost)
            || TooLong(communitySummary, trimmedCommunity)
            || TooLong(climateNotes, trimmedClimate))
        {
            return Result.Failure<NeighborhoodNarrative>(
                HousingErrors.NeighborhoodSummaryInvalid);
        }

        return Result.Success(new NeighborhoodNarrative(
            trimmedDescription,
            trimmedOverview,
            trimmedSafety,
            trimmedTransport,
            trimmedCost,
            trimmedCommunity,
            trimmedClimate));
    }

    private static bool TooLong(string? supplied, string? accepted) =>
        accepted is null && !string.IsNullOrWhiteSpace(supplied);

    private static string? Clamp(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > maximum ? null : trimmed;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Description;
        yield return Overview;
        yield return SafetySummary;
        yield return TransportSummary;
        yield return CostOfLivingSummary;
        yield return CommunitySummary;
        yield return ClimateNotes;
    }
}
