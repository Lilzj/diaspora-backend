using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Errors;

/// <summary>
/// The errors the module returns, in the same shape as every other module's.
/// <para>
/// Codes come from <see cref="LocationErrorCodes"/> and are frozen; the descriptions beside
/// them are not, and may be reworded without a version bump. That split is what lets a client
/// branch safely on <c>code</c> while the text stays free to improve.
/// </para>
/// </summary>
public static class LocationErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized(LocationErrorCodes.NotAuthenticated, "You must be signed in.");

    public static Error CountryNotFound(string code) =>
        Error.NotFound(
            LocationErrorCodes.CountryNotFound,
            $"No country is registered with the code '{code}'.");

    public static Error CountryNotSupported(string code) =>
        Error.Validation(
            LocationErrorCodes.CountryNotSupported,
            $"Diaspora does not operate in '{code}' yet.");

    public static readonly Error AreaNotFound =
        Error.NotFound(LocationErrorCodes.AreaNotFound, "That place is not in our directory.");

    public static readonly Error NeighborhoodNotFound =
        Error.NotFound(
            LocationErrorCodes.NeighborhoodNotFound,
            "This neighbourhood could not be located in our geographic directory.");

    public static readonly Error ProfileNotFound =
        Error.NotFound(
            LocationErrorCodes.ProfileNotFound, "You do not have a location profile yet.");

    public static readonly Error SavedLocationNotFound =
        Error.NotFound(
            LocationErrorCodes.SavedLocationNotFound, "You have not saved a place with that label.");

    public static readonly Error ExperienceNotFound =
        Error.NotFound(
            LocationErrorCodes.ExperienceNotFound,
            "You have not recorded any time in that country.");

    public static Error ExperienceDuplicate(string countryCode) =>
        Error.Conflict(
            LocationErrorCodes.ExperienceDuplicate,
            $"You have already recorded time in {countryCode}. Amend it instead of adding another.");

    public static readonly Error ExperienceOverlapping =
        Error.Conflict(
            LocationErrorCodes.ExperienceOverlapping,
            "That period overlaps one you have already recorded for this country.");

    public static readonly Error GeocodingUnavailable =
        Error.ServiceUnavailable(
            LocationErrorCodes.GeocodingUnavailable,
            "Location lookup is temporarily unavailable. Choose a place manually.");

    /// <summary>
    /// The active spatial provider cannot answer the question that was asked.
    /// <para>
    /// A real, reportable outcome rather than a silent empty result. A polygon search against
    /// a provider with no geometry support that returned nothing would be indistinguishable
    /// from a polygon containing nothing, and the caller would draw the wrong conclusion.
    /// </para>
    /// </summary>
    public static Error SpatialCapabilityUnsupported(string capability, string provider) =>
        Error.Validation(
            LocationErrorCodes.SpatialCapabilityUnsupported,
            $"The active geospatial provider '{provider}' does not support {capability}.");
}
