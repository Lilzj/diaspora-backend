namespace Diaspora.Modules.Guides.Infrastructure.Trust;

/// <summary>
/// The one place B8.2's verification vocabulary meets Trust's.
/// <para>
/// <b>Trust already had a type for this, and it needed no change to B3 at all.</b> Trust's
/// <c>VerificationType</c> catalogue names <c>Embassy</c> — "embassy or consulate endorsement" —
/// whose handler accepts <c>GovernmentImmigrationDocuments</c>, <c>OrganizationDocument</c> and
/// <c>Other</c>, requires a document, and carries a ten-day review SLA. That is exactly a claim that
/// an institution is official, backed by paperwork somebody has to read. B8 found the same thing with
/// <c>CommunityGuide</c>; twice now, the deepest cross-module integration of a phase has cost zero
/// lines in Trust.
/// </para>
/// <para>
/// <b>The rejected alternative was <c>Organization</c></b> ("a community organisation's
/// legitimacy"). It is the right shape and the wrong meaning: an organization verification says a
/// group of members is genuinely the association it claims to be, and an immigration department is
/// not that. Using it would put ministries and community associations under one badge whose wording
/// is true of only one of them.
/// </para>
/// <para>
/// Strings on both sides rather than a reference to Trust's enum: Trust's published events carry the
/// type as a string precisely so a consumer need not reference the module that raised them, so the
/// handler side works on strings anyway. One vocabulary rather than two that can drift — and B8.2
/// keeps the architecture-tested independence from <c>Diaspora.Modules.Trust</c>.
/// </para>
/// </summary>
public static class AuthorityVerificationTypeMap
{
    /// <summary>The Trust <c>VerificationType</c> name an authority claim submits as.</summary>
    public const string AuthorityVerificationType = "Embassy";

    /// <summary>
    /// The Trust <c>VerificationDocumentType</c> name B8.2 stamps on untyped evidence.
    /// <para>
    /// Untyped evidence is refused before a reviewer ever sees it — the B3.0.2 trap — and
    /// <c>GovernmentImmigrationDocuments</c> is what the <c>Embassy</c> handler accepts for exactly
    /// this kind of proof. A caller that knows better supplies its own type and this never applies.
    /// </para>
    /// </summary>
    public const string DefaultEvidenceDocumentType = "GovernmentImmigrationDocuments";

    /// <summary>
    /// Whether a Trust outcome event is about an authority claim.
    /// <para>
    /// False is the common case and not an error: Trust raises verification events for phone
    /// possession, business registration and diaspora eligibility several times a minute in a healthy
    /// system, and a handler that treated an unmapped type as a failure would log noise continuously.
    /// </para>
    /// <para>
    /// <b>This filter is necessary but not sufficient, and the distinction matters.</b> B8.2 and any
    /// other consumer of <c>Embassy</c> verifications share the type name, so a matching type only
    /// means "possibly ours". What decides is whether the case identifier resolves to a row in this
    /// module's registers — which is why both projections look the subject up rather than trusting
    /// the type.
    /// </para>
    /// </summary>
    public static bool IsAuthorityVerification(string? verificationType) =>
        string.Equals(
            verificationType?.Trim(),
            AuthorityVerificationType,
            StringComparison.OrdinalIgnoreCase);
}
