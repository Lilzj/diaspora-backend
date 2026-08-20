namespace Diaspora.Modules.Providers.Domain.Enums;

/// <summary>
/// What has been proven about a provider.
/// <para>
/// Every member maps onto a Trust <c>VerificationType</c> that B3 already publishes — B7 adds
/// nothing to Trust and edits nothing in it. The mapping lives in
/// <c>Infrastructure.Trust.ProviderVerificationTypeMap</c> and is the only place the two
/// vocabularies meet.
/// </para>
/// <para>
/// Several kinds rather than one boolean, because the brief requires identity verification,
/// business verification, document verification and certification verification as separate
/// concerns and they genuinely are: a sole trader can prove who they are without having a
/// company, and a company can be registered without its director having passed a face match.
/// The client's single <c>verificationStatus: 'Verified'</c> string cannot say "identity
/// verified, business registration pending", which is the most common real state of a new
/// provider.
/// </para>
/// </summary>
public enum ProviderVerificationKind
{
    /// <summary>The owner is who they say they are. Trust's <c>Identity</c>.</summary>
    Identity = 1,

    /// <summary>The business is registered. Trust's <c>Business</c>.</summary>
    Business = 2,

    /// <summary>The organization is a legitimate community body. Trust's <c>Organization</c>.</summary>
    Organization = 3,

    /// <summary>A professional licence or credential. Trust's <c>Professional</c>.</summary>
    Professional = 4,

    /// <summary>
    /// A supporting document was checked — a lease, an insurance certificate, a food hygiene
    /// rating. Trust's <c>Residence</c> handler covers the address case; others route through
    /// Trust's generic document review.
    /// </summary>
    Document = 5,

    /// <summary>One named <see cref="Providers.ProviderCertification"/> was checked.</summary>
    Certification = 6,

    /// <summary>An embassy or consulate endorsed the provider. Trust's <c>Embassy</c>.</summary>
    Embassy = 7
}
