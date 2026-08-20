using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// The one place a domain enum becomes a wire string and back.
/// <para>
/// <b>Centralised because a serialised enum is a frozen contract, and a default
/// <c>ToString()</c> is a contract nobody decided on.</b> Every value here is either the name the
/// client already uses or a deliberate choice recorded in the frontend contract report. The two
/// that are neither the enum name nor obvious are called out below.
/// </para>
/// <para>
/// Parsing is case-insensitive on the way in and exact on the way out. A client that sends
/// <c>business</c> where the contract says <c>Business</c> is being sloppy, not wrong, and
/// refusing it buys nothing; emitting two spellings of the same value, on the other hand, breaks
/// every client that compares string literals — which is what the frontend does throughout.
/// </para>
/// </summary>
public static class ProviderWireNames
{
    // ── Provider role: lower-case, matching the client's OrganizationRole union verbatim ─────

    /// <summary>
    /// The client's <c>userRole</c> is <c>'owner' | 'manager' | 'moderator' | 'contributor'</c>,
    /// and it compares those literals directly and keys its role-tab filter on them. Emitting
    /// <c>Owner</c> would break the comparison silently.
    /// </summary>
    public static string ToWire(ProviderRole role) => role switch
    {
        ProviderRole.Owner => "owner",
        ProviderRole.Manager => "manager",
        ProviderRole.Moderator => "moderator",
        ProviderRole.Editor => "editor",
        _ => "contributor"
    };

    public static Result<ProviderRole> ParseRole(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "owner" => Result.Success(ProviderRole.Owner),
        "manager" => Result.Success(ProviderRole.Manager),
        "moderator" => Result.Success(ProviderRole.Moderator),
        "editor" => Result.Success(ProviderRole.Editor),
        "contributor" => Result.Success(ProviderRole.Contributor),
        _ => Result.Failure<ProviderRole>(ProviderErrors.EnumUnknown)
    };

    // ── Everything else: the enum name, which is what the platform's other modules emit ──────

    public static string ToWire(ProviderType type) => type.ToString();

    public static string ToWire(ProviderStatus status) => status.ToString();

    public static string ToWire(ProviderVisibilityLevel level) => level.ToString();

    public static string ToWire(ProviderVerificationKind kind) => kind.ToString();

    public static string ToWire(ProviderVerificationState state) => state.ToString();

    public static string ToWire(ServiceDeliveryMode mode) => mode.ToString();

    public static string ToWire(ServiceAreaKind kind) => kind.ToString();

    public static string ToWire(PricingModel model) => model.ToString();

    public static string ToWire(ContactMethodKind kind) => kind.ToString();

    public static string ToWire(AvailabilityKind kind) => kind.ToString();

    public static string ToWire(AvailabilityExceptionKind kind) => kind.ToString();

    public static string ToWire(CertificationStatus status) => status.ToString();

    public static string ToWire(RecommendationKind kind) => kind.ToString();

    public static string ToWire(ReviewStatus status) => status.ToString();

    public static string ToWire(LanguageProficiency proficiency) => proficiency.ToString();

    public static string ToWire(ProviderSortOrder sort) => sort.ToString();

    /// <summary>
    /// The day of week, as the client's <c>businessHours[].dayOfWeek</c> spells it — "Monday".
    /// The BCL name and the client's string happen to agree, which is worth stating so nobody
    /// "helpfully" switches to a numeric day later.
    /// </summary>
    public static string ToWire(DayOfWeek day) => day.ToString();

    public static Result<ProviderType> ParseProviderType(string? value) =>
        Parse<ProviderType>(value);

    public static Result<ProviderStatus> ParseStatus(string? value) =>
        Parse<ProviderStatus>(value);

    public static Result<ProviderVisibilityLevel> ParseVisibility(string? value) =>
        Parse<ProviderVisibilityLevel>(value);

    public static Result<ProviderVerificationKind> ParseVerificationKind(string? value) =>
        Parse<ProviderVerificationKind>(value);

    public static Result<ServiceDeliveryMode> ParseDeliveryMode(string? value) =>
        Parse<ServiceDeliveryMode>(value);

    public static Result<ServiceAreaKind> ParseServiceAreaKind(string? value) =>
        Parse<ServiceAreaKind>(value);

    public static Result<PricingModel> ParsePricingModel(string? value) =>
        Parse<PricingModel>(value);

    public static Result<ContactMethodKind> ParseContactMethodKind(string? value) =>
        Parse<ContactMethodKind>(value);

    public static Result<AvailabilityKind> ParseAvailabilityKind(string? value) =>
        Parse<AvailabilityKind>(value);

    public static Result<AvailabilityExceptionKind> ParseAvailabilityExceptionKind(string? value) =>
        Parse<AvailabilityExceptionKind>(value);

    public static Result<RecommendationKind> ParseRecommendationKind(string? value) =>
        Parse<RecommendationKind>(value);

    public static Result<LanguageProficiency> ParseProficiency(string? value) =>
        Parse<LanguageProficiency>(value);

    public static Result<DayOfWeek> ParseDayOfWeek(string? value) => Parse<DayOfWeek>(value);

    /// <summary>
    /// The client's filter sheet sends <c>default</c>, <c>distance</c> and <c>rating</c>. Those
    /// three are accepted alongside the contract names so the existing screen keeps working
    /// while it migrates.
    /// </summary>
    public static Result<ProviderSortOrder> ParseSort(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            null or "" or "default" or "recommended" => Result.Success(ProviderSortOrder.Recommended),
            "distance" => Result.Success(ProviderSortOrder.Distance),
            "rating" => Result.Success(ProviderSortOrder.Rating),
            "newest" => Result.Success(ProviderSortOrder.Newest),
            "mostreviewed" or "most-reviewed" => Result.Success(ProviderSortOrder.MostReviewed),
            "name" => Result.Success(ProviderSortOrder.Name),
            _ => Result.Failure<ProviderSortOrder>(ProviderErrors.SortUnknown)
        };

    private static Result<TEnum> Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? Result.Success(parsed)
            : Result.Failure<TEnum>(ProviderErrors.EnumUnknown);
}
