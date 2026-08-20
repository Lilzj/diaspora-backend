using Diaspora.Modules.Trust.Domain;

namespace Diaspora.Modules.Trust.Extensibility.Handlers;

/// <summary>
/// Shared behaviour for the handlers Trust ships with.
/// <para>
/// Defaults are deliberately the cautious ones: documents required, no automatic approval,
/// no expiry. A handler that wants something riskier has to say so explicitly, which is the
/// right way round for a component that decides who may publish.
/// </para>
/// </summary>
public abstract class VerificationTypeHandlerBase : IVerificationTypeHandler
{
    public abstract VerificationType Type { get; }

    public virtual IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments => [];

    public virtual bool RequiresDocument => true;

    public virtual bool RequiresSelfie => false;

    public virtual TimeSpan? GrantLifetime => null;

    public virtual TimeSpan ReviewSla => TimeSpan.FromHours(48);

    public virtual bool SupportsAutomaticApproval => false;

    public virtual ReputationAward? ApprovalAward => null;

    public virtual IReadOnlyCollection<string> Validate(VerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var problems = new List<string>();

        if (RequiresDocument && request.Documents.Count == 0)
        {
            problems.Add("At least one supporting document is required.");
        }

        if (RequiresSelfie && string.IsNullOrWhiteSpace(request.SelfieStorageKey))
        {
            problems.Add("A biometric selfie is required for this verification.");
        }

        if (AcceptedDocuments.Count > 0)
        {
            foreach (var document in request.Documents)
            {
                if (!AcceptedDocuments.Contains(document.DocumentType))
                {
                    problems.Add($"'{document.DocumentType}' is not accepted for {Type} verification.");
                }
            }
        }

        return problems;
    }

    public virtual AutomaticDecision Evaluate(VerificationRequest request, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Expired paperwork is the one thing worth refusing without a human: it is a fact on
        // the document rather than a judgement about it, and queuing it wastes a reviewer.
        var today = DateOnly.FromDateTime(asOf.UtcDateTime);
        if (request.Documents.Any(document => document.IsExpired(today)))
        {
            return AutomaticDecision.Reject(
                $"{Type.ToString().ToLowerInvariant()}-expiry-check",
                VerificationRejectionReason.EXPIRED_DOCUMENT,
                "The document you submitted has expired.");
        }

        return AutomaticDecision.Defer;
    }
}

/// <summary>
/// The platform's core eligibility check: is this person actually diaspora?
/// <para>
/// The one verification the whole product rests on. It refuses automatically when nationality
/// and residence match — that is not a judgement call, it is the definition of the community,
/// and the frontend already refuses it client-side in <c>TrustEngine.evaluateEligibility</c>.
/// Everything else goes to a human, because the alternative is a document-forgery check
/// performed by a regular expression.
/// </para>
/// </summary>
public sealed class DiasporaVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Diaspora;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.Passport,
        VerificationDocumentType.ResidencePermit,
        VerificationDocumentType.WorkPermit,
        VerificationDocumentType.OccupationPermit,
        VerificationDocumentType.StudentPermit,
        VerificationDocumentType.Visa,
        VerificationDocumentType.GovernmentImmigrationDocuments,
        VerificationDocumentType.Other
    ];

    public override bool RequiresSelfie => true;

    public override TimeSpan ReviewSla => TimeSpan.FromHours(48);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.CountryVerified,
        ReputationDimension.verification,
        30,
        "Diaspora status verified.");

    public override IReadOnlyCollection<string> Validate(VerificationRequest request)
    {
        var problems = base.Validate(request).ToList();

        if (string.IsNullOrWhiteSpace(request.Nationality))
        {
            problems.Add("Nationality is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ResidenceCountry))
        {
            problems.Add("Country of residence is required.");
        }

        return problems;
    }

    public override AutomaticDecision Evaluate(VerificationRequest request, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(request);

        var baseDecision = base.Evaluate(request, asOf);
        if (baseDecision.Kind != AutomaticDecisionKind.Defer)
        {
            return baseDecision;
        }

        if (!string.IsNullOrWhiteSpace(request.Nationality)
            && string.Equals(request.Nationality, request.ResidenceCountry, StringComparison.OrdinalIgnoreCase))
        {
            return AutomaticDecision.Reject(
                "diaspora-eligibility",
                VerificationRejectionReason.NOT_DIASPORA_ELIGIBLE,
                "Diaspora is for people living outside their country of origin. Your declared "
                + "nationality and country of residence are the same.");
        }

        return AutomaticDecision.Defer;
    }
}

/// <summary>
/// Government ID plus a biometric face match.
/// <para>
/// Never auto-approved. This is the verification that unlocks publishing, and an automatic
/// path to it is an automatic path for whoever finds the hole.
/// </para>
/// </summary>
public sealed class IdentityVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Identity;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.Passport,
        VerificationDocumentType.NationalId,
        VerificationDocumentType.DriverLicense,
        VerificationDocumentType.ResidencePermit,
        VerificationDocumentType.WorkPermit,
        VerificationDocumentType.StudentPermit
    ];

    public override bool RequiresSelfie => true;

    public override TimeSpan ReviewSla => TimeSpan.FromHours(24);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.GovernmentIdVerified,
        ReputationDimension.verification,
        40,
        "Government ID and biometric check passed.");
}

