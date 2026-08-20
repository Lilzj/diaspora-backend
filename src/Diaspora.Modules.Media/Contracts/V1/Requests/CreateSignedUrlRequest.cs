namespace Diaspora.Modules.Media.Contracts.V1.Requests;

/// <summary>
/// Asks for a short-lived link to a private asset.
/// <para>
/// The lifetime is not a parameter. A caller that could ask for a week-long link to an
/// immigration document would eventually be a caller that did, and the ceiling is a property
/// of what the file is rather than of who is asking — verification documents get ten minutes,
/// everything else fifteen.
/// </para>
/// </summary>
public sealed record CreateSignedUrlRequest
{
    /// <summary>
    /// Which rendition to open. Defaults to the original.
    /// <para>
    /// The token binds it, so a link to a thumbnail cannot be replayed for the full-size
    /// original — which for a scanned passport is the whole point.
    /// </para>
    /// </summary>
    public string? Variant { get; init; }

    /// <summary>
    /// Restrict the link to one member.
    /// <para>
    /// Strongly recommended and used by the verification review flow. Without it the link
    /// works for whoever holds it, including whoever reads the support ticket it was pasted
    /// into.
    /// </para>
    /// </summary>
    public Guid? AudienceUserId { get; init; }
}
