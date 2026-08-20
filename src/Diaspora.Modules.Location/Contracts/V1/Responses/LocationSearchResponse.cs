namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// Autocomplete results for a typed place name.
/// <para>
/// Returns <see cref="LocationReferenceResponse"/> rather than a bespoke search DTO,
/// deliberately: what the selector does with a result is set it as the current location, and
/// a result that is already the shape a location has to be saves the client a mapping step it
/// would otherwise write by hand and get subtly wrong.
/// </para>
/// </summary>
public sealed record LocationSearchResponse
{
    /// <summary>The term as the server interpreted it, after trimming and capping.</summary>
    public required string Term { get; init; }

    /// <summary>Best match first.</summary>
    public required IReadOnlyList<LocationReferenceResponse> Results { get; init; }
}
