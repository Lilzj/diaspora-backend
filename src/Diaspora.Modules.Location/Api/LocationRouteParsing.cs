using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// Query-string parsing shared by the module's endpoint groups.
/// <para>
/// Enums arrive as strings so the wire contract stays readable and stable. An unparseable
/// value falls back rather than throwing: for a filter, null means "no filter", so a malformed
/// parameter widens the query rather than failing it. FluentValidation rejects anything
/// genuinely invalid with a proper problem document.
/// </para>
/// </summary>
internal static class LocationRouteParsing
{
    public static PaginationRequest Paginate(int? page, int? pageSize) =>
        new PaginationRequest
        {
            Page = page ?? PaginationRequest.DefaultPage,
            PageSize = pageSize ?? PaginationRequest.DefaultPageSize
        }.Sanitize();

    public static TEnum? TryParse<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : null;

    /// <summary>
    /// Parses a comma-separated level filter — <c>?levels=City,Neighborhood</c>.
    /// <para>
    /// Unrecognised names are dropped rather than rejected. A client sending a level a newer
    /// server would know about should get the levels it asked for that this one does, not an
    /// error; that is what makes adding a rung a compatible change.
    /// </para>
    /// </summary>
    public static IReadOnlyList<AdministrativeLevel> ParseLevels(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(TryParse<AdministrativeLevel>)
            .Where(level => level is not null)
            .Select(level => level!.Value)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Resolves the radius from either name the frontend uses for it.
    /// <para>
    /// <c>radiusKm</c> is the documented parameter; <c>distanceRadius</c> is a second name for
    /// the same thing that the marketplace filter sends. Accepting both costs one null
    /// coalesce and saves a frontend change. Naming finding #18.
    /// </para>
    /// </summary>
    public static double? ResolveRadius(double? radiusKm, double? distanceRadius) =>
        radiusKm ?? distanceRadius;

    /// <summary>
    /// Resolves the city from either name the frontend uses for it.
    /// <para>
    /// Six call sites send <c>cityName</c> and two send <c>city</c>. Both bind. Naming finding
    /// #14.
    /// </para>
    /// </summary>
    public static string? ResolveCity(string? cityName, string? city) =>
        string.IsNullOrWhiteSpace(cityName) ? city : cityName;
}
