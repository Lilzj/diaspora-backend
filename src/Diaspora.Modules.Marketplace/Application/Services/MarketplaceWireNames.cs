using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Translates between the module's enums and the strings on the wire.
/// <para>
/// <b>Every enum crosses the boundary as its name, never as its number.</b> A contract that sent
/// integers would break the moment somebody inserted a value in the middle of an enum, and would
/// be unreadable in a log, a support ticket or a database query window. Parsing is
/// case-insensitive so a client sending <c>fixedprice</c> is not punished for it; formatting is
/// exact, because the formatted name is the contract.
/// </para>
/// <para>
/// A static class with no state, deliberately. B6.0.1 records why: a stateless type registered in
/// the container is an entry nobody resolves, and CA1822 in Release is what pushes it static in
/// the first place.
/// </para>
/// </summary>
internal static class MarketplaceWireNames
{
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
        value.ToString();

    public static ListingType? ParseListingType(string? value) => Parse<ListingType>(value);

    public static ProductCondition? ParseCondition(string? value) =>
        Parse<ProductCondition>(value);

    public static ListingVisibility? ParseVisibility(string? value) =>
        Parse<ListingVisibility>(value);

    public static ListingMediaRole? ParseMediaRole(string? value) =>
        Parse<ListingMediaRole>(value);

    public static ContactPreference? ParseContactPreference(string? value) =>
        Parse<ContactPreference>(value);

    public static SellerKind? ParseSellerKind(string? value) => Parse<SellerKind>(value);

    public static ListingSortOrder? ParseSortOrder(string? value) =>
        Parse<ListingSortOrder>(value);

    public static ListingReportReason? ParseReportReason(string? value) =>
        Parse<ListingReportReason>(value);

    public static ListingReportStatus? ParseReportStatus(string? value) =>
        Parse<ListingReportStatus>(value);

    public static PromotionKind? ParsePromotionKind(string? value) => Parse<PromotionKind>(value);

    public static ListingStatus? ParseListingStatus(string? value) => Parse<ListingStatus>(value);

    /// <summary>
    /// The generic parse every method above delegates to. Returns null rather than throwing,
    /// because an unrecognised value on the wire is a validation failure the handler reports with
    /// a specific error, not an exception the pipeline turns into a 500.
    /// </summary>
    private static TEnum? Parse<TEnum>(string? value) where TEnum : struct, Enum =>
        !string.IsNullOrWhiteSpace(value)
        && Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? parsed
            : null;
}
