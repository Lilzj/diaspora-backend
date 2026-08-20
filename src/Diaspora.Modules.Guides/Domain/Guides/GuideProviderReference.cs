using Diaspora.Modules.Guides.Domain.Policies;

namespace Diaspora.Modules.Guides.Domain.Guides;

/// <summary>
/// One provider a guide points its readers at — B8 as the first consumer of B7's contracts.
/// <para>
/// An identifier and two strings, deliberately: the provider's name, slug and verified badge
/// are hydrated at read time through <c>IProviderDirectory</c>, never copied here, because a
/// provider that renames itself or loses its badge must not be misrepresented by every guide
/// that ever mentioned it. The role is free text ("RecommendedLawyer") rather than an enum —
/// what a guide calls a provider is editorial, not taxonomic.
/// </para>
/// </summary>
public sealed class GuideProviderReference
{
    private GuideProviderReference(Guid providerId, string? role, string? note, int order)
    {
        ProviderId = providerId;
        Role = role;
        Note = note;
        Order = order;
    }

    private GuideProviderReference() { }

    public Guid ProviderId { get; private set; }

    /// <summary>What the guide calls this provider. Editorial, free text.</summary>
    public string? Role { get; private set; }

    /// <summary>Why the guide recommends them, in the author's words.</summary>
    public string? Note { get; private set; }

    public int Order { get; private set; }

    internal static GuideProviderReference Create(
        Guid providerId, string? role, string? note, int order) =>
        new(
            providerId,
            Trim(role, GuideLimits.MaxProviderReferenceRole),
            Trim(note, GuideLimits.MaxProviderReferenceNote),
            order);

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(value.Trim().Length, max)];
}
