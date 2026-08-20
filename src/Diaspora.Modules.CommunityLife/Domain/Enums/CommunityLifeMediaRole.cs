namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What a referenced Media asset is for.
/// <para>
/// Shared by both aggregates rather than split in two, because the roles genuinely coincide — an
/// organization and an event each have exactly one cover and one logo and any number of gallery
/// images — and two enums would drift the first time somebody added a role to one of them.
/// </para>
/// </summary>
public enum CommunityLifeMediaRole
{
    /// <summary>The single banner image. One per subject; attaching a second replaces the first.</summary>
    Cover = 0,

    /// <summary>The mark. One per subject.</summary>
    Logo = 1,

    /// <summary>Photographs. Ordered.</summary>
    Gallery = 2,

    /// <summary>A moving image.</summary>
    Video = 3,

    /// <summary>A brochure, a programme, a flyer.</summary>
    Brochure = 4,

    /// <summary>A constitution, a certificate, a risk assessment.</summary>
    Document = 5,

    /// <summary>Somebody billed to speak.</summary>
    SpeakerPhoto = 6,

    /// <summary>Evidence submitted with a verification claim. Never rendered publicly.</summary>
    VerificationEvidence = 7
}
