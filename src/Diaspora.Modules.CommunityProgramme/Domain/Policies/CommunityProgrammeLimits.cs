namespace Diaspora.Modules.CommunityProgramme.Domain.Policies;

/// <summary>
/// The module's field and collection bounds, in one place.
/// <para>
/// Every one of these is a persistence bound as well as a validation bound: the EF configuration
/// reads the same constants, so a validator and a column can never disagree about how long a title
/// may be. A mismatch between the two is a request that passes validation and dies in the database
/// with a message nobody can act on.
/// </para>
/// </summary>
public static class CommunityProgrammeLimits
{
    public const int MaxNameLength = 160;
    public const int MaxSummaryLength = 600;
    public const int MaxDescriptionLength = 4000;
    public const int MaxLabelLength = 160;
    public const int MaxReasonLength = 1000;
    public const int MaxIconLength = 64;
    public const int MaxRouteLength = 512;
    public const int MaxSkillLength = 80;
    public const int MaxAnswerKeyLength = 80;
    public const int MaxAnswerValueLength = 2000;
    public const int MaxOpenResponseLength = 2000;
    public const int MaxCurrencyLength = 3;
    public const int MaxCountryLength = 2;
    public const int MaxIdempotencyKeyLength = 200;
    public const int MaxMediaReferences = 10;

    /// <summary>A programme has few tiers. More than this is a catalogue, not a membership.</summary>
    public const int MaxTiers = 20;

    public const int MaxBenefitsPerTier = 30;
    public const int MaxRequirementsPerTier = 20;
    public const int MaxShiftsPerOpportunity = 60;
    public const int MaxSkillsPerOpportunity = 30;
    public const int MaxPollOptions = 30;
    public const int MaxAnswers = 30;

    /// <summary>
    /// How long past its renewal date a membership keeps its benefits.
    /// <para>
    /// Fourteen days, and the number is a product decision rather than an arbitrary one: a member
    /// whose card expired while they were away has not left the organization. The state that carries
    /// it — <c>RenewalDue</c> — grants benefits precisely so no screen has to decide this for itself.
    /// </para>
    /// </summary>
    public const int RenewalGraceDays = 14;

    /// <summary>The largest page any of this module's cursor reads will serve.</summary>
    public const int MaxPageSize = 50;

    public const int DefaultPageSize = 20;

    /// <summary>Clamps a caller's requested page size into the range the module will serve.</summary>
    public static int PageSize(int? requested) =>
        requested is null or < 1 ? DefaultPageSize : Math.Min(requested.Value, MaxPageSize);
}
