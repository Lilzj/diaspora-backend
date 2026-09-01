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
/// <b>Deny wins, and a grant reaches exactly one thing.</b> A denial is final. A grant widens a
/// <em>read</em> that Media would have refused on visibility alone — which is the case this seam
/// exists for, because a listing photograph is private to its uploader until the vertical says
/// the listing is something the caller may look at.
/// </para>
/// <para>
/// A grant cannot reach anything else. The state floor — deleted, quarantined, unscanned — has
/// already run and is not revisited, so no policy can serve a quarantined file; and management is
/// gated on ownership before a policy is consulted, so none can hand a member someone else's file
/// to edit.
/// </para>
/// <para>
/// <b>Prefer abstaining to denying.</b> A denial removes access the uploader would otherwise have
/// had — including their own file. Unless the vertical genuinely means "not even the owner", the
/// honest answer for a case it does not want to permit is <c>Abstain</c>, which leaves Media's
/// decision standing.
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
