using Diaspora.Modules.Media.Domain.Enums;

namespace Diaspora.Modules.Media.Extensibility.Access;

/// <summary>
/// A vertical's answer to "may this member read that file?".
/// <para>
/// <b>The seam that stops Media becoming a second authorisation system.</b> Media knows an
/// asset belongs to <c>HousingListing 7f3c…</c> and nothing else about housing listings. It
/// cannot know that the listing is unpublished, that its owner blocked this member, or that
/// the member's booking gives them access to the floor plan. Every one of those is Housing's
/// knowledge, and encoding any of it here would mean Media had to change every time a vertical
/// changed its rules.
/// </para>
/// <para>
/// So the read path asks. Media decides what it owns — ownership, explicit grants, visibility,
/// scan status, quarantine — and then hands the remaining question to whichever policy claims
/// the owner type. A vertical that registers no policy gets Media's default, which is what
/// makes the seam optional rather than a burden on every consumer.
/// </para>
/// <para>
/// <b>Deny wins.</b> Media's own checks run first and a refusal there is final — a policy
/// cannot grant access to a quarantined file. A policy can only narrow.
/// </para>
/// <para>
/// <b>Implementing one.</b> Put it in your module's assembly; Media scans every
/// <c>Diaspora.*</c> assembly for implementations exactly as Location scans for nearby
/// providers. Media never learns your module exists.
/// </para>
/// </summary>
public interface IMediaAccessPolicy
{
    /// <summary>
    /// Which owner type this policy speaks for. One policy per type; two claiming the same
    /// type fails at startup rather than resolving by assembly load order.
    /// </summary>
    MediaOwnerType OwnerType { get; }

    /// <summary>
    /// Whether the caller may perform <paramref name="request"/>.
    /// <para>
    /// Called on the read path of every non-public asset owned by this type, so it should be a
    /// cheap lookup. A policy that needs three joins will be felt on every image in a gallery.
    /// </para>
    /// </summary>
    Task<MediaAccessDecision> EvaluateAsync(
        MediaAccessRequest request, CancellationToken cancellationToken = default);
}