/// <summary>
/// Residency in a country community.
/// <para>
/// The only type with a real expiry: permits lapse, and a member whose right to remain has
/// ended should not keep vouching for local knowledge indefinitely. Two years is the
/// re-check interval, well inside the shortest common permit term.
/// </para>
/// </summary>
public sealed class CountryVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Country;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.PassportStamp,
        VerificationDocumentType.ResidencePermit,
        VerificationDocumentType.WorkPermit,
        VerificationDocumentType.StudentVisa,
        VerificationDocumentType.Visa,
        VerificationDocumentType.UtilityBill,
        VerificationDocumentType.BankStatement,
        VerificationDocumentType.ProofOfAddress,
        VerificationDocumentType.Other
    ];

    public override TimeSpan? GrantLifetime => TimeSpan.FromDays(365 * 2);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.CountryVerified,
        ReputationDimension.verification,
        30,
        "Residency verified.");

    public override IReadOnlyCollection<string> Validate(VerificationRequest request)
    {
        var problems = base.Validate(request).ToList();

        if (string.IsNullOrWhiteSpace(request.TargetCountry))
        {
            problems.Add("A target country is required.");
        }

        if (request.ArrivalDate is null)
        {
            problems.Add("An arrival date is required.");
        }

        return problems;
    }
}

/// <summary>Proof of address, distinct from country-level residency.</summary>
public sealed class ResidenceVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Residence;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.UtilityBill,
        VerificationDocumentType.BankStatement,
        VerificationDocumentType.ProofOfAddress,
        VerificationDocumentType.ResidencePermit
    ];

    public override TimeSpan? GrantLifetime => TimeSpan.FromDays(365);
}

/// <summary>Employment, evidenced by a letter or contract.</summary>
public sealed class EmploymentVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Employment;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.EmploymentLetter,
        VerificationDocumentType.WorkPermit,
        VerificationDocumentType.OccupationPermit
    ];

    public override TimeSpan? GrantLifetime => TimeSpan.FromDays(365);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.ProviderVerified,
        ReputationDimension.verification,
        25,
        "Employment verified.");
}

/// <summary>A registered business.</summary>
public sealed class BusinessVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Business;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.BusinessRegistration,
        VerificationDocumentType.OrganizationDocument
    ];

    public override TimeSpan ReviewSla => TimeSpan.FromDays(5);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.ProviderVerified,
        ReputationDimension.verification,
        25,
        "Business verified.");
}

/// <summary>A community organisation.</summary>
public sealed class OrganizationVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Organization;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.OrganizationDocument,
        VerificationDocumentType.BusinessRegistration
    ];

    public override TimeSpan ReviewSla => TimeSpan.FromDays(5);
}

/// <summary>A professional credential.</summary>
public sealed class ProfessionalVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Professional;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.EmploymentLetter,
        VerificationDocumentType.OccupationPermit,
        VerificationDocumentType.Other
    ];

    public override TimeSpan? GrantLifetime => TimeSpan.FromDays(365 * 2);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.ProviderVerified,
        ReputationDimension.verification,
        25,
        "Professional credential verified.");
}

/// <summary>Embassy or consulate endorsement.</summary>
public sealed class EmbassyVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Embassy;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.GovernmentImmigrationDocuments,
        VerificationDocumentType.OrganizationDocument,
        VerificationDocumentType.Other
    ];

    public override TimeSpan ReviewSla => TimeSpan.FromDays(10);
}

/// <summary>
/// Community guide eligibility.
/// <para>
/// Evidence-based rather than document-based: what makes a guide is a contribution record,
/// which the reviewer reads from the member's profile rather than from an upload.
/// </para>
/// </summary>
public sealed class CommunityGuideVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.CommunityGuide;

    public override IReadOnlyCollection<VerificationDocumentType> AcceptedDocuments =>
    [
        VerificationDocumentType.GuideEvidence,
        VerificationDocumentType.Other
    ];

    public override bool RequiresDocument => false;

    public override TimeSpan ReviewSla => TimeSpan.FromDays(7);
}

/// <summary>Contributor eligibility — the right to publish guides.</summary>
public sealed class ContributorVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Contributor;

    public override bool RequiresDocument => false;

    public override TimeSpan ReviewSla => TimeSpan.FromDays(3);
}

/// <summary>
/// Phone possession.
/// <para>
/// Identity owns the OTP flow and the actual proof. This handler exists so the type appears
/// in the trust centre's checklist and contributes to reputation — Trust mirrors the outcome,
/// it does not re-verify it, which is why it needs no document and auto-approves.
/// </para>
/// </summary>
public sealed class PhoneVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Phone;

    public override bool RequiresDocument => false;

    public override bool SupportsAutomaticApproval => true;

    public override TimeSpan ReviewSla => TimeSpan.FromHours(1);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.PhoneVerified,
        ReputationDimension.verification,
        20,
        "Phone number verified.");

    public override AutomaticDecision Evaluate(VerificationRequest request, DateTimeOffset asOf) =>
        AutomaticDecision.Approve("identity-phone-mirror", "Phone verified by Identity.");
}

/// <summary>Email possession. Mirrors Identity, exactly as the phone handler does.</summary>
public sealed class EmailVerificationHandler : VerificationTypeHandlerBase
{
    public override VerificationType Type => VerificationType.Email;

    public override bool RequiresDocument => false;

    public override bool SupportsAutomaticApproval => true;

    public override TimeSpan ReviewSla => TimeSpan.FromHours(1);

    public override ReputationAward? ApprovalAward => ReputationAward.Of(
        ReputationActions.EmailVerified,
        ReputationDimension.verification,
        10,
        "Email address verified.");

    public override AutomaticDecision Evaluate(VerificationRequest request, DateTimeOffset asOf) =>
        AutomaticDecision.Approve("identity-email-mirror", "Email verified by Identity.");
}
