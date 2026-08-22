namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// What a caller may say about the copy — BG-07.
/// <para>
/// One optional field, and the body itself is optional. See
/// <c>DuplicateHousingListingCommandHandler</c> for what a duplicate carries — and, more
/// importantly, for why the address and the photographs are not among it.
/// </para>
/// </summary>
public sealed record DuplicateHousingListingRequest
{
    /// <summary>A title for the copy. Null takes the source's, prefixed <c>Copy of </c>.</summary>
    public string? Title { get; init; }
}
