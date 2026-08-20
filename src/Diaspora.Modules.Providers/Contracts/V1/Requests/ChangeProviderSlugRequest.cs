namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Changes a provider's deep-link handle.
/// <para>
/// Its own endpoint because it breaks links. Every printed flyer, WhatsApp share and search
/// result pointing at the old handle stops working, and that has to be a decision somebody made
/// rather than a side effect of renaming a business.
/// </para>
/// </summary>
public sealed record ChangeProviderSlugRequest
{
    /// <summary>Lower-case letters, digits and single hyphens.</summary>
    public required string Slug { get; init; }
}
