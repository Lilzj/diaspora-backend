namespace Diaspora.Modules.Guides.Infrastructure.Trust;

/// <summary>
/// The one place B8's verification vocabulary meets Trust's.
/// <para>
/// <b>Trust already had a type for this, and finding it was the one open question in the plan.</b>
/// Trust's <c>VerificationType</c> catalogue names <c>CommunityGuide</c> — "community guide
/// eligibility" — whose handler requires no document, accepts
/// <c>VerificationDocumentType.GuideEvidence</c> and <c>Other</c>, and carries a seven-day review
/// SLA. That is precisely a claim about guide content backed by optional sourcing, so B8 submits
/// as <c>CommunityGuide</c> and adds nothing to B3.
/// </para>
/// <para>
/// The rejected alternative was <c>Contributor</c> ("the right to publish guides"). It is about
/// the <em>person</em> and is what grants an author standing — Trust raises it, B8's author
/// standing projector consumes it — so submitting a guide under it would conflate "this document
/// is accurate" with "this member may publish", and the badge on the guide would then be a badge
/// about its author.
/// </para>
/// <para>
/// Strings on both sides rather than a reference to Trust's enum: Trust's published events carry
/// <c>VerificationType</c> as a string precisely so a consumer need not reference the module that
/// raised them, so the handler side works on strings anyway. One vocabulary rather than two that
/// can drift — and B8 keeps its architecture-tested independence from
/// <c>Diaspora.Modules.Trust</c>.
/// </para>
/// </summary>
public static class GuideVerificationTypeMap
{
    /// <summary>The Trust <c>VerificationType</c> name a guide accuracy claim submits as.</summary>
    public const string GuideVerificationType = "CommunityGuide";

    /// <summary>
    /// The Trust <c>VerificationDocumentType</c> name B8 stamps on untyped evidence.
    /// <para>
    /// Untyped evidence is refused before a reviewer ever sees it (the B3.0.2 trap), and
    /// <c>GuideEvidence</c> is what the <c>CommunityGuide</c> handler accepts. A caller that knows
    /// better — a practising certificate, a government circular — supplies its own type and this
    /// default never applies.
    /// </para>
    /// </summary>
    public const string DefaultEvidenceDocumentType = "GuideEvidence";

    /// <summary>
    /// Whether a Trust outcome event is about a guide claim.
    /// <para>
    /// False is the common case and not an error: Trust raises verification events for phone
    /// possession, business registration and diaspora eligibility several times a minute in a
    /// healthy system, and a handler that treated an unmapped type as a failure would log noise
    /// continuously. The guide the event concerns is found by verification identifier regardless;
    /// this is the cheap filter that runs first.
    /// </para>
    /// </summary>
    public static bool IsGuideVerification(string? verificationType) =>
        string.Equals(
            verificationType?.Trim(), GuideVerificationType, StringComparison.OrdinalIgnoreCase);
}
