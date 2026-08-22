namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// What a caller may say about the copy — BG-07.
/// <para>
/// One optional field, and the body itself is optional. See
/// <c>DuplicateListingCommandHandler</c> for what a duplicate carries and what it resets.
/// </para>
/// </summary>
public sealed record DuplicateListingRequest
{
    /// <summary>A title for the copy. Null takes the source's, prefixed <c>Copy of </c>.</summary>
    public string? Title { get; init; }
}
