using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Infrastructure.Trust;

/// <summary>
/// The one place B7's verification vocabulary meets Trust's.
/// <para>
/// <b>Every kind B7 models already exists in Trust's <c>VerificationType</c>.</b> That is worth
/// stating plainly, because it is the evidence for B7's central claim about verification: this
/// module added nothing to B3 and edited nothing in it. <c>Business</c>, <c>Organization</c>,
/// <c>Professional</c> and <c>Embassy</c> were named in Trust's enum in B3; <c>Identity</c> and
/// <c>Residence</c> likewise.
/// </para>
/// <para>
/// Strings rather than a direct enum reference, deliberately. Trust's published integration
/// events carry <c>VerificationType</c> as a <c>string</c> — a consumer must be able to parse
/// them without referencing the module that raised them — so the handler side of this mapping
/// has to work on strings anyway. Using them on both sides means one vocabulary rather than two
/// that can drift.
/// </para>
/// </summary>
public static class ProviderVerificationTypeMap
{
    /// <summary>The Trust <c>VerificationType</c> name a B7 kind submits as.</summary>
    public static string ToTrust(ProviderVerificationKind kind) => kind switch
    {
        ProviderVerificationKind.Identity => "Identity",
        ProviderVerificationKind.Business => "Business",
        ProviderVerificationKind.Organization => "Organization",
        ProviderVerificationKind.Professional => "Professional",
        ProviderVerificationKind.Embassy => "Embassy",

        // A certification is a professional credential under review; Trust has no finer type and
        // does not need one, because the certification's own identifier travels with the claim.
        ProviderVerificationKind.Certification => "Professional",

        // A supporting document about where the provider operates. Trust's Residence handler is
        // the closest published match and is what a reviewer would open anyway.
        _ => "Residence"
    };

    /// <summary>
    /// The B7 kind a Trust event belongs to, or null when it is about something else.
    /// <para>
    /// Null is the common case and not an error: Trust raises verification events for phone
    /// possession, email possession and diaspora eligibility, none of which is about a provider.
    /// A handler that treated an unmapped type as a failure would log an error several times a
    /// minute in a healthy system.
    /// </para>
    /// </summary>
    public static ProviderVerificationKind? FromTrust(string? verificationType) =>
        verificationType?.Trim() switch
        {
            "Identity" => ProviderVerificationKind.Identity,
            "Business" => ProviderVerificationKind.Business,
            "Organization" => ProviderVerificationKind.Organization,
            "Professional" => ProviderVerificationKind.Professional,
            "Embassy" => ProviderVerificationKind.Embassy,
            "Residence" => ProviderVerificationKind.Document,
            _ => null
        };
}
